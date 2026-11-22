using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Shared.CodeGeneration.Chained.Rendering;
using static Microsoft.CodeAnalysis.CSharp.SyntaxFactory;

namespace Shared.CodeGeneration.Chained;

internal sealed class Expr
{
    private const string NoCompoundForm = "The operator has no compound assignment form.";

    internal Expr(ExpressionSyntax? node) => Node = node;

    internal static Expr Empty { get; } = new(null);
    internal ExpressionSyntax? Node { get; }
    internal bool IsEmpty => Node is null;

    public Expr Dot(string name, bool when = true)
    {
        return ApplyPostfix(when, node => node is MemberBindingExpressionSyntax or ElementBindingExpressionSyntax
            ? MemberAccessExpression(SyntaxKind.SimpleMemberAccessExpression, node, BuildMemberName(name))
            : MemberAccessExpression(SyntaxKind.SimpleMemberAccessExpression, ParenthesizeForPostfix(node),
                BuildMemberName(name)));
    }

    public Expr NullDot(string name, bool when = true)
    {
        return ApplyPostfix(when, node => ConditionalAccessExpression(ParenthesizeForPostfix(node),
            MemberBindingExpression(BuildMemberName(name))));
    }

    public Expr Call(IReadOnlyList<Arg>? arguments = null, bool when = true)
    {
        return ApplyPostfix(when,
            node => InvocationExpression(PrepareReceiver(node), ArgumentList(BuildArguments(arguments))));
    }

    public Expr TypeArgs(IReadOnlyList<TypeRef> arguments, bool when = true)
    {
        var list = TypeArgumentList(SeparatedList(arguments.Where(static argument => argument.IsEmpty is false)
            .Select(static argument => argument.Node!)));
        return ApplyPostfix(when && list.Arguments.Count > 0, node => node switch
        {
            IdentifierNameSyntax identifier => GenericName(identifier.Identifier, list),
            MemberAccessExpressionSyntax { Name: IdentifierNameSyntax name } access
                => access.WithName(GenericName(name.Identifier, list)),
            MemberBindingExpressionSyntax { Name: IdentifierNameSyntax name } binding
                => binding.WithName(GenericName(name.Identifier, list)),
            _ => node
        });
    }

    public Expr Index(IReadOnlyList<Arg> arguments, bool when = true)
    {
        return ApplyPostfix(when,
            node => ElementAccessExpression(PrepareReceiver(node), BracketedArgumentList(BuildArguments(arguments))));
    }

    public Expr Init(IReadOnlyList<Expr> items, bool when = true)
    {
        if (when is false || Node is not BaseObjectCreationExpressionSyntax creation) return this;

        var nodes = items.Where(static item => item.IsEmpty is false).Select(static item => item.Node!).ToArray();

        var kind = nodes.All(static node => node is AssignmentExpressionSyntax)
            ? SyntaxKind.ObjectInitializerExpression
            : SyntaxKind.CollectionInitializerExpression;
        return new(creation.WithInitializer(InitializerExpression(kind, SeparatedList(nodes))));
    }

    public Expr Assign(Expr value, bool when = true)
    {
        if (when is false || Node is null || value.Node is null) return this;
        return new(AssignmentExpression(SyntaxKind.SimpleAssignmentExpression,
            Precedence.Parenthesize(Node, Precedence.Unary), value.Node));
    }

    public Expr Binary(BinaryOperator @operator, Expr right, bool when = true)
    {
        if (when is false || Node is null || right.Node is null) return this;

        var kind = MapOperator(@operator);
        var precedence = Precedence.Rank(kind);
        var left = EndsWithType(Node) ? ParenthesizedExpression(Node) : Precedence.Parenthesize(Node, precedence);
        return new(BinaryExpression(kind, left, Precedence.Parenthesize(right.Node, precedence + 1)));
    }

    public Expr CompoundAssign(BinaryOperator @operator, Expr value, bool when = true)
    {
        if (when is false || Node is null || value.Node is null) return this;
        return new(AssignmentExpression(MapCompound(@operator), Precedence.Parenthesize(Node, Precedence.Unary),
            value.Node));
    }

    public Expr Coalesce(Expr right, bool when = true)
    {
        if (when is false || Node is null || right.Node is null) return this;
        var rightOperand = right.Node is ThrowExpressionSyntax
            ? right.Node
            : Precedence.Parenthesize(right.Node, Precedence.Coalesce);
        return new(BinaryExpression(SyntaxKind.CoalesceExpression,
            Precedence.Parenthesize(Node, Precedence.Coalesce + 1), rightOperand));
    }

    public Expr Ternary(Expr whenTrue, Expr whenFalse, bool when = true)
    {
        if (when is false || Node is null || whenTrue.Node is null || whenFalse.Node is null) return this;
        return new(ConditionalExpression(Precedence.Parenthesize(Node, Precedence.Coalesce), whenTrue.Node,
            whenFalse.Node));
    }

    public Expr As(TypeRef type, bool when = true)
    {
        if (when is false || Node is null || type.Node is null) return this;
        return new(BinaryExpression(SyntaxKind.AsExpression, Precedence.Parenthesize(Node, Precedence.Relational),
            type.Node));
    }

    public Expr NullForgiving(bool when = true)
    {
        return ApplyPostfix(when,
            node => PostfixUnaryExpression(SyntaxKind.SuppressNullableWarningExpression, PrepareReceiver(node)));
    }

    public Expr Is(Expr pattern, bool when = true)
    {
        if (when is false || Node is null || pattern.Node is null) return this;
        return new(IsPatternExpression(Precedence.Parenthesize(Node, Precedence.Relational),
            BuildPattern(pattern.Node)));
    }

    public ArmChain Switch(bool when = true) => new(this, when && Node is not null);

    internal static PatternSyntax BuildPattern(ExpressionSyntax node)
    {
        return node switch
        {
            IdentifierNameSyntax { Identifier.Text: "_" } => DiscardPattern(),
            PrefixUnaryExpressionSyntax { RawKind: (int)SyntaxKind.LogicalNotExpression } negation
                => UnaryPattern(Token(SyntaxKind.NotKeyword), BuildPattern(Unwrap(negation.Operand))),
            _ => ConstantPattern(Precedence.Parenthesize(node, Precedence.Shift))
        };
    }

    internal static SeparatedSyntaxList<ArgumentSyntax> BuildArguments(IReadOnlyList<Arg>? arguments)
    {
        return SeparatedList(arguments?.Where(static argument => argument.IsEmpty is false)
            .Select(static argument => argument.ToArgument()) ?? []);
    }

    private static IdentifierNameSyntax BuildMemberName(string name) => IdentifierName(Names.CreateIdentifier(name));

    private static bool EndsWithType(ExpressionSyntax node)
    {
        return node.Kind() is SyntaxKind.AsExpression or SyntaxKind.IsExpression;
    }

    private static ExpressionSyntax Unwrap(ExpressionSyntax node)
    {
        return node is ParenthesizedExpressionSyntax parenthesized ? parenthesized.Expression : node;
    }

    private static ExpressionSyntax ParenthesizeForPostfix(ExpressionSyntax node)
    {
        return Precedence.Parenthesize(node, Precedence.Primary);
    }

    private static ExpressionSyntax PrepareReceiver(ExpressionSyntax node)
    {
        return node is MemberBindingExpressionSyntax or ElementBindingExpressionSyntax
            ? node
            : ParenthesizeForPostfix(node);
    }

    private Expr ApplyPostfix(bool when, Func<ExpressionSyntax, ExpressionSyntax> apply)
    {
        if (when is false || Node is null) return this;
        return new(Node is ConditionalAccessExpressionSyntax access ? ApplyInside(access, apply) : apply(Node));
    }

    private static ConditionalAccessExpressionSyntax ApplyInside(ConditionalAccessExpressionSyntax access,
        Func<ExpressionSyntax, ExpressionSyntax> apply)
    {
        return access.WhenNotNull is ConditionalAccessExpressionSyntax inner
            ? access.WithWhenNotNull(ApplyInside(inner, apply))
            : access.WithWhenNotNull(apply(access.WhenNotNull));
    }

    private static SyntaxKind MapCompound(BinaryOperator @operator)
    {
        return @operator switch
        {
            BinaryOperator.Add => SyntaxKind.AddAssignmentExpression,
            BinaryOperator.Subtract => SyntaxKind.SubtractAssignmentExpression,
            BinaryOperator.Multiply => SyntaxKind.MultiplyAssignmentExpression,
            BinaryOperator.Divide => SyntaxKind.DivideAssignmentExpression,
            BinaryOperator.Modulo => SyntaxKind.ModuloAssignmentExpression,
            BinaryOperator.LeftShift => SyntaxKind.LeftShiftAssignmentExpression,
            BinaryOperator.RightShift => SyntaxKind.RightShiftAssignmentExpression,
            BinaryOperator.BitwiseAnd => SyntaxKind.AndAssignmentExpression,
            BinaryOperator.BitwiseOr => SyntaxKind.OrAssignmentExpression,
            BinaryOperator.ExclusiveOr => SyntaxKind.ExclusiveOrAssignmentExpression,
            _ => throw new ArgumentOutOfRangeException(nameof(@operator), @operator, NoCompoundForm)
        };
    }

    private static SyntaxKind MapOperator(BinaryOperator @operator)
    {
        return @operator switch
        {
            BinaryOperator.Add => SyntaxKind.AddExpression,
            BinaryOperator.Subtract => SyntaxKind.SubtractExpression,
            BinaryOperator.Multiply => SyntaxKind.MultiplyExpression,
            BinaryOperator.Divide => SyntaxKind.DivideExpression,
            BinaryOperator.Modulo => SyntaxKind.ModuloExpression,
            BinaryOperator.LeftShift => SyntaxKind.LeftShiftExpression,
            BinaryOperator.RightShift => SyntaxKind.RightShiftExpression,
            BinaryOperator.Less => SyntaxKind.LessThanExpression,
            BinaryOperator.LessOrEqual => SyntaxKind.LessThanOrEqualExpression,
            BinaryOperator.Greater => SyntaxKind.GreaterThanExpression,
            BinaryOperator.GreaterOrEqual => SyntaxKind.GreaterThanOrEqualExpression,
            BinaryOperator.Equal => SyntaxKind.EqualsExpression,
            BinaryOperator.NotEqual => SyntaxKind.NotEqualsExpression,
            BinaryOperator.BitwiseAnd => SyntaxKind.BitwiseAndExpression,
            BinaryOperator.BitwiseOr => SyntaxKind.BitwiseOrExpression,
            BinaryOperator.ExclusiveOr => SyntaxKind.ExclusiveOrExpression,
            BinaryOperator.And => SyntaxKind.LogicalAndExpression,
            BinaryOperator.Or => SyntaxKind.LogicalOrExpression,
            _ => throw new ArgumentOutOfRangeException(nameof(@operator), @operator, null)
        };
    }
}
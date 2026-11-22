using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Shared.CodeGeneration.Scoped.Rendering;
using static Microsoft.CodeAnalysis.CSharp.SyntaxFactory;

namespace Shared.CodeGeneration.Scoped;

internal sealed class Expr
{
    internal Expr(ExpressionSyntax? node) => Node = node;

    internal static Expr Empty { get; } = new(null);
    internal ExpressionSyntax? Node { get; }
    internal bool IsEmpty => Node is null;

    public Expr Dot(string name, bool when = true)
    {
        return Postfix(when, node => node is MemberBindingExpressionSyntax or ElementBindingExpressionSyntax
            ? MemberAccessExpression(SyntaxKind.SimpleMemberAccessExpression, node, Member(name))
            : MemberAccessExpression(SyntaxKind.SimpleMemberAccessExpression, Primary(node), Member(name)));
    }

    public Expr NullDot(string name, bool when = true)
    {
        return Postfix(when, node => ConditionalAccessExpression(Primary(node), MemberBindingExpression(Member(name))));
    }

    public Expr Call(IReadOnlyList<Arg>? arguments = null, bool when = true)
    {
        return Postfix(when, node => InvocationExpression(Receiver(node), ArgumentList(Arguments(arguments))));
    }

    public Expr TypeArgs(IReadOnlyList<TypeRef> arguments, bool when = true)
    {
        var list = TypeArgumentList(SeparatedList(arguments.Where(static argument => argument.IsEmpty is false)
            .Select(static argument => argument.Node!)));
        return Postfix(when && list.Arguments.Count > 0, node => node switch
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
        return Postfix(when,
            node => ElementAccessExpression(Receiver(node), BracketedArgumentList(Arguments(arguments))));
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
            Precedence.AtLeast(Node, Precedence.Unary), value.Node));
    }

    public Expr Binary(BinaryOperator @operator, Expr right, bool when = true)
    {
        if (when is false || Node is null || right.Node is null) return this;

        var kind = KindOf(@operator);
        var precedence = Precedence.Of(kind);
        return new(BinaryExpression(kind, Precedence.AtLeast(Node, precedence),
            Precedence.AtLeast(right.Node, precedence + 1)));
    }

    public Expr NullForgiving(bool when = true)
    {
        return Postfix(when,
            node => PostfixUnaryExpression(SyntaxKind.SuppressNullableWarningExpression, Receiver(node)));
    }

    public Expr Is(Expr pattern, bool when = true)
    {
        if (when is false || Node is null || pattern.Node is null) return this;
        return new(IsPatternExpression(Precedence.AtLeast(Node, Precedence.Relational), Pattern(pattern.Node)));
    }

    public Expr Switch(Action<ArmScope> arms, bool when = true)
    {
        if (when is false || Node is null) return this;

        var scope = new ArmScope();
        arms(scope);
        return new(scope.Complete(SwitchExpression(Precedence.AtLeast(Node, Precedence.Unary))));
    }

    internal static PatternSyntax Pattern(ExpressionSyntax node)
    {
        return node is IdentifierNameSyntax { Identifier.Text: "_" } ? DiscardPattern() : ConstantPattern(node);
    }

    internal static SeparatedSyntaxList<ArgumentSyntax> Arguments(IReadOnlyList<Arg>? arguments)
    {
        return SeparatedList(arguments?.Where(static argument => argument.IsEmpty is false)
            .Select(static argument => argument.ToArgument()) ?? []);
    }

    private static IdentifierNameSyntax Member(string name) => IdentifierName(Names.Identifier(name));
    private static ExpressionSyntax Primary(ExpressionSyntax node) => Precedence.AtLeast(node, Precedence.Primary);

    private static ExpressionSyntax Receiver(ExpressionSyntax node)
    {
        return node is MemberBindingExpressionSyntax or ElementBindingExpressionSyntax ? node : Primary(node);
    }

    private Expr Postfix(bool when, Func<ExpressionSyntax, ExpressionSyntax> apply)
    {
        if (when is false || Node is null) return this;
        return new(Node is ConditionalAccessExpressionSyntax access ? Inside(access, apply) : apply(Node));
    }

    private static ConditionalAccessExpressionSyntax Inside(ConditionalAccessExpressionSyntax access,
        Func<ExpressionSyntax, ExpressionSyntax> apply)
    {
        return access.WhenNotNull is ConditionalAccessExpressionSyntax inner
            ? access.WithWhenNotNull(Inside(inner, apply))
            : access.WithWhenNotNull(apply(access.WhenNotNull));
    }

    private static SyntaxKind KindOf(BinaryOperator @operator) => @operator switch
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
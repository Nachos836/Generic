using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Shared.CodeGeneration.Scoped.Rendering;
using static Microsoft.CodeAnalysis.CSharp.SyntaxFactory;

namespace Shared.CodeGeneration.Scoped;

internal static class Blocks
{
    public static TypeRef T(string name, IReadOnlyList<TypeRef>? arguments = null, bool when = true)
    {
        if (when is false) return TypeRef.Empty;

        var keyword = SyntaxFacts.GetKeywordKind(name);
        if (SyntaxFacts.IsPredefinedType(keyword)) return new(PredefinedType(Token(keyword)));

        var lastDot = name.LastIndexOf('.');
        var simple = TypeRef.BuildSimpleName(name.Substring(lastDot + 1), arguments);
        return new(lastDot < 0 ? simple : QualifiedName(Names.Qualify(name.Substring(0, lastDot)), simple));
    }

    public static Expr Id(string name, bool when = true)
    {
        return BuildIf(when, () => IdentifierName(Names.CreateIdentifier(name)));
    }

    public static Expr Lit(string value, bool when = true)
    {
        return BuildIf(when, () => LiteralExpression(SyntaxKind.StringLiteralExpression, Literal(value)));
    }

    public static Expr Lit(char value, bool when = true)
    {
        return BuildIf(when, () => LiteralExpression(SyntaxKind.CharacterLiteralExpression, Literal(value)));
    }

    public static Expr Lit(int value, bool when = true) => BuildNumber(Literal(value), when);
    public static Expr Lit(uint value, bool when = true) => BuildNumber(Literal(value), when);
    public static Expr Lit(long value, bool when = true) => BuildNumber(Literal(value), when);
    public static Expr Lit(ulong value, bool when = true) => BuildNumber(Literal(value), when);

    public static Expr Lit(float value, bool when = true)
    {
        if (float.IsNaN(value) || float.IsInfinity(value)) return BuildNonFinite(SyntaxKind.FloatKeyword, value, when);
        return BuildNumber(Literal(value.ToString("R", CultureInfo.InvariantCulture) + "F", value), when);
    }

    public static Expr Lit(double value, bool when = true)
    {
        if (double.IsNaN(value) || double.IsInfinity(value))
        {
            return BuildNonFinite(SyntaxKind.DoubleKeyword, value, when);
        }

        var text = value.ToString("R", CultureInfo.InvariantCulture);
        return BuildNumber(Literal(text.IndexOfAny(['.', 'E']) < 0 ? text + "D" : text, value), when);
    }

    public static Expr Lit(bool value, bool when = true)
    {
        return BuildIf(when, () => LiteralExpression(value
            ? SyntaxKind.TrueLiteralExpression
            : SyntaxKind.FalseLiteralExpression));
    }

    public static Expr Default(TypeRef? type = null, bool when = true)
    {
        return BuildIf(when, () => type?.Node is { } node
            ? DefaultExpression(node)
            : LiteralExpression(SyntaxKind.DefaultLiteralExpression));
    }

    public static Expr This(bool when = true) => BuildIf(when, static () => ThisExpression());

    public static Expr New(TypeRef type, IReadOnlyList<Arg>? arguments = null, bool when = true)
    {
        return BuildIf(when && type.Node is not null,
            () => ObjectCreationExpression(type.Node!, ArgumentList(Expr.BuildArguments(arguments)), null));
    }

    public static Expr New(IReadOnlyList<Arg>? arguments = null, bool when = true)
    {
        return BuildIf(when,
            () => ImplicitObjectCreationExpression(ArgumentList(Expr.BuildArguments(arguments)), null));
    }

    public static Expr Cast(TypeRef type, Expr value, bool when = true)
    {
        return BuildIf(when && type.Node is not null && value.Node is not null,
            () => CastExpression(type.Node!, Precedence.ParenthesizeCastOperand(value.Node!)));
    }

    public static Expr TypeOf(TypeRef type, bool when = true)
    {
        return BuildIf(when && type.Node is not null, () => TypeOfExpression(type.Node!));
    }

    public static Expr SizeOf(TypeRef type, bool when = true)
    {
        return BuildIf(when && type.Node is not null, () => SizeOfExpression(type.Node!));
    }

    public static Expr NameOf(Expr value, bool when = true)
    {
        return BuildIf(when && value.Node is not null, () => InvocationExpression(IdentifierName("nameof"),
            ArgumentList(SingletonSeparatedList(Argument(value.Node!)))));
    }

    public static Expr AddressOf(Expr value, bool when = true)
    {
        return BuildIf(when && value.Node is not null, () => PrefixUnaryExpression(SyntaxKind.AddressOfExpression,
            Precedence.Parenthesize(value.Node!, Precedence.Unary)));
    }

    public static Expr Ref(Expr value, bool when = true)
    {
        return BuildIf(when && value.Node is not null, () => RefExpression(value.Node!));
    }

    public static Expr Discard(bool when = true) => BuildIf(when, static () => IdentifierName("_"));

    public static Expr ThrowExpression(Expr exception, bool when = true)
    {
        return BuildIf(when && exception.Node is not null, () => SyntaxFactory.ThrowExpression(exception.Node!));
    }

    public static Lambda Lambda(IReadOnlyList<string>? parameters = null, bool when = true)
    {
        return new(parameters, false, when);
    }

    public static Lambda StaticLambda(IReadOnlyList<string>? parameters = null, bool when = true)
    {
        return new(parameters, true, when);
    }

    public static Arg RefArg(Expr value, bool when = true) => when ? new(value, SyntaxKind.RefKeyword) : Arg.Empty;
    public static Arg InArg(Expr value, bool when = true) => when ? new(value, SyntaxKind.InKeyword) : Arg.Empty;
    public static Arg Named(string name, Arg value, bool when = true) => when ? value.Named(name) : Arg.Empty;
    public static IReadOnlyList<string> Lines(params string[] lines) => lines;

    private static Expr BuildNumber(Microsoft.CodeAnalysis.SyntaxToken literal, bool when)
    {
        return BuildIf(when, () => LiteralExpression(SyntaxKind.NumericLiteralExpression, literal));
    }

    private static Expr BuildNonFinite(SyntaxKind keyword, double value, bool when)
    {
        var member = double.IsNaN(value) ? "NaN" : value > 0 ? "PositiveInfinity" : "NegativeInfinity";
        return BuildIf(when, () => MemberAccessExpression(SyntaxKind.SimpleMemberAccessExpression,
            PredefinedType(Token(keyword)), IdentifierName(member)));
    }

    private static Expr BuildIf(bool when, System.Func<ExpressionSyntax> create)
    {
        return when ? new(create()) : Expr.Empty;
    }
}
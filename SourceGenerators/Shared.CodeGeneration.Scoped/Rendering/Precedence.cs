using System;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using static Microsoft.CodeAnalysis.CSharp.SyntaxKind;

namespace Shared.CodeGeneration.Scoped.Rendering;

internal static class Precedence
{
    public const int Assignment = 0;
    public const int Conditional = 1;
    public const int Coalesce = 2;
    public const int Relational = 9;
    public const int Shift = 10;
    public const int Switch = 13;
    public const int Range = 14;
    public const int Unary = 15;
    public const int Primary = 16;

    public static int Rank(ExpressionSyntax expression)
    {
        return IsSigned(expression) ? Unary : RankKind(expression);
    }

    public static int Rank(SyntaxKind binaryKind)
    {
        return Rank(SyntaxFactory.BinaryExpression(binaryKind, SyntaxFactory.IdentifierName("a"),
            SyntaxFactory.IdentifierName("b")));
    }

    public static ExpressionSyntax Parenthesize(ExpressionSyntax expression, int precedence)
    {
        return Rank(expression) >= precedence ? expression : SyntaxFactory.ParenthesizedExpression(expression);
    }

    public static ExpressionSyntax ParenthesizeCastOperand(ExpressionSyntax operand)
    {
        var ambiguous = IsSigned(operand) || operand.Kind() is UnaryMinusExpression or UnaryPlusExpression
            or AddressOfExpression or PointerIndirectionExpression or PreIncrementExpression or PreDecrementExpression;
        return ambiguous ? SyntaxFactory.ParenthesizedExpression(operand) : Parenthesize(operand, Unary);
    }

    private static bool IsSigned(ExpressionSyntax expression)
    {
        return expression is LiteralExpressionSyntax literal &&
               literal.Token.Text.StartsWith("-", StringComparison.Ordinal);
    }

    private static int RankKind(ExpressionSyntax expression)
    {
        return expression.Kind() switch
        {
            SimpleAssignmentExpression or AddAssignmentExpression or SubtractAssignmentExpression
                or MultiplyAssignmentExpression or DivideAssignmentExpression or ModuloAssignmentExpression
                or AndAssignmentExpression or OrAssignmentExpression or ExclusiveOrAssignmentExpression
                or LeftShiftAssignmentExpression or RightShiftAssignmentExpression or CoalesceAssignmentExpression
                or UnsignedRightShiftAssignmentExpression or ParenthesizedLambdaExpression or SimpleLambdaExpression
                or AnonymousMethodExpression or ThrowExpression or QueryExpression => Assignment,
            ConditionalExpression => Conditional,
            CoalesceExpression => Coalesce,
            LogicalOrExpression => 3,
            LogicalAndExpression => 4,
            BitwiseOrExpression => 5,
            ExclusiveOrExpression => 6,
            BitwiseAndExpression => 7,
            EqualsExpression or NotEqualsExpression => 8,
            LessThanExpression or LessThanOrEqualExpression or GreaterThanExpression or GreaterThanOrEqualExpression
                or IsExpression or IsPatternExpression or AsExpression => Relational,
            LeftShiftExpression or RightShiftExpression or UnsignedRightShiftExpression => 10,
            AddExpression or SubtractExpression => 11,
            MultiplyExpression or DivideExpression or ModuloExpression => 12,
            SwitchExpression or WithExpression => Switch,
            RangeExpression => Range,
            CastExpression or LogicalNotExpression or UnaryMinusExpression or UnaryPlusExpression
                or BitwiseNotExpression or PreIncrementExpression or PreDecrementExpression
                or AddressOfExpression or PointerIndirectionExpression or RefExpression or AwaitExpression
                or IndexExpression => Unary,
            _ => Primary
        };
    }
}
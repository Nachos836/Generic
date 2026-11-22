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
    public const int Unary = 14;
    public const int Primary = 15;

    public static int Of(ExpressionSyntax expression)
    {
        return IsSigned(expression) ? Unary : OfKind(expression);
    }

    public static int Of(SyntaxKind binaryKind)
    {
        return Of(SyntaxFactory.BinaryExpression(binaryKind, SyntaxFactory.IdentifierName("a"),
            SyntaxFactory.IdentifierName("b")));
    }

    public static ExpressionSyntax AtLeast(ExpressionSyntax expression, int precedence)
    {
        return Of(expression) >= precedence ? expression : SyntaxFactory.ParenthesizedExpression(expression);
    }

    public static ExpressionSyntax CastOperand(ExpressionSyntax operand)
    {
        var ambiguous = IsSigned(operand) || operand.Kind() is UnaryMinusExpression or UnaryPlusExpression
            or AddressOfExpression or PointerIndirectionExpression or PreIncrementExpression or PreDecrementExpression;
        return ambiguous ? SyntaxFactory.ParenthesizedExpression(operand) : AtLeast(operand, Unary);
    }

    private static bool IsSigned(ExpressionSyntax expression)
    {
        return expression is LiteralExpressionSyntax literal &&
               literal.Token.Text.StartsWith("-", StringComparison.Ordinal);
    }

    private static int OfKind(ExpressionSyntax expression)
    {
        return expression.Kind() switch
        {
            SimpleAssignmentExpression or AddAssignmentExpression or SubtractAssignmentExpression
                or MultiplyAssignmentExpression or DivideAssignmentExpression or ModuloAssignmentExpression
                or AndAssignmentExpression or OrAssignmentExpression or ExclusiveOrAssignmentExpression
                or LeftShiftAssignmentExpression or RightShiftAssignmentExpression or CoalesceAssignmentExpression
                or ParenthesizedLambdaExpression or SimpleLambdaExpression or ThrowExpression => Assignment,
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
            LeftShiftExpression or RightShiftExpression => 10,
            AddExpression or SubtractExpression => 11,
            MultiplyExpression or DivideExpression or ModuloExpression => 12,
            SwitchExpression => Switch,
            CastExpression or LogicalNotExpression or UnaryMinusExpression or UnaryPlusExpression
                or BitwiseNotExpression or PreIncrementExpression or PreDecrementExpression
                or AddressOfExpression or PointerIndirectionExpression or RefExpression => Unary,
            _ => Primary
        };
    }
}
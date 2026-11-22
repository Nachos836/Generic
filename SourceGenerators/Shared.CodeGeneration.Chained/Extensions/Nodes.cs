using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace Shared.CodeGeneration.Chained.Extensions;

internal interface IStatementExtensionHost<out TSelf>
{
    TSelf AddExtension(StatementSyntax statement, bool when);
}

internal interface IMemberExtensionHost<out TSelf>
{
    TSelf AddExtension(MemberDeclarationSyntax member, bool when);
}

internal static class Nodes
{
    public static TypeRef Type(TypeSyntax node, bool when = true) => when ? new(node) : TypeRef.Empty;
    public static Expr Expression(ExpressionSyntax node, bool when = true) => when ? new(node) : Expr.Empty;

    public static TSelf Statement<TSelf>(this IStatementExtensionHost<TSelf> chain, StatementSyntax node,
        bool when = true)
    {
        return chain.AddExtension(node, when);
    }

    public static TSelf Member<TSelf>(this IMemberExtensionHost<TSelf> chain, MemberDeclarationSyntax node,
        bool when = true)
    {
        return chain.AddExtension(node, when);
    }
}

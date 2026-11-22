using System;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace Shared.CodeGeneration.Chained.Extensions;

internal interface IStatementExtensionHost<TSelf> where TSelf : class, IStatementHost
{
    TSelf AddExtension(StatementSyntax statement, bool when);
    StatementBlockChain<TSelf> OpenExtension(Func<BlockSyntax, StatementSyntax> wrap, bool when);
}

internal interface IMemberExtensionHost<out TSelf>
{
    TSelf AddExtension(MemberDeclarationSyntax member, bool when);
}

internal static class Nodes
{
    public static TypeRef Type(TypeSyntax node, bool when = true) => when ? new(node) : TypeRef.Empty;
    public static Expr Expression(ExpressionSyntax node, bool when = true) => when ? new(node) : Expr.Empty;

    public static TypeRef Type(TypeRef type, Func<TypeSyntax, TypeSyntax> wrap, bool when = true)
    {
        return when && type.Node is { } node ? new(wrap(node)) : TypeRef.Empty;
    }

    public static Expr Expression(Expr value, Func<ExpressionSyntax, ExpressionSyntax> wrap, bool when = true)
    {
        return when && value.Node is { } node ? new(wrap(node)) : Expr.Empty;
    }

    public static TSelf Statement<TSelf>(this IStatementExtensionHost<TSelf> chain, StatementSyntax node,
        bool when = true) where TSelf : class, IStatementHost
    {
        return chain.AddExtension(node, when);
    }

    public static StatementBlockChain<TSelf> Statement<TSelf>(this IStatementExtensionHost<TSelf> chain,
        Func<BlockSyntax, StatementSyntax> wrap, bool when = true) where TSelf : class, IStatementHost
    {
        return chain.OpenExtension(wrap, when);
    }

    public static TSelf Member<TSelf>(this IMemberExtensionHost<TSelf> chain, MemberDeclarationSyntax node,
        bool when = true)
    {
        return chain.AddExtension(node, when);
    }
}

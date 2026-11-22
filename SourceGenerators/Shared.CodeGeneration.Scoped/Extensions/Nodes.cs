using System;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using static Microsoft.CodeAnalysis.CSharp.SyntaxFactory;

namespace Shared.CodeGeneration.Scoped.Extensions;

internal interface IStatementExtensionHost
{
    BodyScope AddExtension(StatementSyntax statement, bool when);
}

internal interface IMemberExtensionHost<out TNext>
{
    TNext AddExtension(MemberDeclarationSyntax member, bool when);
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

    public static BodyScope Statement(this IStatementExtensionHost scope, StatementSyntax node, bool when = true)
    {
        return scope.AddExtension(node, when);
    }

    public static BodyScope Statement(this IStatementExtensionHost scope, Func<BlockSyntax, StatementSyntax> wrap,
        Action<BodyScope> body, bool when = true)
    {
        return scope.AddExtension(when ? wrap(BodyScope.BuildBlock(body)) : EmptyStatement(), when);
    }

    public static TNext Member<TNext>(this IMemberExtensionHost<TNext> scope, MemberDeclarationSyntax node,
        bool when = true)
    {
        return scope.AddExtension(node, when);
    }
}

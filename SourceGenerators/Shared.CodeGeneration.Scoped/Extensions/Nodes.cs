using Microsoft.CodeAnalysis.CSharp.Syntax;

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

    public static BodyScope Statement(this IStatementExtensionHost scope, StatementSyntax node, bool when = true)
    {
        return scope.AddExtension(node, when);
    }

    public static TNext Member<TNext>(this IMemberExtensionHost<TNext> scope, MemberDeclarationSyntax node,
        bool when = true)
    {
        return scope.AddExtension(node, when);
    }
}

using System;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using static Microsoft.CodeAnalysis.CSharp.SyntaxFactory;

namespace Shared.CodeGeneration.Scoped.Rendering;

internal static class Names
{
    private const string Verbatim = "@";

    public static SyntaxToken Identifier(string name)
    {
        if (name.StartsWith(Verbatim, StringComparison.Ordinal))
        {
            return SyntaxFactory.Identifier(default, SyntaxKind.IdentifierToken, name, name.Substring(1), default);
        }
        return SyntaxFacts.GetKeywordKind(name) == SyntaxKind.None
            ? SyntaxFactory.Identifier(name)
            : SyntaxFactory.Identifier(default, SyntaxKind.IdentifierToken, Verbatim + name, name, default);
    }

    public static NameSyntax Qualified(string dottedName)
    {
        var segments = dottedName.Split('.');
        NameSyntax name = IdentifierName(Identifier(segments[0]));

        for (var index = 1; index < segments.Length; index++)
        {
            name = QualifiedName(name, IdentifierName(Identifier(segments[index])));
        }
        return name;
    }
}
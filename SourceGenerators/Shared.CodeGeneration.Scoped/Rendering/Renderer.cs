using System.Collections.Generic;
using System.Linq;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using static Microsoft.CodeAnalysis.CSharp.SyntaxFactory;

namespace Shared.CodeGeneration.Scoped.Rendering;

internal static class Renderer
{
    private const string Indentation = "\t";
    private const string LineBreak = "\n";
    private const string BlankLine = LineBreak + LineBreak;

    public static string Render(CompilationUnitSyntax unit)
    {
        return InsertGaps(unit.NormalizeWhitespace(Indentation, LineBreak, elasticTrivia: true)).ToFullString();
    }

    private static SyntaxNode InsertGaps(SyntaxNode root)
    {
        var gapTokens = new HashSet<SyntaxToken>(root.GetAnnotatedNodes(Lead.Gap)
            .Where(static node => node is not StructuredTriviaSyntax)
            .Select(static node => node.GetFirstToken())
            .Concat(root.GetAnnotatedTokens(Lead.Gap)));

        var tokens = root.DescendantTokens()
            .Where(token => gapTokens.Contains(token) || token.LeadingTrivia.Any(IsGap));
        return root.ReplaceTokens(tokens, (original, _) => original.WithLeadingTrivia(WithGaps(original, gapTokens)));
    }

    private static List<SyntaxTrivia> WithGaps(SyntaxToken token, HashSet<SyntaxToken> gapTokens)
    {
        var previousLine = token.GetPreviousToken(includeZeroWidth: true).TrailingTrivia.ToFullString();
        var trivia = new List<SyntaxTrivia>();

        foreach (var item in token.LeadingTrivia)
        {
            if (IsGap(item)) AddBlankLine(trivia, previousLine);
            trivia.Add(item);
        }

        if (gapTokens.Contains(token)) AddBlankLine(trivia, previousLine);
        return trivia;
    }

    private static void AddBlankLine(List<SyntaxTrivia> trivia, string previousLine)
    {
        var lineStart = trivia.Count;
        while (lineStart > 0 && trivia[lineStart - 1].IsKind(SyntaxKind.WhitespaceTrivia)) lineStart--;

        var textBefore =
            previousLine + string.Concat(trivia.Take(lineStart).Select(static item => item.ToFullString()));
        if (textBefore.Replace(" ", string.Empty).Replace(Indentation, string.Empty).EndsWith(BlankLine)) return;

        trivia.Insert(lineStart, EndOfLine(LineBreak));
    }

    private static bool IsGap(SyntaxTrivia trivia)
    {
        return trivia.HasAnnotation(Lead.Gap) || trivia.GetStructure()?.HasAnnotation(Lead.Gap) is true;
    }
}
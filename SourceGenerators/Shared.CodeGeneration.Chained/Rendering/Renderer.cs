using System.Collections.Generic;
using System.Linq;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using static Microsoft.CodeAnalysis.CSharp.SyntaxFactory;

namespace Shared.CodeGeneration.Chained.Rendering;

internal static class Renderer
{
    private const string Indentation = "\t";
    private const string LineBreak = "\n";
    private const string BlankLine = LineBreak + LineBreak;

    public static string Render(CompilationUnitSyntax unit)
    {
        var normalized = unit.NormalizeWhitespace(Indentation, LineBreak, elasticTrivia: true);
        return TrimBeforeMissingTokens(InsertGaps(normalized)).ToFullString();
    }

    private static SyntaxNode TrimBeforeMissingTokens(SyntaxNode root)
    {
        var tokens = root.DescendantTokens()
            .Where(static token => token.GetNextToken(includeZeroWidth: true).IsMissing);
        return root.ReplaceTokens(tokens, static (original, _) => original.WithTrailingTrivia(
            original.TrailingTrivia.Reverse().SkipWhile(static item => item.IsKind(SyntaxKind.WhitespaceTrivia))
                .Reverse()));
    }

    private static SyntaxNode InsertGaps(SyntaxNode root)
    {
        var gapTokens = new HashSet<SyntaxToken>(root.GetAnnotatedNodes(Lead.Gap)
            .Where(static node => node is not StructuredTriviaSyntax)
            .Select(static node => node.GetFirstToken())
            .Concat(root.GetAnnotatedTokens(Lead.Gap)));

        var tokens = root.DescendantTokens()
            .Where(token => gapTokens.Contains(token) || token.LeadingTrivia.Any(IsGap));
        return root.ReplaceTokens(tokens,
            (original, _) => original.WithLeadingTrivia(BuildGappedTrivia(original, gapTokens)));
    }

    private static List<SyntaxTrivia> BuildGappedTrivia(SyntaxToken token, HashSet<SyntaxToken> gapTokens)
    {
        var previous = token.GetPreviousToken(includeZeroWidth: true);
        var opensScope = previous == default || previous.IsKind(SyntaxKind.OpenBraceToken);
        var trivia = new List<SyntaxTrivia>();

        foreach (var item in token.LeadingTrivia)
        {
            if (IsGap(item)) AddBlankLine(trivia, previous.TrailingTrivia.ToFullString(), opensScope);
            trivia.Add(item);
        }

        if (gapTokens.Contains(token)) AddBlankLine(trivia, previous.TrailingTrivia.ToFullString(), opensScope);
        return trivia;
    }

    private static void AddBlankLine(List<SyntaxTrivia> trivia, string previousLine, bool opensScope)
    {
        var lineStart = trivia.Count;
        while (lineStart > 0 && trivia[lineStart - 1].IsKind(SyntaxKind.WhitespaceTrivia)) lineStart--;

        var textBefore = string.Concat(trivia.Take(lineStart).Select(static item => item.ToFullString()));
        if (opensScope && textBefore.Trim().Length == 0) return;

        var squeezed = (previousLine + textBefore).Replace(" ", string.Empty).Replace(Indentation, string.Empty);
        if (squeezed.EndsWith(BlankLine)) return;

        trivia.Insert(lineStart, EndOfLine(LineBreak));
    }

    private static bool IsGap(SyntaxTrivia trivia)
    {
        return trivia.HasAnnotation(Lead.Gap) || trivia.GetStructure()?.HasAnnotation(Lead.Gap) is true;
    }
}

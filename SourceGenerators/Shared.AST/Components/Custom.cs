using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;

namespace Shared.Components;

internal static class Custom
{
    /// <summary>
    /// This line feed survives the formatter and preserves blank lines.
    /// </summary>
    public static SyntaxTrivia HardLineFeed { get; } = SyntaxFactory.Comment("\r");

    /// <summary>
    /// This carriage return survives the formatter and preserves blank lines.
    /// </summary>
    public static SyntaxTrivia HardCarriageReturn { get; } = SyntaxFactory.Comment("\n");
}

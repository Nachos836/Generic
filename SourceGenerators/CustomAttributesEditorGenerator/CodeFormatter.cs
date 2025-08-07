using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Formatting;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Formatting;

namespace CustomAttributesEditorGenerator;

internal static class CodeFormatter
{
    public static string PrettyPrint(this CompilationUnitSyntax compilationUnit)
    {
        using var workspace = new AdhocWorkspace();

        var options = workspace.Options
            .WithChangedOption(FormattingOptions.UseTabs, LanguageNames.CSharp, false)
            .WithChangedOption(FormattingOptions.TabSize, LanguageNames.CSharp, 4)
            .WithChangedOption(FormattingOptions.IndentationSize, LanguageNames.CSharp, 4)
            .WithChangedOption(CSharpFormattingOptions.NewLinesForBracesInMethods, true);

        var candidate = compilationUnit.WithAdditionalAnnotations(Formatter.Annotation);
        var formattedRoot = Formatter.Format(candidate, workspace, options);

        return formattedRoot.ToFullString();
    }
}

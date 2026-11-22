using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;

namespace Generator.InspectorAttributesEditor.Tests;

internal sealed record GeneratedFile(string HintName, string Text);

internal static class GeneratorRun
{
    public static (IReadOnlyList<GeneratedFile> Files, IReadOnlyList<Diagnostic> Diagnostics) Run(string source)
    {
        var platformAssemblies = (string)AppContext.GetData("TRUSTED_PLATFORM_ASSEMBLIES")!;

        var references = platformAssemblies
            .Split(Path.PathSeparator)
            .Concat(
            [
                typeof(ButtonDrawerGenerator).Assembly.Location,
                typeof(JetBrains.Annotations.UsedImplicitlyAttribute).Assembly.Location
            ])
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .Select(static path => MetadataReference.CreateFromFile(path));

        var compilation = CSharpCompilation.Create(
            assemblyName: "TestAssembly",
            syntaxTrees: [CSharpSyntaxTree.ParseText(source)],
            references: references,
            options: new(OutputKind.DynamicallyLinkedLibrary));

        var result = CSharpGeneratorDriver.Create(new ButtonDrawerGenerator())
            .RunGenerators(compilation)
            .GetRunResult()
            .Results.Single();

        var files = result.GeneratedSources
            .Select(static source => new GeneratedFile(source.HintName, source.SourceText.ToString()))
            .ToArray();

        return (files, result.Diagnostics);
    }
}

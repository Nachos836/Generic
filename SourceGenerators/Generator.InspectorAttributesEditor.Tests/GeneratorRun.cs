using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using NUnit.Framework;

namespace Generator.InspectorAttributesEditor.Tests;

internal sealed record GeneratedFile(string HintName, string Text);

internal static class GeneratorRun
{
    public static CSharpParseOptions EditorOptions { get; } =
        CSharpParseOptions.Default.WithPreprocessorSymbols("UNITY_EDITOR");

    public static (IReadOnlyList<GeneratedFile> Files, IReadOnlyList<Diagnostic> Diagnostics) Run(string source)
    {
        var compilation = CreateCompilation(source);
        var driver = CreateDriver(new ButtonDrawerGenerator())
            .RunGeneratorsAndUpdateCompilation(compilation, out var output, out _);

        var outputErrors = output.GetDiagnostics()
            .Where(static diagnostic => diagnostic.Severity == DiagnosticSeverity.Error)
            .ToArray();

        Assert.That(outputErrors, Is.Empty, string.Join(Environment.NewLine, outputErrors.AsEnumerable()));
        return CollectRun(driver);
    }

    public static GeneratorDriver CreateDriver(IIncrementalGenerator generator)
    {
        return CSharpGeneratorDriver.Create([generator.AsSourceGenerator()], parseOptions: EditorOptions);
    }

    public static (IReadOnlyList<GeneratedFile> Files, IReadOnlyList<Diagnostic> Diagnostics) CollectRun(
        GeneratorDriver driver)
    {
        var result = driver.GetRunResult().Results.Single();

        var files = result.GeneratedSources
            .Select(static source => new GeneratedFile(source.HintName, source.SourceText.ToString()))
            .ToArray();
        return (files, result.Diagnostics);
    }

    public static CSharpCompilation CreateCompilation(string source)
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

        return CSharpCompilation.Create(
            assemblyName: "TestAssembly",
            syntaxTrees: [CSharpSyntaxTree.ParseText(source, EditorOptions)],
            references: references,
            options: new(OutputKind.DynamicallyLinkedLibrary));
    }
}

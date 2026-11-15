using InspectorAttributes;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using NUnit.Framework;

namespace CustomAttributesEditorGenerator.Tests;

public sealed class Tests
{
    private IIncrementalGenerator _generator = default!;
    private CSharpGeneratorDriver _driver = default!;

    [SetUp]
    public void Setup()
    {
        _generator = new ButtonDrawerGenerator();
        _driver = CSharpGeneratorDriver.Create(_generator);
    }

    [Test]
    public void SimpleCase()
    {
        const string source = """
            using System.Diagnostics;
            using InspectorAttributes;
            using JetBrains.Annotations;
            using UnityEngine;
            
            namespace Generic.Samples
            {
                [ApplyCustomUIProcessing]
                internal sealed partial class BehaviourWithButtonAttribute : MonoBehaviour
                {
                    [Button("Cyka!"), UsedImplicitly, Conditional("UNITY_EDITOR")]
                    private void Test()
                    {
                        UnityEngine.Debug.Log("Test");
                    }
                }
            }
            
            """;
        var syntaxTree = CSharpSyntaxTree.ParseText(source);
        var platformAssemblies = AppContext.GetData("TRUSTED_PLATFORM_ASSEMBLIES") as string;
        if (string.IsNullOrEmpty(platformAssemblies))
            throw new InvalidOperationException("Runtime assembly paths are unavailable.");

        var references = platformAssemblies
            .Split(Path.PathSeparator)
            .Concat(
            [
                typeof(ButtonDrawerGenerator).Assembly.Location,
                typeof(UnityEngine.MonoBehaviour).Assembly.Location,
                typeof(ButtonAttribute).Assembly.Location,
                typeof(JetBrains.Annotations.UsedImplicitlyAttribute).Assembly.Location
            ])
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .Select(static path => MetadataReference.CreateFromFile(path));
        var compilation = CSharpCompilation.Create(
            assemblyName: "TestAssembly",
            syntaxTrees: [syntaxTree],
            references: references,
            options: new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary)
        );

        var inputErrors = compilation.GetDiagnostics()
            .Where(static diagnostic => diagnostic.Severity == DiagnosticSeverity.Error)
            .ToArray();

        Assert.That(inputErrors, Is.Empty, string.Join(Environment.NewLine, inputErrors.Select(static diagnostic => diagnostic.ToString())));

        _driver.RunGeneratorsAndUpdateCompilation(compilation, out var newCompilation, out var diagnostics);

        var generatedTrees = newCompilation.SyntaxTrees
            .Where(tree => tree != syntaxTree)
            .ToArray();

        if (diagnostics.IsEmpty is false)
        {
            foreach (var diagnostic in diagnostics)
            {
                TestContext.WriteLine(diagnostic.ToString());
            }
            return;
        }

        Assert.That(generatedTrees.Length == 0, Is.Not.True);
        foreach (var tree in generatedTrees)
        {
            TestContext.WriteLine(tree.FilePath);
            TestContext.WriteLine(tree.ToString());
            TestContext.WriteLine("<<<<<<<<<<<<<<<<<<<<<<<<<<<<<<<<<<<<<<<<<<<<<<<<<<<<<<<<<\n");
        }
    }
}

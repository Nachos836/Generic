using System.Diagnostics;
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
                    [Button(nameof(Test)), UsedImplicitly, Conditional("UNITY_EDITOR")]
                    private void Test()
                    {
                        UnityEngine.Debug.Log("Test");
                    }
                }
            }
            
            """;
        var syntaxTree = CSharpSyntaxTree.ParseText(source);
        var references = new[]
        {
            MetadataReference.CreateFromFile(typeof(object).Assembly.Location),
            MetadataReference.CreateFromFile(typeof(ConditionalAttribute).Assembly.Location),
            MetadataReference.CreateFromFile(typeof(UnityEngine.MonoBehaviour).Assembly.Location),
            MetadataReference.CreateFromFile(typeof(ButtonAttribute).Assembly.Location)
        };
        var compilation = CSharpCompilation.Create(
            assemblyName: "TestAssembly",
            syntaxTrees: [syntaxTree],
            references: references,
            options: new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary)
        );

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
            TestContext.WriteLine(tree.ToString());
            TestContext.WriteLine("<<<<<<<<<<<<<<<<<<<<<<<<<<<<<<<<<<<<<<<<<<<<<<<<<<<<<<<<<\n");
        }
    }
}

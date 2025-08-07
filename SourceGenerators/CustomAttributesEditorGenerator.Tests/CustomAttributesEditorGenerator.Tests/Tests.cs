using System.Diagnostics;
using InspectorAttributes;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using NUnit.Framework;

namespace CustomAttributesEditorGenerator.Tests;

public sealed class Tests
{
    private IIncrementalGenerator _generator;
    private CSharpGeneratorDriver _driver;

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
            using UnityEngine;

            namespace Generic.Samples
            {
                internal sealed partial class BehaviourWithButtonAttribute : MonoBehaviour
                {
                    [Button(nameof(Test)), Conditional("UNITY_EDITOR")]
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

        // 6. Извлекаем сгенерированные деревья (они идут после исходного)
        var generatedTrees = newCompilation.SyntaxTrees
            .Where(t => t != syntaxTree)
            .ToList();

        if (diagnostics.IsEmpty is false)
        {
            foreach (var diagnostic in diagnostics)
            {
                TestContext.WriteLine(diagnostic.ToString());
            }
            Assert.Fail();
        }

        // Проверяем, что что-то сгенерировалось
        Assert.That(generatedTrees, Is.Not.Empty);

        // Получаем текст первого сгенерированного файла
        var generatedCode = generatedTrees[0].ToString();

        TestContext.WriteLine(generatedCode);

        // Assert.That(generatedCode, Does.Contain("partial class"));
    }
}

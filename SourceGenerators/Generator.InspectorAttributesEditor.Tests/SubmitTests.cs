using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using NUnit.Framework;
using Shared.Generator;

namespace Generator.InspectorAttributesEditor.Tests;

public sealed class SubmitTests
{
    private const string PreviousVersionKept = "UCGEN002";
    private const string NothingEmitted = "UCGEN003";

    private const string Owner = """
        using InspectorAttributes;
        using UnityEngine;

        namespace Generic.Samples
        {
            internal sealed partial class Spawner : MonoBehaviour
            {
                [Button("Spawn")]
                private void Spawn() { }
            }
        }
        """;

    private const string EditorShadow = """

        namespace Generic.Samples
        {
            internal sealed class Editor { }
        }
        """;

    [Test]
    public void FailedCheckKeepsPreviousVersion()
    {
        var generator = new ButtonDrawerGenerator();
        var driver = GeneratorRun.CreateDriver(generator)
            .RunGenerators(GeneratorRun.CreateCompilation(Owner));
        var (previous, _) = GeneratorRun.CollectRun(driver);

        var (files, diagnostics) = GeneratorRun.CollectRun(
            driver.RunGenerators(GeneratorRun.CreateCompilation(Owner + EditorShadow)));

        Assert.That(previous, Has.Count.EqualTo(2));
        Assert.That(files, Is.EqualTo(previous));
        Assert.That(diagnostics.Select(static diagnostic => diagnostic.Id), Is.EqualTo(new[] { PreviousVersionKept }));
        Assert.That(diagnostics.Single().Severity, Is.EqualTo(DiagnosticSeverity.Warning));
    }

    [Test]
    public void FailedCheckWithoutPreviousVersionEmitsNothing()
    {
        var driver = GeneratorRun.CreateDriver(new ButtonDrawerGenerator())
            .RunGenerators(GeneratorRun.CreateCompilation(Owner + EditorShadow));
        var (files, diagnostics) = GeneratorRun.CollectRun(driver);

        Assert.That(files, Is.Empty);
        Assert.That(diagnostics.Select(static diagnostic => diagnostic.Id), Is.EqualTo(new[] { NothingEmitted }));
        Assert.That(diagnostics.Single().Severity, Is.EqualTo(DiagnosticSeverity.Error));
    }

    [Test]
    public void PassedCheckEmitsSessionFiles()
    {
        var result = new Session(new(), "Owner").Add("A.g.cs", "internal static class A { }")
            .Submit(GeneratorRun.CreateCompilation(""), CancellationToken.None);

        Assert.That(result.Files.Select(static file => file.HintName), Is.EqualTo(new[] { "A.g.cs" }));
        Assert.That(result.Diagnostic, Is.Null);
    }

    [Test]
    public void CheckCompilesSessionFilesTogetherWithUserCode()
    {
        var compilation = GeneratorRun.CreateCompilation("internal static partial class Shared { }");

        var result = new Session(new(), "Owner")
            .Add("A.g.cs", "internal static partial class Shared { public static int Value => B.Count; }")
            .Add("B.g.cs", "internal static class B { public const int Count = 1; }")
            .Submit(compilation, CancellationToken.None);

        Assert.That(result.Files, Has.Length.EqualTo(2));
        Assert.That(result.Diagnostic, Is.Null);
    }

    [Test]
    public void CheckIgnoresErrorsInUserCode()
    {
        var result = new Session(new(), "Owner").Add("A.g.cs", "internal static class A { }")
            .Submit(GeneratorRun.CreateCompilation("internal class Broken : Missing { }"), CancellationToken.None);

        Assert.That(result.Files, Has.Length.EqualTo(1));
        Assert.That(result.Diagnostic, Is.Null);
    }

    [Test]
    public void FailedCheckReportsTheFirstError()
    {
        var memory = new SessionMemory();
        new Session(memory, "Owner").Add("A.g.cs", "internal static class A { }")
            .Submit(GeneratorRun.CreateCompilation(""), CancellationToken.None);

        var result = new Session(memory, "Owner").Add("A.g.cs", "internal class A { private Missing _m; }")
            .Submit(GeneratorRun.CreateCompilation(""), CancellationToken.None);

        Assert.That(result.Files.Single().Text, Is.EqualTo("internal static class A { }"));
        Assert.That(result.Diagnostic!.GetMessage(), Does.Contain("Owner").And.Contain("CS0246"));
    }

    [Test]
    public void MemoryKeepsOwnersApart()
    {
        var memory = new SessionMemory();
        new Session(memory, "First").Add("A.g.cs", "internal static class A { }")
            .Submit(GeneratorRun.CreateCompilation(""), CancellationToken.None);

        var result = new Session(memory, "Second").Add("B.g.cs", "internal class B { private Missing _m; }")
            .Submit(GeneratorRun.CreateCompilation(""), CancellationToken.None);

        Assert.That(result.Files, Is.Empty);
        Assert.That(result.Diagnostic!.Id, Is.EqualTo(NothingEmitted));
    }
}

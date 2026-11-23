using Microsoft.CodeAnalysis;
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
        var driver = GeneratorRun.CreateDriver(new ButtonDrawerGenerator())
            .RunGenerators(GeneratorRun.CreateCompilation(Owner));
        var (previous, _) = GeneratorRun.CollectRun(driver);

        var renamed = Owner.Replace("void Spawn()", "void Fire()") + EditorShadow;
        var (files, diagnostics) = GeneratorRun.CollectRun(
            driver.RunGenerators(GeneratorRun.CreateCompilation(renamed)));

        Assert.That(previous, Has.Count.EqualTo(2));
        Assert.That(files, Is.EqualTo(previous));
        Assert.That(diagnostics.Select(static diagnostic => diagnostic.Id), Is.EqualTo(new[] { PreviousVersionKept }));
        Assert.That(diagnostics.Single().Severity, Is.EqualTo(DiagnosticSeverity.Warning));
    }

    [Test]
    public void RemovedOwnerLeavesNoPreviousVersion()
    {
        var driver = GeneratorRun.CreateDriver(new ButtonDrawerGenerator())
            .RunGenerators(GeneratorRun.CreateCompilation(Owner))
            .RunGenerators(GeneratorRun.CreateCompilation(Owner.Replace("[Button(\"Spawn\")]", "")));

        var (files, diagnostics) = GeneratorRun.CollectRun(
            driver.RunGenerators(GeneratorRun.CreateCompilation(Owner + EditorShadow)));

        Assert.That(files, Is.Empty);
        Assert.That(diagnostics.Select(static diagnostic => diagnostic.Id), Is.EqualTo(new[] { NothingEmitted }));
    }

    [Test]
    public void CheckFailsOnErrorsTheSessionCausesInUserCode()
    {
        var clash = Owner.Replace("private void Spawn() { }", "private void Spawn() { }\n        private int Drawer;");
        var driver = GeneratorRun.CreateDriver(new ButtonDrawerGenerator())
            .RunGenerators(GeneratorRun.CreateCompilation(clash));
        var (files, diagnostics) = GeneratorRun.CollectRun(driver);

        Assert.That(files, Is.Empty);
        Assert.That(diagnostics.Single().Id, Is.EqualTo(NothingEmitted));
        Assert.That(diagnostics.Single().GetMessage(), Does.Contain("CS0102"));
        Assert.That(diagnostics.Single().Location.SourceTree?.FilePath, Is.Empty);
        Assert.That(diagnostics.Single().Location.IsInSource, Is.True);
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
        var result = Submit(new(), "Owner", "", ("A.g.cs", "internal static class A { }"));

        Assert.That(result.Files.Select(static file => file.HintName), Is.EqualTo(new[] { "A.g.cs" }));
        Assert.That(result.Diagnostic, Is.Null);
    }

    [Test]
    public void CheckCompilesSessionFilesTogetherWithUserCode()
    {
        var result = Submit(new(), "Owner", "internal static partial class Shared { }",
            ("A.g.cs", "internal static partial class Shared { public static int Value => B.Count; }"),
            ("B.g.cs", "internal static class B { public const int Count = 1; }"));

        Assert.That(result.Files, Has.Length.EqualTo(2));
        Assert.That(result.Diagnostic, Is.Null);
    }

    [Test]
    public void CheckIgnoresErrorsAlreadyInUserCode()
    {
        var result = Submit(new(), "Owner", "internal partial class A : Missing { }",
            ("A.g.cs", "internal partial class A { }"));

        Assert.That(result.Files, Has.Length.EqualTo(1));
        Assert.That(result.Diagnostic, Is.Null);
    }

    [Test]
    public void CheckUsesTheGivenParseOptions()
    {
        var compilation = GeneratorRun.CreateCompilation("");
        var file = "#if UNITY_EDITOR\ninternal class A { private Missing _m; }\n#endif";

        var plain = new Session(new(), "Owner").Add("A.g.cs", file)
            .Submit(compilation, Microsoft.CodeAnalysis.CSharp.CSharpParseOptions.Default, CancellationToken.None);
        var editor = new Session(new(), "Owner").Add("A.g.cs", file)
            .Submit(compilation, GeneratorRun.EditorOptions, CancellationToken.None);

        Assert.That(plain.Diagnostic, Is.Null);
        Assert.That(editor.Diagnostic!.Id, Is.EqualTo(NothingEmitted));
    }

    [Test]
    public void FailedCheckReportsTheFirstError()
    {
        var memory = new SessionMemory();
        Submit(memory, "Owner", "", ("A.g.cs", "internal static class A { }"));

        var result = Submit(memory, "Owner", "", ("A.g.cs", "internal class A { private Missing _m; }"));

        Assert.That(result.Files.Single().Text, Is.EqualTo("internal static class A { }"));
        Assert.That(result.Diagnostic!.GetMessage(), Does.Contain("Owner").And.Contain("CS0246"));
    }

    [Test]
    public void FailedChecksKeepTheLastGoodVersion()
    {
        var memory = new SessionMemory();
        Submit(memory, "Owner", "", ("A.g.cs", "internal static class A { }"));
        Submit(memory, "Owner", "", ("A.g.cs", "internal class A { private Missing _m; }"));

        var result = Submit(memory, "Owner", "", ("A.g.cs", "internal class A { private Absent _m; }"));

        Assert.That(result.Files.Single().Text, Is.EqualTo("internal static class A { }"));
    }

    [Test]
    public void MemoryKeepsOwnersApart()
    {
        var memory = new SessionMemory();
        Submit(memory, "First", "", ("A.g.cs", "internal static class A { }"));

        var result = Submit(memory, "Second", "", ("B.g.cs", "internal class B { private Missing _m; }"));

        Assert.That(result.Files, Is.Empty);
        Assert.That(result.Diagnostic!.Id, Is.EqualTo(NothingEmitted));
    }

    [Test]
    public void MemoryKeepsAssembliesApart()
    {
        var memory = new SessionMemory();
        new Session(memory, "Owner").Add("A.g.cs", "internal static class A { }")
            .Submit(GeneratorRun.CreateCompilation("").WithAssemblyName("First"), GeneratorRun.EditorOptions,
                CancellationToken.None);

        var result = new Session(memory, "Owner").Add("A.g.cs", "internal class A { private Missing _m; }")
            .Submit(GeneratorRun.CreateCompilation("").WithAssemblyName("Second"), GeneratorRun.EditorOptions,
                CancellationToken.None);

        Assert.That(result.Files, Is.Empty);
    }

    [TestCase("A.g.cs", "a.G.cs")]
    [TestCase("A", "A.cs")]
    [TestCase("A.g.cs", "bad<name>.cs")]
    public void InvalidOrDuplicateFileNamesFailTheCheck(string first, string second)
    {
        var result = Submit(new(), "Owner", "", (first, "internal static class A { }"),
            (second, "internal static class B { }"));

        Assert.That(result.Files, Is.Empty);
        Assert.That(result.Diagnostic!.GetMessage(), Does.Contain("file name"));
    }

    [Test]
    public void CancelledSubmitStops()
    {
        var session = new Session(new(), "Owner").Add("A.g.cs", "internal static class A { }");
        Assert.Throws<OperationCanceledException>(() => session.Submit(GeneratorRun.CreateCompilation(""),
            GeneratorRun.EditorOptions, new CancellationToken(true)));
    }

    private static SubmitResult Submit(SessionMemory memory, string owner, string userCode,
        params (string HintName, string Text)[] files)
    {
        var session = new Session(memory, owner);

        foreach (var (hintName, text) in files)
        {
            session.Add(hintName, text);
        }

        return session.Submit(GeneratorRun.CreateCompilation(userCode), GeneratorRun.EditorOptions,
            CancellationToken.None);
    }
}

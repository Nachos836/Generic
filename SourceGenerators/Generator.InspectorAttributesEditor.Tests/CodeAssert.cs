using System.Text.RegularExpressions;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using NUnit.Framework;

namespace Generator.InspectorAttributesEditor.Tests;

internal static class CodeAssert
{
    private static readonly Regex LineBreak = new("\r\n|\r|\n", RegexOptions.Compiled);

    public static void AreEqual(string expected, string actual)
    {
        Assert.That(Normalize(actual), Is.EqualTo(Normalize(expected)));
    }

    public static void Parses(string code)
    {
        var errors = CSharpSyntaxTree.ParseText(code).GetDiagnostics()
            .Where(static diagnostic => diagnostic.Severity == DiagnosticSeverity.Error);
        Assert.That(errors, Is.Empty, code);
    }

    private static string Normalize(string text)
    {
        var lines = LineBreak.Split(text).Select(static line => line.TrimEnd());
        return string.Join("\n", lines).Trim('\n');
    }
}
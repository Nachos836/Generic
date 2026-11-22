using System.Runtime.CompilerServices;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using NUnit.Framework;

namespace Generator.InspectorAttributesEditor.Tests;

internal static class InventorySample
{
    private const char ByteOrderMark = '﻿';

    public static string Read(string fileName)
    {
        var repositoryRoot = Path.GetFullPath(Path.Combine(FindSourceDirectory(), "..", ".."));
        var path = Path.Combine(repositoryRoot, "Assets", "Samples", "GameplayInventory", "Generated", fileName);
        return File.ReadAllText(path).TrimStart(ByteOrderMark);
    }

    public static void AssertExpresses(string sample, string generated)
    {
        var sampleTree = CSharpSyntaxTree.ParseText(sample);
        var generatedTree = CSharpSyntaxTree.ParseText(generated);
        var errors = generatedTree.GetDiagnostics().Where(static item => item.Severity == DiagnosticSeverity.Error);

        Assert.That(errors, Is.Empty, generated);
        Assert.That(Squeeze(generated), Is.EqualTo(Squeeze(sample)), generated);
        Assert.That(generatedTree.GetRoot().IsEquivalentTo(sampleTree.GetRoot(), topLevel: false), Is.True, generated);
    }

    private static string FindSourceDirectory([CallerFilePath] string path = "") => Path.GetDirectoryName(path)!;

    private static string Squeeze(string text)
    {
        return new(text.Where(static character => char.IsWhiteSpace(character) is false).ToArray());
    }
}
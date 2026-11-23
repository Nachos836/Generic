using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Globalization;
using System.Linq;
using System.Threading;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Text;

namespace Shared.Generator;

internal sealed class SessionFile
{
    internal SessionFile(string hintName, string text)
    {
        HintName = hintName;
        Text = text;
    }

    public string HintName { get; }
    public string Text { get; }
}

internal sealed class SubmitResult
{
    internal SubmitResult(ImmutableArray<SessionFile> files, Diagnostic? diagnostic)
    {
        Files = files;
        Diagnostic = diagnostic;
    }

    public ImmutableArray<SessionFile> Files { get; }
    public Diagnostic? Diagnostic { get; }
}

internal sealed class SessionMemory
{
    private const char Separator = '\0';

    private readonly ConcurrentDictionary<string, ImmutableArray<SessionFile>> _lastGood = new(StringComparer.Ordinal);

    public void Retain(Compilation compilation, IEnumerable<string> owners)
    {
        var assembly = compilation.AssemblyName + Separator;
        var kept = new HashSet<string>(owners.Select(owner => assembly + owner), StringComparer.Ordinal);

        foreach (var key in _lastGood.Keys)
        {
            if (key.StartsWith(assembly, StringComparison.Ordinal) && kept.Contains(key) is false)
            {
                _lastGood.TryRemove(key, out _);
            }
        }
    }

    internal bool TryRecall(Compilation compilation, string owner, out ImmutableArray<SessionFile> files)
    {
        return _lastGood.TryGetValue(compilation.AssemblyName + Separator + owner, out files);
    }

    internal void Remember(Compilation compilation, string owner, ImmutableArray<SessionFile> files)
    {
        _lastGood[compilation.AssemblyName + Separator + owner] = files;
    }
}

internal sealed class Session
{
    private const string SourceExtension = ".cs";

    private static readonly char[] InvalidHintNameCharacters = ['<', '>', ':', '"', '|', '?', '*', '/', '\\'];

    private readonly List<SessionFile> _files = [];
    private readonly SessionMemory _memory = default!;
    private readonly string _owner = default!;

    internal Session(SessionMemory memory, string owner)
    {
        _memory = memory;
        _owner = owner;
    }

    public Session Add(string hintName, string text)
    {
        _files.Add(new(hintName, text));
        return this;
    }

    public SubmitResult Submit(Compilation compilation, ParseOptions options, CancellationToken cancellationToken)
    {
        var files = _files.ToImmutableArray();
        var trees = files.Select(file => CSharpSyntaxTree.ParseText(file.Text, options as CSharpParseOptions,
            file.HintName, cancellationToken: cancellationToken)).ToImmutableArray();

        var checkedCompilation = compilation.AddSyntaxTrees(trees);
        var ownerLocations = FindOwnerLocations(checkedCompilation, trees, cancellationToken);
        var failure = FindHintNameFailure(files)
            ?? FindCompileFailure(compilation, checkedCompilation, trees, ownerLocations, cancellationToken);

        if (failure == default)
        {
            _memory.Remember(compilation, _owner, files);
            return new(files, null);
        }

        var location = ownerLocations.FirstOrDefault() ?? Location.None;
        return _memory.TryRecall(compilation, _owner, out var previous)
            ? new(previous, Diagnostic.Create(Diagnostics.PreviousVersionKeptDescriptor, location, _owner, failure))
            : new([], Diagnostic.Create(Diagnostics.NothingEmittedDescriptor, location, _owner, failure));
    }

    private static string? FindHintNameFailure(ImmutableArray<SessionFile> files)
    {
        var names = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        foreach (var file in files)
        {
            var name = file.HintName.EndsWith(SourceExtension, StringComparison.OrdinalIgnoreCase)
                ? file.HintName
                : file.HintName + SourceExtension;

            if (file.HintName.IndexOfAny(InvalidHintNameCharacters) >= 0) return $"invalid file name '{file.HintName}'";
            if (names.Add(name) is false) return $"duplicate file name '{file.HintName}'";
        }

        return null;
    }

    private static string? FindCompileFailure(Compilation compilation, Compilation checkedCompilation,
        ImmutableArray<SyntaxTree> trees, ImmutableArray<Location> ownerLocations,
        CancellationToken cancellationToken)
    {
        var sessionError = trees.SelectMany(tree => checkedCompilation.GetSemanticModel(tree)
                .GetDiagnostics(cancellationToken: cancellationToken))
            .FirstOrDefault(static diagnostic => diagnostic.Severity == DiagnosticSeverity.Error);

        var error = sessionError ?? ownerLocations.Select(static location => location.SourceTree!).Distinct()
            .SelectMany(tree => FindNewDeclarationErrors(compilation, checkedCompilation, tree, cancellationToken))
            .FirstOrDefault();

        return error == default ? null : $"{error.Id}: {error.GetMessage(CultureInfo.InvariantCulture)}";
    }

    private static ImmutableArray<Location> FindOwnerLocations(Compilation checkedCompilation,
        ImmutableArray<SyntaxTree> trees, CancellationToken cancellationToken)
    {
        return trees.SelectMany(tree => tree.GetRoot(cancellationToken).DescendantNodes()
                .OfType<BaseTypeDeclarationSyntax>()
                .Select(declaration => checkedCompilation.GetSemanticModel(tree)
                    .GetDeclaredSymbol(declaration, cancellationToken)))
            .SelectMany(static symbol => symbol?.Locations ?? [])
            .Where(location => location.SourceTree is { } tree && trees.Contains(tree) is false)
            .ToImmutableArray();
    }

    private static IEnumerable<Diagnostic> FindNewDeclarationErrors(Compilation compilation,
        Compilation checkedCompilation, SyntaxTree tree, CancellationToken cancellationToken)
    {
        var existing = new HashSet<(string, TextSpan)>(compilation.GetSemanticModel(tree)
            .GetDeclarationDiagnostics(cancellationToken: cancellationToken)
            .Select(static diagnostic => (diagnostic.Id, diagnostic.Location.SourceSpan)));

        return checkedCompilation.GetSemanticModel(tree).GetDeclarationDiagnostics(cancellationToken: cancellationToken)
            .Where(diagnostic => diagnostic.Severity == DiagnosticSeverity.Error
                && existing.Contains((diagnostic.Id, diagnostic.Location.SourceSpan)) is false);
    }
}

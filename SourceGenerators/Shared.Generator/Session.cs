using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Globalization;
using System.Linq;
using System.Threading;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;

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
    private readonly ConcurrentDictionary<string, ImmutableArray<SessionFile>> _lastGood = new(StringComparer.Ordinal);

    internal bool TryRecall(string owner, out ImmutableArray<SessionFile> files)
    {
        return _lastGood.TryGetValue(owner, out files);
    }

    internal void Remember(string owner, ImmutableArray<SessionFile> files) => _lastGood[owner] = files;
}

internal sealed class Session
{
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

    public SubmitResult Submit(Compilation compilation, CancellationToken cancellationToken)
    {
        var files = _files.ToImmutableArray();
        var error = FindFirstError(compilation, files, cancellationToken);

        if (error == default)
        {
            _memory.Remember(_owner, files);
            return new(files, null);
        }

        var reason = $"{error.Id}: {error.GetMessage(CultureInfo.InvariantCulture)}";
        return _memory.TryRecall(_owner, out var previous)
            ? new(previous, Diagnostic.Create(Diagnostics.PreviousVersionKeptDescriptor, Location.None, _owner, reason))
            : new([], Diagnostic.Create(Diagnostics.NothingEmittedDescriptor, Location.None, _owner, reason));
    }

    private static Diagnostic? FindFirstError(Compilation compilation, ImmutableArray<SessionFile> files,
        CancellationToken cancellationToken)
    {
        var options = compilation.SyntaxTrees.FirstOrDefault()?.Options as CSharpParseOptions;

        var trees = files.Select(file => CSharpSyntaxTree.ParseText(file.Text, options, file.HintName,
            cancellationToken: cancellationToken)).ToArray();

        var checkedCompilation = compilation.AddSyntaxTrees(trees);
        return trees.SelectMany(tree => checkedCompilation.GetSemanticModel(tree).GetDiagnostics(
                cancellationToken: cancellationToken))
            .FirstOrDefault(static diagnostic => diagnostic.Severity == DiagnosticSeverity.Error);
    }
}

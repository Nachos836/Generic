using System;
using System.Collections.Generic;
using Microsoft.CodeAnalysis.CSharp;
using Shared.CodeGeneration.Scoped.Rendering;
using static Microsoft.CodeAnalysis.CSharp.SyntaxFactory;

namespace Shared.CodeGeneration.Scoped;

internal abstract class Scope<TNext> where TNext : class
{
    private protected Scope(Lead lead) => Lead = lead;

    private protected Lead Lead { get; }

    private protected abstract TNext Next { get; }

    public TNext Comment(string text, bool when = true)
    {
        if (when) Lead.AddTrivia(SyntaxFactory.Comment(text.Length == 0 ? "//" : "// " + text));
        return Next;
    }

    public TNext DocComment(IReadOnlyList<string> lines, bool when = true)
    {
        if (when is false) return Next;

        foreach (var line in lines)
        {
            Lead.AddTrivia(SyntaxFactory.Comment(line.Length == 0 ? "///" : "/// " + line));
        }

        return Next;
    }

    public TNext Gap(bool when = true)
    {
        if (when) Lead.MarkGap();
        return Next;
    }

    public TNext IfDirective(string symbol, bool when = true) => IfDirective(new Expr(IdentifierName(symbol)), when);

    public TNext IfDirective(Expr condition, bool when = true)
    {
        if (when && condition.Node is { } node)
        {
            Lead.AddTrivia(Trivia(IfDirectiveTrivia(node, isActive: true, branchTaken: true, conditionValue: true)));
        }

        return Next;
    }

    public TNext ElseDirective(bool when = true)
    {
        if (when) Lead.AddTrivia(Trivia(ElseDirectiveTrivia(isActive: true, branchTaken: false)));
        return Next;
    }

    public TNext EndIfDirective(bool when = true)
    {
        if (when) Lead.AddTrivia(Trivia(EndIfDirectiveTrivia(isActive: true)));
        return Next;
    }

    public TNext ForEach<TItem>(IEnumerable<TItem> items, Action<TNext, TItem> block, bool when = true)
    {
        if (when is false) return Next;

        foreach (var item in items)
        {
            block(Next, item);
        }

        return Next;
    }
}
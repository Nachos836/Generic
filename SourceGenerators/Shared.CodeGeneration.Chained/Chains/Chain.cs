using System;
using System.Collections.Generic;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Shared.CodeGeneration.Chained.Rendering;
using static Microsoft.CodeAnalysis.CSharp.SyntaxFactory;

namespace Shared.CodeGeneration.Chained;

internal interface IMemberHost
{
    void AddMember(MemberDeclarationSyntax member);
}

internal interface IStatementHost
{
    void AddStatement(StatementSyntax statement);
}

internal sealed class Opening
{
    internal Opening(Leading leading, SyntaxList<AttributeListSyntax> attributes, SyntaxTokenList modifiers,
        NameSyntax? explicitInterface)
    {
        Leading = leading;
        Attributes = attributes;
        Modifiers = modifiers;
        ExplicitInterface = explicitInterface;
    }

    public Leading Leading { get; }
    public SyntaxList<AttributeListSyntax> Attributes { get; }
    public SyntaxTokenList Modifiers { get; }
    public NameSyntax? ExplicitInterface { get; }
}

internal abstract class Chain<TSelf> where TSelf : Chain<TSelf>
{
    private protected Chain(Lead lead) => Lead = lead;

    private protected Lead Lead { get; }
    private protected TSelf Self => (TSelf)this;

    public TSelf Comment(string text, bool when = true)
    {
        if (when) Lead.AddTrivia(SyntaxFactory.Comment(text.Length == 0 ? "//" : "// " + text));
        return Self;
    }

    public TSelf DocComment(IReadOnlyList<string> lines, bool when = true)
    {
        if (when is false) return Self;

        foreach (var line in lines)
        {
            Lead.AddTrivia(SyntaxFactory.Comment(line.Length == 0 ? "///" : "/// " + line));
        }

        return Self;
    }

    public TSelf Gap(bool when = true)
    {
        if (when) Lead.MarkGap();
        return Self;
    }

    public TSelf IfDirective(string symbol, bool when = true) => IfDirective(new Expr(IdentifierName(symbol)), when);

    public TSelf IfDirective(Expr condition, bool when = true)
    {
        if (when && condition.Node is { } node)
        {
            Lead.AddTrivia(Trivia(IfDirectiveTrivia(node, isActive: true, branchTaken: true, conditionValue: true)));
        }

        return Self;
    }

    public TSelf ElseDirective(bool when = true)
    {
        if (when) Lead.AddTrivia(Trivia(ElseDirectiveTrivia(isActive: true, branchTaken: false)));
        return Self;
    }

    public TSelf EndIfDirective(bool when = true)
    {
        if (when) Lead.AddTrivia(Trivia(EndIfDirectiveTrivia(isActive: true)));
        return Self;
    }

    public TSelf ForEach<TItem>(IEnumerable<TItem> items, Func<TSelf, TItem, TSelf> block, bool when = true)
    {
        if (when is false) return Self;

        var chain = Self;
        foreach (var item in items)
        {
            chain = block(chain, item);
        }

        return chain;
    }

    private protected Opening Open()
    {
        return new(Lead.TakeLeading(), Lead.TakeAttributes(), Lead.TakeModifiers(), Lead.TakeExplicitInterface());
    }

    private protected TSelf AddModifier(SyntaxKind keyword, bool when)
    {
        if (when) Lead.AddModifier(keyword);
        return Self;
    }

    private protected TSelf DropPrefixes()
    {
        Lead.DropPrefixes();
        return Self;
    }
}
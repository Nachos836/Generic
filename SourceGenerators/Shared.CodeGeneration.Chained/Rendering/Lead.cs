using System.Collections.Generic;
using System.Linq;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using static Microsoft.CodeAnalysis.CSharp.SyntaxFactory;

namespace Shared.CodeGeneration.Chained.Rendering;

internal sealed class Lead
{
    private readonly List<SyntaxTrivia> _trivia = [];
    private readonly List<AttributeListSyntax> _attributeLists = [];
    private readonly List<AttributeSyntax> _attributes = [];
    private readonly List<SyntaxToken> _modifiers = [];
    private NameSyntax? _explicitInterface = default;
    private bool _gap = false;

    public static SyntaxAnnotation Gap { get; } = new("Shared.CodeGeneration.Chained.Gap");
    public bool HasPending => _trivia.Count > 0 || _gap;

    public void AddTrivia(SyntaxTrivia trivia)
    {
        if (_gap)
        {
            trivia = trivia.GetStructure() is StructuredTriviaSyntax structure
                ? Trivia(structure.WithAdditionalAnnotations(Gap))
                : trivia.WithAdditionalAnnotations(Gap);

            _gap = false;
        }

        _trivia.Add(trivia);
    }

    public void MarkGap() => _gap = true;
    public void AddAttribute(AttributeSyntax attribute) => _attributes.Add(attribute);

    public void SplitAttributes()
    {
        if (_attributes.Count == 0) return;

        _attributeLists.Add(AttributeList(SeparatedList(_attributes)));
        _attributes.Clear();
    }

    public void AddModifier(SyntaxKind keyword) => _modifiers.Add(Token(keyword));
    public void SetExplicitInterface(NameSyntax name) => _explicitInterface = name;

    public SyntaxList<AttributeListSyntax> TakeAttributes()
    {
        SplitAttributes();
        var lists = List(_attributeLists);
        _attributeLists.Clear();
        return lists;
    }

    public SyntaxTokenList TakeModifiers()
    {
        var modifiers = TokenList(_modifiers);
        _modifiers.Clear();
        return modifiers;
    }

    public NameSyntax? TakeExplicitInterface()
    {
        var name = _explicitInterface;
        _explicitInterface = default;
        return name;
    }

    public Leading TakeLeading()
    {
        var leading = new Leading(_trivia.ToArray(), _gap);
        _trivia.Clear();
        _gap = false;
        return leading;
    }

    public void DropPrefixes()
    {
        _attributes.Clear();
        _attributeLists.Clear();
        _modifiers.Clear();
        _explicitInterface = default;
    }

    public TNode Attach<TNode>(TNode node) where TNode : SyntaxNode => TakeLeading().Apply(node);

    public SyntaxToken Close(SyntaxToken token)
    {
        token = token.WithLeadingTrivia(_trivia.Concat(token.LeadingTrivia));
        if (_gap) token = token.WithAdditionalAnnotations(Gap);

        _trivia.Clear();
        _gap = false;
        DropPrefixes();
        return token;
    }
}

internal sealed class Leading
{
    private readonly IReadOnlyList<SyntaxTrivia> _trivia = default!;
    private readonly bool _gap;

    internal Leading(IReadOnlyList<SyntaxTrivia> trivia, bool gap)
    {
        _trivia = trivia;
        _gap = gap;
    }

    public TNode Apply<TNode>(TNode node) where TNode : SyntaxNode
    {
        if (_trivia.Count > 0) node = node.WithLeadingTrivia(_trivia.Concat(node.GetLeadingTrivia()));
        return _gap ? node.WithAdditionalAnnotations(Lead.Gap) : node;
    }
}
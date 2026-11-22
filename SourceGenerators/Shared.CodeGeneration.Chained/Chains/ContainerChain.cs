using System;
using System.Collections.Generic;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Shared.CodeGeneration.Chained.Rendering;
using static Microsoft.CodeAnalysis.CSharp.SyntaxFactory;

namespace Shared.CodeGeneration.Chained;

internal abstract class ContainerChain<TSelf> : DeclarationChain<TSelf> where TSelf : ContainerChain<TSelf>
{
    private protected List<UsingDirectiveSyntax> Usings { get; } = [];
    private protected List<MemberDeclarationSyntax> Members { get; } = [];

    public TSelf Using(string name, bool when = true)
    {
        if (when) Usings.Add(Lead.Attach(UsingDirective(Names.Qualify(name))));
        return DropPrefixes();
    }

    public TSelf UsingStatic(string name, bool when = true)
    {
        if (when) Usings.Add(Lead.Attach(UsingDirective(Token(SyntaxKind.StaticKeyword), null, Names.Qualify(name))));
        return DropPrefixes();
    }

    public NamespaceChain<TSelf> Namespace(string name, bool when = true)
    {
        var opening = Open();
        return new(Self, opening.Leading, NamespaceDeclaration(Names.Qualify(name)), when);
    }

    private protected override void Collect(MemberDeclarationSyntax member) => Members.Add(member);
}

internal sealed class FileChain : ContainerChain<FileChain>
{
    private const string DefineAfterCode = "A #define must precede the first using directive and declaration.";

    private readonly bool _active;

    internal FileChain(bool active) => _active = active;

    public FileChain Nullable(bool when = true)
    {
        if (when) Lead.AddTrivia(Trivia(NullableDirectiveTrivia(Token(SyntaxKind.EnableKeyword), isActive: true)));
        return this;
    }

    public FileChain Define(string symbol, bool when = true)
    {
        if (when is false) return this;
        if (Usings.Count > 0 || Members.Count > 0) throw new InvalidOperationException(DefineAfterCode);

        Lead.AddTrivia(Trivia(DefineDirectiveTrivia(symbol, isActive: true)));
        return this;
    }

    public string EndFile()
    {
        if (_active is false) return string.Empty;

        var unit = CompilationUnit()
            .WithUsings(List(Usings))
            .WithMembers(List(Members))
            .WithEndOfFileToken(Lead.Close(Token(SyntaxKind.EndOfFileToken)));
        return Renderer.Render(unit);
    }
}

internal sealed class NamespaceChain<TOuter> : ContainerChain<NamespaceChain<TOuter>>
    where TOuter : class, IMemberHost
{
    private readonly TOuter _outer = default!;
    private readonly Leading _leading = default!;
    private readonly NamespaceDeclarationSyntax _declaration = default!;
    private readonly bool _active;

    internal NamespaceChain(TOuter outer, Leading leading, NamespaceDeclarationSyntax declaration, bool active)
    {
        _outer = outer;
        _leading = leading;
        _declaration = declaration;
        _active = active;
    }

    public TOuter EndNamespace()
    {
        var declaration = _declaration
            .WithUsings(List(Usings))
            .WithMembers(List(Members))
            .WithCloseBraceToken(Lead.Close(Token(SyntaxKind.CloseBraceToken)));

        if (_active) _outer.AddMember(_leading.Apply(declaration));
        return _outer;
    }
}
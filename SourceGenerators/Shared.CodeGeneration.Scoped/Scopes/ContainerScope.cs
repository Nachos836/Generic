using System;
using System.Collections.Generic;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Shared.CodeGeneration.Scoped.Rendering;
using static Microsoft.CodeAnalysis.CSharp.SyntaxFactory;

namespace Shared.CodeGeneration.Scoped;

internal abstract class ContainerScope<TNext> : DeclarationScope<TNext> where TNext : class
{
    private protected ContainerScope() : base(new())
    {
    }

    private protected List<UsingDirectiveSyntax> Usings { get; } = [];

    private protected List<MemberDeclarationSyntax> Members { get; } = [];

    public TNext Using(string name, bool when = true)
    {
        if (when) Usings.Add(Lead.Attach(UsingDirective(Names.Qualified(name))));
        return Skip();
    }

    public TNext UsingStatic(string name, bool when = true)
    {
        if (when) Usings.Add(Lead.Attach(UsingDirective(Token(SyntaxKind.StaticKeyword), null, Names.Qualified(name))));
        return Skip();
    }

    public TNext Namespace(string name, Action<NamespaceScope> body, bool when = true)
    {
        if (when is false) return Skip();

        var scope = new NamespaceScope();
        body(scope);
        return AddMember(scope.Complete(NamespaceDeclaration(Names.Qualified(name))));
    }

    private protected override void Add(MemberDeclarationSyntax member) => Members.Add(member);
}

internal sealed class FileScope : ContainerScope<FileScope>
{
    private protected override FileScope Next => this;

    public FileScope Nullable(bool when = true)
    {
        if (when) Lead.AddTrivia(Trivia(NullableDirectiveTrivia(Token(SyntaxKind.EnableKeyword), isActive: true)));
        return this;
    }

    public FileScope Define(string symbol, bool when = true)
    {
        if (when) Lead.AddTrivia(Trivia(DefineDirectiveTrivia(symbol, isActive: true)));
        return this;
    }

    internal CompilationUnitSyntax Complete()
    {
        return CompilationUnit()
            .WithUsings(List(Usings))
            .WithMembers(List(Members))
            .WithEndOfFileToken(Lead.Close(Token(SyntaxKind.EndOfFileToken)));
    }
}

internal sealed class NamespaceScope : ContainerScope<NamespaceScope>
{
    private protected override NamespaceScope Next => this;

    internal NamespaceDeclarationSyntax Complete(NamespaceDeclarationSyntax declaration)
    {
        return declaration
            .WithUsings(List(Usings))
            .WithMembers(List(Members))
            .WithCloseBraceToken(Lead.Close(Token(SyntaxKind.CloseBraceToken)));
    }
}
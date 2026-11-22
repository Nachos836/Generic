using System;
using System.Collections.Generic;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Shared.CodeGeneration.Scoped.Rendering;
using static Microsoft.CodeAnalysis.CSharp.SyntaxFactory;

namespace Shared.CodeGeneration.Scoped;

internal abstract class DeclarationScope<TNext> : Scope<TNext> where TNext : class
{
    private protected DeclarationScope(Lead lead) : base(lead)
    {
    }

    public TNext Attribute(string name, IReadOnlyList<Arg>? arguments = null, bool when = true)
    {
        if (when) Lead.AddAttribute(Declarations.Attribute(name, arguments));
        return Next;
    }

    public TNext SplitAttributes(bool when = true)
    {
        if (when) Lead.SplitAttributes();
        return Next;
    }

    public TNext Public(bool when = true) => Modifier(SyntaxKind.PublicKeyword, when);
    public TNext Internal(bool when = true) => Modifier(SyntaxKind.InternalKeyword, when);
    public TNext Private(bool when = true) => Modifier(SyntaxKind.PrivateKeyword, when);
    public TNext Static(bool when = true) => Modifier(SyntaxKind.StaticKeyword, when);
    public TNext Readonly(bool when = true) => Modifier(SyntaxKind.ReadOnlyKeyword, when);
    public TNext Sealed(bool when = true) => Modifier(SyntaxKind.SealedKeyword, when);
    public TNext Override(bool when = true) => Modifier(SyntaxKind.OverrideKeyword, when);
    public TNext Partial(bool when = true) => Modifier(SyntaxKind.PartialKeyword, when);
    public TNext Unsafe(bool when = true) => Modifier(SyntaxKind.UnsafeKeyword, when);
    public TNext Ref(bool when = true) => Modifier(SyntaxKind.RefKeyword, when);

    public TNext Class(string name, Action<TypeScope> body, bool when = true)
    {
        return Type(ClassDeclaration(Names.Identifier(name)), name, body, when);
    }

    public TNext Struct(string name, Action<TypeScope> body, bool when = true)
    {
        return Type(StructDeclaration(Names.Identifier(name)), name, body, when);
    }

    public TNext Interface(string name, Action<TypeScope> body, bool when = true)
    {
        return Type(InterfaceDeclaration(Names.Identifier(name)), name, body, when);
    }

    public TNext Enum(string name, Action<EnumScope> body, bool when = true)
    {
        if (when is false) return Skip();

        var declaration = EnumDeclaration(Names.Identifier(name))
            .WithAttributeLists(Lead.TakeAttributes())
            .WithModifiers(Lead.TakeModifiers());

        var scope = new EnumScope();
        body(scope);
        return AddMember(scope.Complete(declaration));
    }

    private protected abstract void Add(MemberDeclarationSyntax member);

    private protected TNext AddMember(MemberDeclarationSyntax member)
    {
        Add(Lead.Attach(member));
        return Next;
    }

    private protected TNext Modifier(SyntaxKind keyword, bool when)
    {
        if (when) Lead.AddModifier(keyword);
        return Next;
    }

    private protected TNext Skip()
    {
        Lead.DropPrefixes();
        return Next;
    }

    private TNext Type(TypeDeclarationSyntax declaration, string name, Action<TypeScope> body, bool when)
    {
        if (when is false) return Skip();

        declaration = declaration
            .WithAttributeLists(Lead.TakeAttributes())
            .WithModifiers(Lead.TakeModifiers());

        var scope = new TypeScope(name);
        body(scope);
        return AddMember(scope.Complete(declaration));
    }
}
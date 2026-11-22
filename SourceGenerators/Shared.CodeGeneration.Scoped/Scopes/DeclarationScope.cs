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
        if (when) Lead.AddAttribute(Declarations.BuildAttribute(name, arguments));
        return Next;
    }

    public TNext SplitAttributes(bool when = true)
    {
        if (when) Lead.SplitAttributes();
        return Next;
    }

    public TNext Public(bool when = true) => AddModifier(SyntaxKind.PublicKeyword, when);
    public TNext Internal(bool when = true) => AddModifier(SyntaxKind.InternalKeyword, when);
    public TNext Protected(bool when = true) => AddModifier(SyntaxKind.ProtectedKeyword, when);
    public TNext Private(bool when = true) => AddModifier(SyntaxKind.PrivateKeyword, when);
    public TNext FileLocal(bool when = true) => AddModifier(SyntaxKind.FileKeyword, when);
    public TNext Static(bool when = true) => AddModifier(SyntaxKind.StaticKeyword, when);
    public TNext Readonly(bool when = true) => AddModifier(SyntaxKind.ReadOnlyKeyword, when);
    public TNext Const(bool when = true) => AddModifier(SyntaxKind.ConstKeyword, when);
    public TNext Sealed(bool when = true) => AddModifier(SyntaxKind.SealedKeyword, when);
    public TNext Abstract(bool when = true) => AddModifier(SyntaxKind.AbstractKeyword, when);
    public TNext Virtual(bool when = true) => AddModifier(SyntaxKind.VirtualKeyword, when);
    public TNext Override(bool when = true) => AddModifier(SyntaxKind.OverrideKeyword, when);
    public TNext New(bool when = true) => AddModifier(SyntaxKind.NewKeyword, when);
    public TNext Partial(bool when = true) => AddModifier(SyntaxKind.PartialKeyword, when);
    public TNext Unsafe(bool when = true) => AddModifier(SyntaxKind.UnsafeKeyword, when);
    public TNext Extern(bool when = true) => AddModifier(SyntaxKind.ExternKeyword, when);
    public TNext Async(bool when = true) => AddModifier(SyntaxKind.AsyncKeyword, when);
    public TNext Required(bool when = true) => AddModifier(SyntaxKind.RequiredKeyword, when);
    public TNext Volatile(bool when = true) => AddModifier(SyntaxKind.VolatileKeyword, when);
    public TNext Ref(bool when = true) => AddModifier(SyntaxKind.RefKeyword, when);

    public TNext Class(string name, Action<TypeScope> body, bool when = true)
    {
        return AddType(ClassDeclaration(Names.CreateIdentifier(name)), new TypeScope(name), body, when);
    }

    public TNext Record(string name, Action<TypeScope> body, bool when = true)
    {
        var declaration = RecordDeclaration(Token(SyntaxKind.RecordKeyword), Names.CreateIdentifier(name))
            .WithOpenBraceToken(Token(SyntaxKind.OpenBraceToken));
        return AddType(declaration, new TypeScope(name), body, when);
    }

    public TNext Struct(string name, Action<TypeScope> body, bool when = true)
    {
        return AddType(StructDeclaration(Names.CreateIdentifier(name)), new TypeScope(name), body, when);
    }

    public TNext Interface(string name, Action<InterfaceScope> body, bool when = true)
    {
        return AddType(InterfaceDeclaration(Names.CreateIdentifier(name)), new InterfaceScope(), body, when);
    }

    public TNext Enum(string name, Action<EnumScope> body, bool when = true)
    {
        if (when is false) return Skip();

        var declaration = EnumDeclaration(Names.CreateIdentifier(name))
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
        return Skip();
    }

    private protected TNext AddModifier(SyntaxKind keyword, bool when)
    {
        if (when) Lead.AddModifier(keyword);
        return Next;
    }

    private protected TNext Skip()
    {
        Lead.DropPrefixes();
        return Next;
    }

    private TNext AddType<TScope>(TypeDeclarationSyntax declaration, TScope scope, Action<TScope> body, bool when)
        where TScope : MemberScope<TScope>
    {
        if (when is false) return Skip();

        declaration = declaration
            .WithAttributeLists(Lead.TakeAttributes())
            .WithModifiers(Lead.TakeModifiers());

        body(scope);
        return AddMember(scope.Complete(declaration));
    }
}
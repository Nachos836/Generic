using System.Collections.Generic;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Shared.CodeGeneration.Chained.Extensions;
using Shared.CodeGeneration.Chained.Rendering;
using static Microsoft.CodeAnalysis.CSharp.SyntaxFactory;

namespace Shared.CodeGeneration.Chained;

internal abstract class DeclarationChain<TSelf> : Chain<TSelf>, IMemberHost, IMemberExtensionHost<TSelf>
    where TSelf : DeclarationChain<TSelf>
{
    private protected DeclarationChain() : base(new())
    {
    }

    public TSelf Attribute(string name, IReadOnlyList<Arg>? arguments = null, bool when = true)
    {
        if (when) Lead.AddAttribute(Declarations.BuildAttribute(name, arguments));
        return Self;
    }

    public TSelf SplitAttributes(bool when = true)
    {
        if (when) Lead.SplitAttributes();
        return Self;
    }

    public TSelf Public(bool when = true) => AddModifier(SyntaxKind.PublicKeyword, when);
    public TSelf Internal(bool when = true) => AddModifier(SyntaxKind.InternalKeyword, when);
    public TSelf Static(bool when = true) => AddModifier(SyntaxKind.StaticKeyword, when);
    public TSelf Readonly(bool when = true) => AddModifier(SyntaxKind.ReadOnlyKeyword, when);
    public TSelf Sealed(bool when = true) => AddModifier(SyntaxKind.SealedKeyword, when);
    public TSelf Abstract(bool when = true) => AddModifier(SyntaxKind.AbstractKeyword, when);
    public TSelf Partial(bool when = true) => AddModifier(SyntaxKind.PartialKeyword, when);
    public TSelf Unsafe(bool when = true) => AddModifier(SyntaxKind.UnsafeKeyword, when);
    public TSelf Ref(bool when = true) => AddModifier(SyntaxKind.RefKeyword, when);

    public ClassChain<TSelf> Class(string name, bool when = true)
    {
        return new(Self, Open(when), ClassDeclaration(Names.CreateIdentifier(name)), name, when);
    }

    public RecordChain<TSelf> Record(string name, bool when = true)
    {
        var declaration = RecordDeclaration(Token(SyntaxKind.RecordKeyword), Names.CreateIdentifier(name))
            .WithOpenBraceToken(Token(SyntaxKind.OpenBraceToken));
        return new(Self, Open(when), declaration, name, when);
    }

    public StructChain<TSelf> Struct(string name, bool when = true)
    {
        return new(Self, Open(when), StructDeclaration(Names.CreateIdentifier(name)), name, when);
    }

    public InterfaceChain<TSelf> Interface(string name, bool when = true)
    {
        return new(Self, Open(when), InterfaceDeclaration(Names.CreateIdentifier(name)), when);
    }

    public EnumChain<TSelf> Enum(string name, bool when = true)
    {
        return new(Self, Open(when), EnumDeclaration(Names.CreateIdentifier(name)), when);
    }

    void IMemberHost.AddMember(MemberDeclarationSyntax member)
    {
        Collect(member);
        Lead.DropPrefixes();
    }

    TSelf IMemberExtensionHost<TSelf>.AddExtension(MemberDeclarationSyntax member, bool when)
    {
        if (when is false) return DropPrefixes();

        var opening = Open(true);

        var prefixed = member.WithoutLeadingTrivia()
            .WithAttributeLists(opening.Attributes.AddRange(member.AttributeLists))
            .WithModifiers(opening.Modifiers.AddRange(member.Modifiers))
            .WithLeadingTrivia(member.GetLeadingTrivia());

        ((IMemberHost)this).AddMember(opening.Leading.Apply(prefixed));
        return Self;
    }

    private protected abstract void Collect(MemberDeclarationSyntax member);
}
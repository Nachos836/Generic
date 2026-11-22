using System;
using System.Collections.Generic;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Shared.CodeGeneration.Scoped.Rendering;
using static Microsoft.CodeAnalysis.CSharp.SyntaxFactory;

namespace Shared.CodeGeneration.Scoped;

internal abstract class AccessorScope<TSelf> : Scope<TSelf> where TSelf : AccessorScope<TSelf>
{
    private readonly List<AccessorDeclarationSyntax> _accessors = [];

    private protected AccessorScope() : base(new())
    {
    }

    public TSelf Private(bool when = true) => AddModifier(SyntaxKind.PrivateKeyword, when);
    public TSelf Protected(bool when = true) => AddModifier(SyntaxKind.ProtectedKeyword, when);
    public TSelf Internal(bool when = true) => AddModifier(SyntaxKind.InternalKeyword, when);
    public TSelf Readonly(bool when = true) => AddModifier(SyntaxKind.ReadOnlyKeyword, when);

    public TSelf Get(bool when = true) => AddAccessor(SyntaxKind.GetAccessorDeclaration, when);
    public TSelf Get(Expr value, bool when = true) => AddAccessor(SyntaxKind.GetAccessorDeclaration, value, when);

    public TSelf Get(Action<BodyScope> body, bool when = true)
    {
        return AddAccessor(SyntaxKind.GetAccessorDeclaration, body, when);
    }

    public TSelf Set(bool when = true) => AddAccessor(SyntaxKind.SetAccessorDeclaration, when);
    public TSelf Set(Expr value, bool when = true) => AddAccessor(SyntaxKind.SetAccessorDeclaration, value, when);

    public TSelf Set(Action<BodyScope> body, bool when = true)
    {
        return AddAccessor(SyntaxKind.SetAccessorDeclaration, body, when);
    }

    public TSelf Init(bool when = true) => AddAccessor(SyntaxKind.InitAccessorDeclaration, when);
    public TSelf Init(Expr value, bool when = true) => AddAccessor(SyntaxKind.InitAccessorDeclaration, value, when);

    public TSelf Init(Action<BodyScope> body, bool when = true)
    {
        return AddAccessor(SyntaxKind.InitAccessorDeclaration, body, when);
    }

    internal AccessorListSyntax BuildAccessors()
    {
        return AccessorList(List(_accessors)).WithCloseBraceToken(Lead.Close(Token(SyntaxKind.CloseBraceToken)));
    }

    private TSelf AddAccessor(SyntaxKind kind, bool when)
    {
        return when ? Add(OpenAccessor(kind).WithSemicolonToken(Token(SyntaxKind.SemicolonToken))) : Skip();
    }

    private TSelf AddAccessor(SyntaxKind kind, Expr value, bool when)
    {
        if (when is false || value.Node is null) return Skip();
        return Add(OpenAccessor(kind)
            .WithExpressionBody(ArrowExpressionClause(value.Node))
            .WithSemicolonToken(Token(SyntaxKind.SemicolonToken)));
    }

    private TSelf AddAccessor(SyntaxKind kind, Action<BodyScope> body, bool when)
    {
        return when ? Add(OpenAccessor(kind).WithBody(BodyScope.BuildBlock(body))) : Skip();
    }

    private AccessorDeclarationSyntax OpenAccessor(SyntaxKind kind)
    {
        return AccessorDeclaration(kind).WithModifiers(Lead.TakeModifiers());
    }

    private TSelf Add(AccessorDeclarationSyntax accessor)
    {
        _accessors.Add(Lead.Attach(accessor));
        return Next;
    }

    private TSelf AddModifier(SyntaxKind keyword, bool when)
    {
        if (when) Lead.AddModifier(keyword);
        return Next;
    }

    private TSelf Skip()
    {
        Lead.DropPrefixes();
        return Next;
    }
}

internal sealed class PropertyScope : AccessorScope<PropertyScope>
{
    private protected override PropertyScope Next => this;
}

internal sealed class IndexerScope : AccessorScope<IndexerScope>
{
    private readonly List<ParameterSyntax> _parameters = [];

    private protected override IndexerScope Next => this;

    public IndexerScope Param(TypeRef type, string name, bool when = true)
    {
        if (when && type.Node is { } node) _parameters.Add(Parameter(Names.CreateIdentifier(name)).WithType(node));
        return this;
    }

    internal BracketedParameterListSyntax BuildParameters() => BracketedParameterList(SeparatedList(_parameters));
}

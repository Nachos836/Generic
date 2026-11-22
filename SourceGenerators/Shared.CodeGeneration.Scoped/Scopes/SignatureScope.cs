using System;
using System.Collections.Generic;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Shared.CodeGeneration.Scoped.Rendering;
using static Microsoft.CodeAnalysis.CSharp.SyntaxFactory;

namespace Shared.CodeGeneration.Scoped;

internal abstract class SignatureScope<TSelf> : Scope<TSelf> where TSelf : SignatureScope<TSelf>
{
    private readonly List<ParameterSyntax> _parameters = [];
    private BlockSyntax? _body = default;
    private ArrowExpressionClauseSyntax? _arrow = default;

    private protected SignatureScope() : base(new())
    {
    }

    public TSelf Attribute(string name, IReadOnlyList<Arg>? arguments = null, bool when = true)
    {
        if (when) Lead.AddAttribute(Declarations.Attribute(name, arguments));
        return Next;
    }

    public TSelf Ref(bool when = true) => Modifier(SyntaxKind.RefKeyword, when);
    public TSelf In(bool when = true) => Modifier(SyntaxKind.InKeyword, when);
    public TSelf This(bool when = true) => Modifier(SyntaxKind.ThisKeyword, when);
    public TSelf Readonly(bool when = true) => Modifier(SyntaxKind.ReadOnlyKeyword, when);

    public TSelf Param(TypeRef type, string name, Expr? value = null, bool when = true)
    {
        if (when is false || type.Node is null)
        {
            Lead.DropPrefixes();
            return Next;
        }

        var parameter = Parameter(Names.Identifier(name))
            .WithType(type.Node)
            .WithAttributeLists(Lead.TakeAttributes())
            .WithModifiers(Lead.TakeModifiers());

        if (value?.Node is { } node) parameter = parameter.WithDefault(EqualsValueClause(node));

        _parameters.Add(Lead.Attach(parameter));
        return Next;
    }

    public TSelf Body(Action<BodyScope> body, bool when = true)
    {
        if (when) _body = BodyScope.Block(body);
        return Next;
    }

    public TSelf Arrow(Expr value, bool when = true)
    {
        if (when && value.Node is { } node) _arrow = ArrowExpressionClause(node);
        return Next;
    }

    private protected TDeclaration Complete<TDeclaration>(TDeclaration declaration)
        where TDeclaration : BaseMethodDeclarationSyntax
    {
        var completed = declaration.WithParameterList(ParameterList(SeparatedList(_parameters))
            .WithCloseParenToken(Lead.Close(Token(SyntaxKind.CloseParenToken))));

        if (_body != default) return (TDeclaration)completed.WithBody(_body);
        return _arrow != default
            ? (TDeclaration)completed.WithExpressionBody(_arrow).WithSemicolonToken(Token(SyntaxKind.SemicolonToken))
            : (TDeclaration)completed.WithSemicolonToken(Token(SyntaxKind.SemicolonToken));
    }

    private TSelf Modifier(SyntaxKind keyword, bool when)
    {
        if (when) Lead.AddModifier(keyword);
        return Next;
    }
}

internal sealed class MethodScope : SignatureScope<MethodScope>
{
    private readonly List<TypeParameterSyntax> _typeParameters = [];
    private readonly List<TypeParameterConstraintClauseSyntax> _constraints = [];

    private protected override MethodScope Next => this;

    public MethodScope TypeParam(string name, Variance variance = Variance.None, bool when = true)
    {
        if (when) _typeParameters.Add(Declarations.TypeParameter(name, variance));
        return this;
    }

    public MethodScope Where(string name, IReadOnlyList<Constraint> constraints, bool when = true)
    {
        if (when && Declarations.Constraints(name, constraints) is { } clause) _constraints.Add(clause);
        return this;
    }

    internal MethodDeclarationSyntax Complete(MethodDeclarationSyntax declaration)
    {
        if (_typeParameters.Count > 0)
        {
            declaration = declaration.WithTypeParameterList(TypeParameterList(SeparatedList(_typeParameters)));
        }
        return Complete<MethodDeclarationSyntax>(declaration.WithConstraintClauses(List(_constraints)));
    }
}

internal sealed class ConstructorScope : SignatureScope<ConstructorScope>
{
    private ConstructorInitializerSyntax? _initializer = default;

    private protected override ConstructorScope Next => this;

    public ConstructorScope CallsThis(IReadOnlyList<Arg>? arguments = null, bool when = true)
    {
        if (when)
        {
            _initializer = ConstructorInitializer(SyntaxKind.ThisConstructorInitializer,
                ArgumentList(Expr.Arguments(arguments)));
        }
        return this;
    }

    internal ConstructorDeclarationSyntax Complete(ConstructorDeclarationSyntax declaration)
    {
        return Complete<ConstructorDeclarationSyntax>(declaration.WithInitializer(_initializer));
    }
}
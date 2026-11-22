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
        if (when) Lead.AddAttribute(Declarations.BuildAttribute(name, arguments));
        return Next;
    }

    public TSelf Ref(bool when = true) => AddModifier(SyntaxKind.RefKeyword, when);
    public TSelf In(bool when = true) => AddModifier(SyntaxKind.InKeyword, when);
    public TSelf This(bool when = true) => AddModifier(SyntaxKind.ThisKeyword, when);
    public TSelf Readonly(bool when = true) => AddModifier(SyntaxKind.ReadOnlyKeyword, when);

    public TSelf Param(TypeRef type, string name, Expr? value = null, bool when = true)
    {
        if (when is false || type.Node is null)
        {
            Lead.DropPrefixes();
            return Next;
        }

        var parameter = Parameter(Names.CreateIdentifier(name))
            .WithType(type.Node)
            .WithAttributeLists(Lead.TakeAttributes())
            .WithModifiers(Lead.TakeModifiers());

        if (value?.Node is { } node) parameter = parameter.WithDefault(EqualsValueClause(node));

        _parameters.Add(Lead.Attach(parameter));
        return Next;
    }

    public TSelf Body(Action<BodyScope> body, bool when = true)
    {
        if (when) _body = BodyScope.BuildBlock(body);
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

    private TSelf AddModifier(SyntaxKind keyword, bool when)
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
        if (when) _typeParameters.Add(Declarations.BuildTypeParameter(name, variance));
        return this;
    }

    public MethodScope Where(string name, IReadOnlyList<Constraint> constraints, bool when = true)
    {
        if (when && Declarations.BuildConstraints(name, constraints) is { } clause) _constraints.Add(clause);
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
        return SetInitializer(SyntaxKind.ThisConstructorInitializer, arguments, when);
    }

    public ConstructorScope CallsBase(IReadOnlyList<Arg>? arguments = null, bool when = true)
    {
        return SetInitializer(SyntaxKind.BaseConstructorInitializer, arguments, when);
    }

    internal ConstructorDeclarationSyntax Complete(ConstructorDeclarationSyntax declaration)
    {
        return Complete<ConstructorDeclarationSyntax>(declaration.WithInitializer(_initializer));
    }

    private ConstructorScope SetInitializer(SyntaxKind kind, IReadOnlyList<Arg>? arguments, bool when)
    {
        if (when) _initializer = ConstructorInitializer(kind, ArgumentList(Expr.BuildArguments(arguments)));
        return this;
    }
}

internal sealed class OperatorScope : SignatureScope<OperatorScope>
{
    private protected override OperatorScope Next => this;

    internal TDeclaration Complete<TDeclaration>(TDeclaration declaration)
        where TDeclaration : BaseMethodDeclarationSyntax
    {
        return base.Complete(declaration);
    }
}
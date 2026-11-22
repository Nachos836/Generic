using System.Collections.Generic;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Shared.CodeGeneration.Chained.Rendering;
using static Microsoft.CodeAnalysis.CSharp.SyntaxFactory;

namespace Shared.CodeGeneration.Chained;

internal abstract class SignatureChain<TSelf> : Chain<TSelf> where TSelf : SignatureChain<TSelf>
{
    private readonly List<ParameterSyntax> _parameters = [];
    private ArrowExpressionClauseSyntax? _arrow = default;

    private protected SignatureChain() : base(new())
    {
    }

    public TSelf Attribute(string name, IReadOnlyList<Arg>? arguments = null, bool when = true)
    {
        if (when) Lead.AddAttribute(Declarations.BuildAttribute(name, arguments));
        return Self;
    }

    public TSelf Ref(bool when = true) => AddModifier(SyntaxKind.RefKeyword, when);
    public TSelf In(bool when = true) => AddModifier(SyntaxKind.InKeyword, when);
    public TSelf This(bool when = true) => AddModifier(SyntaxKind.ThisKeyword, when);
    public TSelf Readonly(bool when = true) => AddModifier(SyntaxKind.ReadOnlyKeyword, when);

    public TSelf Param(TypeRef type, string name, Expr? value = null, bool when = true)
    {
        if (when is false || type.Node is null) return DropPrefixes();

        var parameter = Parameter(Names.CreateIdentifier(name))
            .WithType(type.Node)
            .WithAttributeLists(Lead.TakeAttributes())
            .WithModifiers(Lead.TakeModifiers());

        if (value?.Node is { } node) parameter = parameter.WithDefault(EqualsValueClause(node));

        _parameters.Add(Lead.Attach(parameter));
        return Self;
    }

    public TSelf Arrow(Expr value, bool when = true)
    {
        if (when && value.Node is { } node) _arrow = ArrowExpressionClause(node);
        return Self;
    }

    private protected TDeclaration Complete<TDeclaration>(TDeclaration declaration, BlockSyntax? body)
        where TDeclaration : BaseMethodDeclarationSyntax
    {
        var completed = declaration.WithParameterList(ParameterList(SeparatedList(_parameters))
            .WithCloseParenToken(Lead.Close(Token(SyntaxKind.CloseParenToken))));

        if (body != default) return (TDeclaration)completed.WithBody(body);
        return _arrow != default
            ? (TDeclaration)completed.WithExpressionBody(_arrow).WithSemicolonToken(Token(SyntaxKind.SemicolonToken))
            : (TDeclaration)completed.WithSemicolonToken(Token(SyntaxKind.SemicolonToken));
    }
}

internal sealed class MethodChain<TOuter> : SignatureChain<MethodChain<TOuter>> where TOuter : class, IMemberHost
{
    private readonly List<TypeParameterSyntax> _typeParameters = [];
    private readonly List<TypeParameterConstraintClauseSyntax> _constraints = [];
    private readonly TOuter _outer = default!;
    private readonly Leading _leading = default!;
    private readonly MethodDeclarationSyntax _declaration = default!;
    private readonly bool _active;

    internal MethodChain(TOuter outer, Leading leading, MethodDeclarationSyntax declaration, bool active)
    {
        _outer = outer;
        _leading = leading;
        _declaration = declaration;
        _active = active;
    }

    public MethodChain<TOuter> TypeParam(string name, Variance variance = Variance.None, bool when = true)
    {
        if (when) _typeParameters.Add(Declarations.BuildTypeParameter(name, variance));
        return this;
    }

    public MethodChain<TOuter> Where(string name, IReadOnlyList<Constraint> constraints, bool when = true)
    {
        if (when && Declarations.BuildConstraints(name, constraints) is { } clause) _constraints.Add(clause);
        return this;
    }

    public MethodBodyChain<TOuter> Body(bool when = true) => new(this, when);

    public TOuter EndMethod() => CompleteWith(null);

    internal TOuter CompleteWith(BlockSyntax? body)
    {
        var declaration = _declaration.WithConstraintClauses(List(_constraints));

        if (_typeParameters.Count > 0)
        {
            declaration = declaration.WithTypeParameterList(TypeParameterList(SeparatedList(_typeParameters)));
        }

        var completed = Complete(declaration, body);
        if (_active) _outer.AddMember(_leading.Apply(completed));
        return _outer;
    }
}

internal sealed class MethodBodyChain<TOuter> : StatementChain<MethodBodyChain<TOuter>>
    where TOuter : class, IMemberHost
{
    private readonly MethodChain<TOuter> _method = default!;
    private readonly bool _active;

    internal MethodBodyChain(MethodChain<TOuter> method, bool active) : base(new())
    {
        _method = method;
        _active = active;
    }

    public TOuter EndMethod() => _method.CompleteWith(_active ? BuildBlock() : null);
}

internal sealed class ConstructorChain<TOuter> : SignatureChain<ConstructorChain<TOuter>>
    where TOuter : class, IMemberHost
{
    private readonly TOuter _outer = default!;
    private readonly Leading _leading = default!;
    private readonly ConstructorDeclarationSyntax _declaration = default!;
    private readonly bool _active;
    private ConstructorInitializerSyntax? _initializer = default;

    internal ConstructorChain(TOuter outer, Leading leading, ConstructorDeclarationSyntax declaration, bool active)
    {
        _outer = outer;
        _leading = leading;
        _declaration = declaration;
        _active = active;
    }

    public ConstructorChain<TOuter> CallsThis(IReadOnlyList<Arg>? arguments = null, bool when = true)
    {
        if (when)
        {
            _initializer = ConstructorInitializer(SyntaxKind.ThisConstructorInitializer,
                ArgumentList(Expr.BuildArguments(arguments)));
        }

        return this;
    }

    public ConstructorBodyChain<TOuter> Body(bool when = true) => new(this, when);

    public TOuter EndConstructor() => CompleteWith(null);

    internal TOuter CompleteWith(BlockSyntax? body)
    {
        var completed = Complete(_declaration.WithInitializer(_initializer), body);
        if (_active) _outer.AddMember(_leading.Apply(completed));
        return _outer;
    }
}

internal sealed class ConstructorBodyChain<TOuter> : StatementChain<ConstructorBodyChain<TOuter>>
    where TOuter : class, IMemberHost
{
    private readonly ConstructorChain<TOuter> _constructor = default!;
    private readonly bool _active;

    internal ConstructorBodyChain(ConstructorChain<TOuter> constructor, bool active) : base(new())
    {
        _constructor = constructor;
        _active = active;
    }

    public TOuter EndConstructor() => _constructor.CompleteWith(_active ? BuildBlock() : null);
}
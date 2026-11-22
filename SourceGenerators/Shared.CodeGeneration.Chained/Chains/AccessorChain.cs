using System.Collections.Generic;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Shared.CodeGeneration.Chained.Rendering;
using static Microsoft.CodeAnalysis.CSharp.SyntaxFactory;

namespace Shared.CodeGeneration.Chained;

internal interface IAccessorHost
{
    void AddAccessor(AccessorDeclarationSyntax accessor);
}

internal abstract class AccessorChain<TSelf> : IAccessorHost where TSelf : AccessorChain<TSelf>
{
    private readonly List<AccessorDeclarationSyntax> _accessors = [];

    private protected Lead Lead { get; } = new();
    private TSelf Self => (TSelf)this;

    public TSelf Private(bool when = true) => AddModifier(SyntaxKind.PrivateKeyword, when);
    public TSelf Protected(bool when = true) => AddModifier(SyntaxKind.ProtectedKeyword, when);
    public TSelf Internal(bool when = true) => AddModifier(SyntaxKind.InternalKeyword, when);
    public TSelf Readonly(bool when = true) => AddModifier(SyntaxKind.ReadOnlyKeyword, when);

    public TSelf Get(bool when = true) => AddAccessor(SyntaxKind.GetAccessorDeclaration, when);
    public TSelf Get(Expr value, bool when = true) => AddAccessor(SyntaxKind.GetAccessorDeclaration, value, when);

    public GetAccessorChain<TSelf> GetBody(bool when = true)
    {
        return new(Self, OpenAccessor(SyntaxKind.GetAccessorDeclaration, when), when);
    }

    public TSelf Set(bool when = true) => AddAccessor(SyntaxKind.SetAccessorDeclaration, when);
    public TSelf Set(Expr value, bool when = true) => AddAccessor(SyntaxKind.SetAccessorDeclaration, value, when);

    public SetAccessorChain<TSelf> SetBody(bool when = true)
    {
        return new(Self, OpenAccessor(SyntaxKind.SetAccessorDeclaration, when), when);
    }

    public TSelf Init(bool when = true) => AddAccessor(SyntaxKind.InitAccessorDeclaration, when);
    public TSelf Init(Expr value, bool when = true) => AddAccessor(SyntaxKind.InitAccessorDeclaration, value, when);

    public InitAccessorChain<TSelf> InitBody(bool when = true)
    {
        return new(Self, OpenAccessor(SyntaxKind.InitAccessorDeclaration, when), when);
    }

    void IAccessorHost.AddAccessor(AccessorDeclarationSyntax accessor) => _accessors.Add(accessor);

    private protected AccessorListSyntax BuildAccessors()
    {
        return AccessorList(List(_accessors)).WithCloseBraceToken(Lead.Close(Token(SyntaxKind.CloseBraceToken)));
    }

    private TSelf AddAccessor(SyntaxKind kind, bool when)
    {
        var accessor = OpenAccessor(kind, when);
        if (when) _accessors.Add(accessor.WithSemicolonToken(Token(SyntaxKind.SemicolonToken)));
        return Self;
    }

    private TSelf AddAccessor(SyntaxKind kind, Expr value, bool when)
    {
        var active = when && value.Node is not null;
        var accessor = OpenAccessor(kind, active);

        if (active)
        {
            _accessors.Add(accessor.WithExpressionBody(ArrowExpressionClause(value.Node!))
                .WithSemicolonToken(Token(SyntaxKind.SemicolonToken)));
        }

        return Self;
    }

    private AccessorDeclarationSyntax OpenAccessor(SyntaxKind kind, bool active)
    {
        var accessor = AccessorDeclaration(kind);
        if (active) return accessor.WithModifiers(Lead.TakeModifiers());

        Lead.DropPrefixes();
        return accessor;
    }

    private TSelf AddModifier(SyntaxKind keyword, bool when)
    {
        if (when) Lead.AddModifier(keyword);
        return Self;
    }
}

internal abstract class AccessorBodyChain<TSelf, TOuter> : StatementChain<TSelf>
    where TSelf : AccessorBodyChain<TSelf, TOuter>
    where TOuter : class, IAccessorHost
{
    private readonly TOuter _outer = default!;
    private readonly AccessorDeclarationSyntax _accessor = default!;
    private readonly bool _active;

    private protected AccessorBodyChain(TOuter outer, AccessorDeclarationSyntax accessor, bool active) : base(new())
    {
        _outer = outer;
        _accessor = accessor;
        _active = active;
    }

    private protected TOuter Close()
    {
        var block = BuildBlock();
        if (_active) _outer.AddAccessor(_accessor.WithBody(block));
        return _outer;
    }
}

internal sealed class GetAccessorChain<TOuter> : AccessorBodyChain<GetAccessorChain<TOuter>, TOuter>
    where TOuter : class, IAccessorHost
{
    internal GetAccessorChain(TOuter outer, AccessorDeclarationSyntax accessor, bool active)
        : base(outer, accessor, active)
    {
    }

    public TOuter EndGet() => Close();
}

internal sealed class SetAccessorChain<TOuter> : AccessorBodyChain<SetAccessorChain<TOuter>, TOuter>
    where TOuter : class, IAccessorHost
{
    internal SetAccessorChain(TOuter outer, AccessorDeclarationSyntax accessor, bool active)
        : base(outer, accessor, active)
    {
    }

    public TOuter EndSet() => Close();
}

internal sealed class InitAccessorChain<TOuter> : AccessorBodyChain<InitAccessorChain<TOuter>, TOuter>
    where TOuter : class, IAccessorHost
{
    internal InitAccessorChain(TOuter outer, AccessorDeclarationSyntax accessor, bool active)
        : base(outer, accessor, active)
    {
    }

    public TOuter EndInit() => Close();
}

internal sealed class PropertyChain<TOuter> : AccessorChain<PropertyChain<TOuter>> where TOuter : class, IMemberHost
{
    private readonly TOuter _outer = default!;
    private readonly Leading _leading = default!;
    private readonly PropertyDeclarationSyntax _declaration = default!;
    private readonly Expr? _value = default;
    private readonly bool _active;

    internal PropertyChain(TOuter outer, Leading leading, PropertyDeclarationSyntax declaration, Expr? value,
        bool active)
    {
        _outer = outer;
        _leading = leading;
        _declaration = declaration;
        _value = value;
        _active = active;
    }

    public TOuter EndProperty()
    {
        var declaration = _declaration.WithAccessorList(BuildAccessors());

        if (_value?.Node is { } node)
        {
            declaration = declaration.WithInitializer(EqualsValueClause(node))
                .WithSemicolonToken(Token(SyntaxKind.SemicolonToken));
        }

        if (_active) _outer.AddMember(_leading.Apply(declaration));
        return _outer;
    }
}

internal sealed class IndexerChain<TOuter> : AccessorChain<IndexerChain<TOuter>> where TOuter : class, IMemberHost
{
    private readonly List<ParameterSyntax> _parameters = [];
    private readonly TOuter _outer = default!;
    private readonly Leading _leading = default!;
    private readonly IndexerDeclarationSyntax _declaration = default!;
    private readonly bool _active;

    internal IndexerChain(TOuter outer, Leading leading, IndexerDeclarationSyntax declaration, bool active)
    {
        _outer = outer;
        _leading = leading;
        _declaration = declaration;
        _active = active;
    }

    public IndexerChain<TOuter> Param(TypeRef type, string name, bool when = true)
    {
        if (when && type.Node is { } node) _parameters.Add(Parameter(Names.CreateIdentifier(name)).WithType(node));

        Lead.DropPrefixes();
        return this;
    }

    public TOuter EndIndexer()
    {
        var declaration = _declaration
            .WithParameterList(BracketedParameterList(SeparatedList(_parameters)))
            .WithAccessorList(BuildAccessors());

        if (_active) _outer.AddMember(_leading.Apply(declaration));
        return _outer;
    }
}

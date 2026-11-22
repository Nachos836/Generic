using System.Collections.Generic;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Shared.CodeGeneration.Chained.Rendering;
using static Microsoft.CodeAnalysis.CSharp.SyntaxFactory;

namespace Shared.CodeGeneration.Chained;

internal abstract class MemberChain<TSelf> : DeclarationChain<TSelf> where TSelf : MemberChain<TSelf>
{
    private readonly List<TypeParameterSyntax> _typeParameters = [];
    private readonly List<BaseTypeSyntax> _baseTypes = [];
    private readonly List<TypeParameterConstraintClauseSyntax> _constraints = [];
    private readonly List<MemberDeclarationSyntax> _members = [];
    private readonly Opening _opening = default!;
    private readonly TypeDeclarationSyntax _declaration = default!;

    private protected MemberChain(Opening opening, TypeDeclarationSyntax declaration, bool active)
    {
        _opening = opening;
        _declaration = declaration;
        Active = active;
    }

    private protected bool Active { get; }

    public TSelf TypeParam(string name, Variance variance = Variance.None, bool when = true)
    {
        if (when) _typeParameters.Add(Declarations.BuildTypeParameter(name, variance));
        return Self;
    }

    public TSelf Implements(TypeRef type, bool when = true) => AddBaseType(type, when);

    public TSelf Where(string name, IReadOnlyList<Constraint> constraints, bool when = true)
    {
        if (when && Declarations.BuildConstraints(name, constraints) is { } clause) _constraints.Add(clause);
        return Self;
    }

    public TSelf ExplicitFor(TypeRef type, bool when = true)
    {
        if (when && type.Node is NameSyntax name) Lead.SetExplicitInterface(name);
        return Self;
    }

    public TSelf Event(TypeRef type, string name, bool when = true)
    {
        if (when is false || type.Node is null) return DropPrefixes();

        var variable = SingletonSeparatedList(Declarations.BuildVariable(name, null));
        var member = EventFieldDeclaration(VariableDeclaration(type.Node, variable))
            .WithAttributeLists(Lead.TakeAttributes())
            .WithModifiers(Lead.TakeModifiers());

        ((IMemberHost)this).AddMember(Lead.Attach(member));
        return Self;
    }

    public MethodChain<TSelf> Method(TypeRef returnType, string name, bool when = true)
    {
        var active = when && returnType.Node is not null;
        var opening = Open(active);
        var (modifiers, type) = Declarations.SplitRef(opening.Modifiers, returnType.Node ?? IdentifierName("void"));
        var declaration = MethodDeclaration(type, Names.CreateIdentifier(name))
            .WithAttributeLists(opening.Attributes)
            .WithModifiers(modifiers);

        if (opening.ExplicitInterface is { } explicitInterface)
        {
            declaration = declaration.WithExplicitInterfaceSpecifier(ExplicitInterfaceSpecifier(explicitInterface));
        }

        return new(Self, opening.Leading, declaration, active);
    }

    private protected TSelf AddBaseType(TypeRef type, bool when)
    {
        if (when && type.Node is { } node) _baseTypes.Add(SimpleBaseType(node));
        return Self;
    }

    private protected void Complete(IMemberHost outer)
    {
        var declaration = _declaration.WithAttributeLists(_opening.Attributes).WithModifiers(_opening.Modifiers);

        if (_typeParameters.Count > 0)
        {
            declaration = declaration.WithTypeParameterList(TypeParameterList(SeparatedList(_typeParameters)));
        }

        if (_baseTypes.Count > 0) declaration = declaration.WithBaseList(BaseList(SeparatedList(_baseTypes)));

        declaration = declaration
            .WithConstraintClauses(List(_constraints))
            .WithMembers(List(_members))
            .WithCloseBraceToken(Lead.Close(Token(SyntaxKind.CloseBraceToken)));

        if (Active) outer.AddMember(_opening.Leading.Apply(declaration));
    }

    private protected override void Collect(MemberDeclarationSyntax member) => _members.Add(member);
}

internal abstract class TypeChain<TSelf> : MemberChain<TSelf> where TSelf : TypeChain<TSelf>
{
    private readonly string _name = default!;

    private protected TypeChain(Opening opening, TypeDeclarationSyntax declaration, string name, bool active)
        : base(opening, declaration, active)
    {
        _name = name;
    }

    public TSelf Inherits(TypeRef type, bool when = true) => AddBaseType(type, when);

    public TSelf Field(TypeRef type, string name, Expr? value = null, bool when = true)
    {
        if (when is false || type.Node is null) return DropPrefixes();

        var (modifiers, fieldType) = Declarations.SplitRef(Lead.TakeModifiers(), type.Node);
        var variable = SingletonSeparatedList(Declarations.BuildVariable(name, value));
        var member = FieldDeclaration(VariableDeclaration(fieldType, variable))
            .WithAttributeLists(Lead.TakeAttributes())
            .WithModifiers(modifiers);

        ((IMemberHost)this).AddMember(Lead.Attach(member));
        return Self;
    }

    public ConstructorChain<TSelf> Constructor(bool when = true)
    {
        var opening = Open(when);
        var declaration = ConstructorDeclaration(Names.CreateIdentifier(_name))
            .WithAttributeLists(opening.Attributes)
            .WithModifiers(opening.Modifiers);
        return new(Self, opening.Leading, declaration, when);
    }
}

internal sealed class ClassChain<TOuter> : TypeChain<ClassChain<TOuter>> where TOuter : class, IMemberHost
{
    private readonly TOuter _outer = default!;

    internal ClassChain(TOuter outer, Opening opening, TypeDeclarationSyntax declaration, string name, bool active)
        : base(opening, declaration, name, active)
    {
        _outer = outer;
    }

    public TOuter EndClass()
    {
        Complete(_outer);
        return _outer;
    }
}

internal sealed class StructChain<TOuter> : TypeChain<StructChain<TOuter>> where TOuter : class, IMemberHost
{
    private readonly TOuter _outer = default!;

    internal StructChain(TOuter outer, Opening opening, TypeDeclarationSyntax declaration, string name, bool active)
        : base(opening, declaration, name, active)
    {
        _outer = outer;
    }

    public TOuter EndStruct()
    {
        Complete(_outer);
        return _outer;
    }
}

internal sealed class InterfaceChain<TOuter> : MemberChain<InterfaceChain<TOuter>> where TOuter : class, IMemberHost
{
    private readonly TOuter _outer = default!;

    internal InterfaceChain(TOuter outer, Opening opening, TypeDeclarationSyntax declaration, bool active)
        : base(opening, declaration, active)
    {
        _outer = outer;
    }

    public TOuter EndInterface()
    {
        Complete(_outer);
        return _outer;
    }
}

internal sealed class EnumChain<TOuter> : Chain<EnumChain<TOuter>> where TOuter : class, IMemberHost
{
    private readonly List<EnumMemberDeclarationSyntax> _members = [];
    private readonly TOuter _outer = default!;
    private readonly Opening _opening = default!;
    private readonly EnumDeclarationSyntax _declaration = default!;
    private readonly bool _active;
    private BaseListSyntax? _baseList = default;

    internal EnumChain(TOuter outer, Opening opening, EnumDeclarationSyntax declaration, bool active) : base(new())
    {
        _outer = outer;
        _opening = opening;
        _declaration = declaration;
        _active = active;
    }

    public EnumChain<TOuter> Inherits(TypeRef type, bool when = true)
    {
        if (when && type.Node is { } node)
        {
            _baseList = BaseList(SingletonSeparatedList<BaseTypeSyntax>(SimpleBaseType(node)));
        }

        return this;
    }

    public EnumChain<TOuter> EnumMember(string name, Expr? value = null, bool when = true)
    {
        if (when is false) return this;

        var member = EnumMemberDeclaration(Names.CreateIdentifier(name));
        if (value?.Node is { } node) member = member.WithEqualsValue(EqualsValueClause(node));

        _members.Add(Lead.Attach(member));
        return this;
    }

    public TOuter EndEnum()
    {
        var declaration = _declaration
            .WithAttributeLists(_opening.Attributes)
            .WithModifiers(_opening.Modifiers)
            .WithBaseList(_baseList)
            .WithMembers(SeparatedList(_members))
            .WithCloseBraceToken(Lead.Close(Token(SyntaxKind.CloseBraceToken)));

        if (_active) _outer.AddMember(_opening.Leading.Apply(declaration));
        return _outer;
    }
}
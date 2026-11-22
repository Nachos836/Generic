using System;
using System.Collections.Generic;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Shared.CodeGeneration.Scoped.Rendering;
using static Microsoft.CodeAnalysis.CSharp.SyntaxFactory;

namespace Shared.CodeGeneration.Scoped;

internal sealed class TypeScope : DeclarationScope<TypeScope>
{
    private readonly string _name;
    private readonly List<TypeParameterSyntax> _typeParameters = [];
    private readonly List<BaseTypeSyntax> _baseTypes = [];
    private readonly List<TypeParameterConstraintClauseSyntax> _constraints = [];
    private readonly List<MemberDeclarationSyntax> _members = [];

    internal TypeScope(string name) : base(new()) => _name = name;

    private protected override TypeScope Next => this;

    public TypeScope TypeParam(string name, Variance variance = Variance.None, bool when = true)
    {
        if (when) _typeParameters.Add(Declarations.TypeParameter(name, variance));
        return this;
    }

    public TypeScope Inherits(TypeRef type, bool when = true) => BaseType(type, when);
    public TypeScope Implements(TypeRef type, bool when = true) => BaseType(type, when);

    public TypeScope Where(string name, IReadOnlyList<Constraint> constraints, bool when = true)
    {
        if (when && Declarations.Constraints(name, constraints) is { } clause) _constraints.Add(clause);
        return this;
    }

    public TypeScope ExplicitFor(TypeRef type, bool when = true)
    {
        if (when && type.Node is NameSyntax name) Lead.SetExplicitInterface(name);
        return this;
    }

    public TypeScope Field(TypeRef type, string name, Expr? value = null, bool when = true)
    {
        if (when is false || type.Node is null) return Skip();

        var (modifiers, fieldType) = Declarations.SplitRef(Lead.TakeModifiers(), type.Node);
        return AddMember(FieldDeclaration(VariableDeclaration(fieldType, SingletonSeparatedList(Variable(name, value))))
            .WithAttributeLists(Lead.TakeAttributes())
            .WithModifiers(modifiers));
    }

    public TypeScope Event(TypeRef type, string name, bool when = true)
    {
        if (when is false || type.Node is null) return Skip();
        return AddMember(
            EventFieldDeclaration(VariableDeclaration(type.Node, SingletonSeparatedList(Variable(name, null))))
                .WithAttributeLists(Lead.TakeAttributes())
                .WithModifiers(Lead.TakeModifiers()));
    }

    public TypeScope Constructor(Action<ConstructorScope> signature, bool when = true)
    {
        if (when is false) return Skip();

        var declaration = ConstructorDeclaration(Names.Identifier(_name))
            .WithAttributeLists(Lead.TakeAttributes())
            .WithModifiers(Lead.TakeModifiers());

        var scope = new ConstructorScope();
        signature(scope);
        return AddMember(scope.Complete(declaration));
    }

    public TypeScope Method(TypeRef returnType, string name, Action<MethodScope> signature, bool when = true)
    {
        if (when is false || returnType.Node is null) return Skip();

        var (modifiers, type) = Declarations.SplitRef(Lead.TakeModifiers(), returnType.Node);
        var declaration = MethodDeclaration(type, Names.Identifier(name))
            .WithAttributeLists(Lead.TakeAttributes())
            .WithModifiers(modifiers);

        if (Lead.TakeExplicitInterface() is { } explicitInterface)
        {
            declaration = declaration.WithExplicitInterfaceSpecifier(ExplicitInterfaceSpecifier(explicitInterface));
        }

        var scope = new MethodScope();
        signature(scope);
        return AddMember(scope.Complete(declaration));
    }

    internal TypeDeclarationSyntax Complete(TypeDeclarationSyntax declaration)
    {
        if (_typeParameters.Count > 0)
        {
            declaration = declaration.WithTypeParameterList(TypeParameterList(SeparatedList(_typeParameters)));
        }

        if (_baseTypes.Count > 0) declaration = declaration.WithBaseList(BaseList(SeparatedList(_baseTypes)));
        return declaration
            .WithConstraintClauses(List(_constraints))
            .WithMembers(List(_members))
            .WithCloseBraceToken(Lead.Close(Token(SyntaxKind.CloseBraceToken)));
    }

    private protected override void Add(MemberDeclarationSyntax member) => _members.Add(member);

    private TypeScope BaseType(TypeRef type, bool when)
    {
        if (when && type.Node is { } node) _baseTypes.Add(SimpleBaseType(node));
        return this;
    }

    private static VariableDeclaratorSyntax Variable(string name, Expr? value)
    {
        var declarator = VariableDeclarator(Names.Identifier(name));
        return value?.Node is { } node ? declarator.WithInitializer(EqualsValueClause(node)) : declarator;
    }
}

internal sealed class EnumScope : Scope<EnumScope>
{
    private readonly List<EnumMemberDeclarationSyntax> _members = [];
    private BaseListSyntax? _baseList = default;

    internal EnumScope() : base(new())
    {
    }

    private protected override EnumScope Next => this;

    public EnumScope Inherits(TypeRef type, bool when = true)
    {
        if (when && type.Node is { } node)
        {
            _baseList = BaseList(SingletonSeparatedList<BaseTypeSyntax>(SimpleBaseType(node)));
        }
        return this;
    }

    public EnumScope EnumMember(string name, Expr? value = null, bool when = true)
    {
        if (when is false) return this;

        var member = EnumMemberDeclaration(Names.Identifier(name));
        if (value?.Node is { } node) member = member.WithEqualsValue(EqualsValueClause(node));

        _members.Add(Lead.Attach(member));
        return this;
    }

    internal EnumDeclarationSyntax Complete(EnumDeclarationSyntax declaration)
    {
        return declaration
            .WithBaseList(_baseList)
            .WithMembers(SeparatedList(_members))
            .WithCloseBraceToken(Lead.Close(Token(SyntaxKind.CloseBraceToken)));
    }
}
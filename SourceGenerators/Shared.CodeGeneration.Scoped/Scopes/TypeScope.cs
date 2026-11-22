using System;
using System.Collections.Generic;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Shared.CodeGeneration.Scoped.Rendering;
using static Microsoft.CodeAnalysis.CSharp.SyntaxFactory;

namespace Shared.CodeGeneration.Scoped;

internal abstract class MemberScope<TSelf> : DeclarationScope<TSelf> where TSelf : MemberScope<TSelf>
{
    private readonly List<TypeParameterSyntax> _typeParameters = [];
    private readonly List<BaseTypeSyntax> _baseTypes = [];
    private readonly List<TypeParameterConstraintClauseSyntax> _constraints = [];
    private readonly List<MemberDeclarationSyntax> _members = [];

    private protected MemberScope() : base(new())
    {
    }

    public TSelf Private(bool when = true) => AddModifier(SyntaxKind.PrivateKeyword, when);
    public TSelf Protected(bool when = true) => AddModifier(SyntaxKind.ProtectedKeyword, when);
    public TSelf Virtual(bool when = true) => AddModifier(SyntaxKind.VirtualKeyword, when);
    public TSelf Override(bool when = true) => AddModifier(SyntaxKind.OverrideKeyword, when);
    public TSelf New(bool when = true) => AddModifier(SyntaxKind.NewKeyword, when);
    public TSelf Extern(bool when = true) => AddModifier(SyntaxKind.ExternKeyword, when);
    public TSelf Async(bool when = true) => AddModifier(SyntaxKind.AsyncKeyword, when);

    public TSelf TypeParam(string name, Variance variance = Variance.None, bool when = true)
    {
        if (when) _typeParameters.Add(Declarations.BuildTypeParameter(name, variance));
        return Next;
    }

    public TSelf Implements(TypeRef type, bool when = true) => AddBaseType(type, when);

    public TSelf Where(string name, IReadOnlyList<Constraint> constraints, bool when = true)
    {
        if (when && Declarations.BuildConstraints(name, constraints) is { } clause) _constraints.Add(clause);
        return Next;
    }

    public TSelf ExplicitFor(TypeRef type, bool when = true)
    {
        if (when && type.Node is NameSyntax name) Lead.SetExplicitInterface(name);
        return Next;
    }

    public TSelf Event(TypeRef type, string name, bool when = true)
    {
        if (when is false || type.Node is null) return Skip();
        return AddMember(
            EventFieldDeclaration(VariableDeclaration(type.Node, SingletonSeparatedList(BuildVariable(name, null))))
                .WithAttributeLists(Lead.TakeAttributes())
                .WithModifiers(Lead.TakeModifiers()));
    }

    public TSelf Method(TypeRef returnType, string name, Action<MethodScope> signature, bool when = true)
    {
        if (when is false || returnType.Node is null) return Skip();

        var (modifiers, type) = Declarations.SplitRef(Lead.TakeModifiers(), returnType.Node);

        var declaration = MethodDeclaration(type, Names.CreateIdentifier(name))
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

    public TSelf Property(TypeRef type, string name, Action<PropertyScope> accessors, Expr? value = null,
        bool when = true)
    {
        if (when is false || type.Node is null) return Skip();

        var (modifiers, propertyType) = Declarations.SplitRef(Lead.TakeModifiers(), type.Node);

        var declaration = PropertyDeclaration(propertyType, Names.CreateIdentifier(name))
            .WithAttributeLists(Lead.TakeAttributes())
            .WithModifiers(modifiers)
            .WithExplicitInterfaceSpecifier(TakeExplicitInterface());

        var scope = new PropertyScope();
        accessors(scope);
        declaration = declaration.WithAccessorList(scope.BuildAccessors());

        if (value?.Node is { } node)
        {
            declaration = declaration.WithInitializer(EqualsValueClause(node))
                .WithSemicolonToken(Token(SyntaxKind.SemicolonToken));
        }

        return AddMember(declaration);
    }

    public TSelf Indexer(TypeRef type, Action<IndexerScope> signature, bool when = true)
    {
        if (when is false || type.Node is null) return Skip();

        var (modifiers, indexerType) = Declarations.SplitRef(Lead.TakeModifiers(), type.Node);

        var declaration = IndexerDeclaration(indexerType)
            .WithAttributeLists(Lead.TakeAttributes())
            .WithModifiers(modifiers)
            .WithExplicitInterfaceSpecifier(TakeExplicitInterface());

        var scope = new IndexerScope();
        signature(scope);
        return AddMember(declaration.WithParameterList(scope.BuildParameters())
            .WithAccessorList(scope.BuildAccessors()));
    }

    public TSelf Operator(TypeRef returnType, BinaryOperator @operator, Action<OperatorScope> signature,
        bool when = true)
    {
        if (when is false || returnType.Node is null) return Skip();
        return AddOperator(OperatorDeclaration(returnType.Node, Token(Declarations.MapOperatorToken(@operator)))
            .WithExplicitInterfaceSpecifier(TakeExplicitInterface()), signature);
    }

    public TSelf Operator(TypeRef returnType, UnaryOperator @operator, Action<OperatorScope> signature,
        bool when = true)
    {
        if (when is false || returnType.Node is null) return Skip();
        return AddOperator(OperatorDeclaration(returnType.Node, Token(Declarations.MapOperatorToken(@operator)))
            .WithExplicitInterfaceSpecifier(TakeExplicitInterface()), signature);
    }

    public TSelf Conversion(ConversionKind kind, TypeRef type, Action<OperatorScope> signature, bool when = true)
    {
        if (when is false || type.Node is null) return Skip();

        var keyword = kind == ConversionKind.Implicit ? SyntaxKind.ImplicitKeyword : SyntaxKind.ExplicitKeyword;
        return AddOperator(ConversionOperatorDeclaration(Token(keyword), type.Node)
            .WithExplicitInterfaceSpecifier(TakeExplicitInterface()), signature);
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

    private protected TSelf AddBaseType(TypeRef type, bool when)
    {
        if (when && type.Node is { } node) _baseTypes.Add(SimpleBaseType(node));
        return Next;
    }

    private TSelf AddOperator<TDeclaration>(TDeclaration declaration, Action<OperatorScope> signature)
        where TDeclaration : BaseMethodDeclarationSyntax
    {
        var scope = new OperatorScope();
        signature(scope);
        return AddMember(scope.Complete(declaration
            .WithAttributeLists(Lead.TakeAttributes())
            .WithModifiers(Lead.TakeModifiers())));
    }

    private ExplicitInterfaceSpecifierSyntax? TakeExplicitInterface()
    {
        return Lead.TakeExplicitInterface() is { } name ? ExplicitInterfaceSpecifier(name) : null;
    }

    private protected static VariableDeclaratorSyntax BuildVariable(string name, Expr? value)
    {
        var declarator = VariableDeclarator(Names.CreateIdentifier(name));
        return value?.Node is { } node ? declarator.WithInitializer(EqualsValueClause(node)) : declarator;
    }
}

internal sealed class TypeScope : MemberScope<TypeScope>
{
    private readonly string _name = default!;

    internal TypeScope(string name) => _name = name;

    private protected override TypeScope Next => this;

    public TypeScope Const(bool when = true) => AddModifier(SyntaxKind.ConstKeyword, when);
    public TypeScope Volatile(bool when = true) => AddModifier(SyntaxKind.VolatileKeyword, when);
    public TypeScope Required(bool when = true) => AddModifier(SyntaxKind.RequiredKeyword, when);

    public TypeScope Inherits(TypeRef type, bool when = true) => AddBaseType(type, when);

    public TypeScope Field(TypeRef type, string name, Expr? value = null, bool when = true)
    {
        if (when is false || type.Node is null) return Skip();

        var (modifiers, fieldType) = Declarations.SplitRef(Lead.TakeModifiers(), type.Node);
        var variable = SingletonSeparatedList(BuildVariable(name, value));
        return AddMember(FieldDeclaration(VariableDeclaration(fieldType, variable))
            .WithAttributeLists(Lead.TakeAttributes())
            .WithModifiers(modifiers));
    }

    public TypeScope Constructor(Action<ConstructorScope> signature, bool when = true)
    {
        if (when is false) return Skip();

        var declaration = ConstructorDeclaration(Names.CreateIdentifier(_name))
            .WithAttributeLists(Lead.TakeAttributes())
            .WithModifiers(Lead.TakeModifiers());

        var scope = new ConstructorScope();
        signature(scope);
        return AddMember(scope.Complete(declaration));
    }
}

internal sealed class InterfaceScope : MemberScope<InterfaceScope>
{
    private protected override InterfaceScope Next => this;
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

        var member = EnumMemberDeclaration(Names.CreateIdentifier(name));
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
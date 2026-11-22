using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using static Microsoft.CodeAnalysis.CSharp.SyntaxFactory;

namespace Shared.CodeGeneration.Chained;

internal sealed class Constraint
{
    private Constraint(TypeParameterConstraintSyntax? node) => Node = node;

    public static Constraint Class { get; } = new(ClassOrStructConstraint(SyntaxKind.ClassConstraint));
    public static Constraint Struct { get; } = new(ClassOrStructConstraint(SyntaxKind.StructConstraint));
    public static Constraint Unmanaged { get; } = new(TypeConstraint(IdentifierName("unmanaged")));
    public static Constraint NotNull { get; } = new(TypeConstraint(IdentifierName("notnull")));
    public static Constraint New { get; } = new(ConstructorConstraint());

    internal TypeParameterConstraintSyntax? Node { get; }

    public static implicit operator Constraint(TypeRef type)
    {
        return new(type.Node is { } node ? TypeConstraint(node) : null);
    }
}
using System.Collections.Generic;
using System.Linq;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using static Microsoft.CodeAnalysis.CSharp.SyntaxFactory;

namespace Shared.CodeGeneration.Scoped.Rendering;

internal static class Declarations
{
    public static (SyntaxTokenList Modifiers, TypeSyntax Type) SplitRef(SyntaxTokenList modifiers, TypeSyntax type)
    {
        var index = modifiers.IndexOf(SyntaxKind.RefKeyword);
        if (index < 0) return (modifiers, type);

        var refType = RefType(type);
        var next = index + 1;
        var rest = modifiers.Skip(next);

        if (next < modifiers.Count && modifiers[next].IsKind(SyntaxKind.ReadOnlyKeyword))
        {
            refType = refType.WithReadOnlyKeyword(modifiers[next]);
            rest = rest.Skip(1);
        }

        return (TokenList(modifiers.Take(index).Concat(rest)), refType);
    }

    public static TypeParameterSyntax BuildTypeParameter(string name, Variance variance)
    {
        var parameter = SyntaxFactory.TypeParameter(Names.CreateIdentifier(name));
        return variance switch
        {
            Variance.In => parameter.WithVarianceKeyword(Token(SyntaxKind.InKeyword)),
            Variance.Out => parameter.WithVarianceKeyword(Token(SyntaxKind.OutKeyword)),
            _ => parameter
        };
    }

    public static TypeParameterConstraintClauseSyntax? BuildConstraints(string name,
        IReadOnlyList<Constraint> constraints)
    {
        var nodes = constraints.Where(static constraint => constraint.Node is not null)
            .Select(static constraint => constraint.Node!)
            .ToArray();
        return nodes.Length == 0
            ? null
            : TypeParameterConstraintClause(IdentifierName(Names.CreateIdentifier(name)), SeparatedList(nodes));
    }

    public static AttributeSyntax BuildAttribute(string name, IReadOnlyList<Arg>? arguments)
    {
        var nodes = arguments?.Where(static argument => argument.IsEmpty is false)
            .Select(static argument => argument.ToAttributeArgument())
            .ToArray() ?? [];

        var attribute = SyntaxFactory.Attribute(Names.Qualify(name));
        return nodes.Length == 0 ? attribute : attribute.WithArgumentList(AttributeArgumentList(SeparatedList(nodes)));
    }
}
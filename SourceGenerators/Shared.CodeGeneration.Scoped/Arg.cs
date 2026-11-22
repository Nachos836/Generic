using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Shared.CodeGeneration.Scoped.Rendering;
using static Microsoft.CodeAnalysis.CSharp.SyntaxFactory;

namespace Shared.CodeGeneration.Scoped;

internal sealed class Arg
{
    private readonly Expr _value;
    private readonly SyntaxKind _refKind;
    private readonly string? _name;

    internal Arg(Expr value, SyntaxKind refKind = SyntaxKind.None, string? name = null)
    {
        _value = value;
        _refKind = refKind;
        _name = name;
    }

    internal static Arg Empty { get; } = new(Expr.Empty);
    internal bool IsEmpty => _value.IsEmpty;

    public static implicit operator Arg(Expr value) => new(value);

    internal Arg Named(string name) => new(_value, _refKind, name);

    internal ArgumentSyntax ToArgument()
    {
        var argument = Argument(_value.Node!);
        if (_refKind != SyntaxKind.None) argument = argument.WithRefKindKeyword(Token(_refKind));
        return _name == default ? argument : argument.WithNameColon(NameColon(IdentifierName(Names.Identifier(_name))));
    }

    internal AttributeArgumentSyntax ToAttributeArgument()
    {
        if (_name != default)
        {
            return AttributeArgument(null, NameColon(IdentifierName(Names.Identifier(_name))), _value.Node!);
        }

        return _value.Node is AssignmentExpressionSyntax { Left: IdentifierNameSyntax property } assignment
            ? AttributeArgument(NameEquals(property), null, assignment.Right)
            : AttributeArgument(_value.Node!);
    }
}
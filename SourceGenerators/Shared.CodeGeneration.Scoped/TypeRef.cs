using System.Collections.Generic;
using System.Linq;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Shared.CodeGeneration.Scoped.Rendering;
using static Microsoft.CodeAnalysis.CSharp.SyntaxFactory;

namespace Shared.CodeGeneration.Scoped;

internal sealed class TypeRef
{
    internal TypeRef(TypeSyntax? node) => Node = node;

    internal static TypeRef Empty { get; } = new(null);
    internal TypeSyntax? Node { get; }
    internal bool IsEmpty => Node is null;

    public TypeRef Nested(string name, IReadOnlyList<TypeRef>? arguments = null, bool when = true)
    {
        if (when is false || Node is not NameSyntax outer) return this;
        return new(QualifiedName(outer, BuildSimpleName(name, arguments)));
    }

    public TypeRef Nullable(bool when = true) => when && Node is { } node ? new(NullableType(node)) : this;
    public TypeRef Pointer(bool when = true) => when && Node is { } node ? new(PointerType(node)) : this;

    public TypeRef Array(bool when = true)
    {
        if (when is false || Node is null) return this;
        return new(ArrayType(Node, SingletonList(ArrayRankSpecifier(
            SingletonSeparatedList<ExpressionSyntax>(OmittedArraySizeExpression())))));
    }

    internal static SimpleNameSyntax BuildSimpleName(string name, IReadOnlyList<TypeRef>? arguments)
    {
        var identifier = Names.CreateIdentifier(name);

        var nodes = arguments?.Where(static argument => argument.IsEmpty is false)
            .Select(static argument => argument.Node!).ToArray();
        return nodes is { Length: > 0 }
            ? GenericName(identifier, TypeArgumentList(SeparatedList(nodes)))
            : IdentifierName(identifier);
    }
}
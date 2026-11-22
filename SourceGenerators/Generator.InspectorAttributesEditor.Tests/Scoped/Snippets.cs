using Shared.CodeGeneration.Scoped;
using static Shared.CodeGeneration.Scoped.Blocks;

namespace Generator.InspectorAttributesEditor.Tests.Scoped;

internal static class Snippets
{
    public static string InType(Action<TypeScope> members) => Code.File(file => file.Class("C", members));
    public static string InBody(Action<BodyScope> statements) => InMethod(method => method.Body(statements));
    public static string OfExpression(Expr expression) => InBody(body => body.Do(expression));
    public static string OfType(TypeRef type) => InType(members => members.Field(type, "f"));

    public static string InMethod(Action<MethodScope> signature)
    {
        return InType(type => type.Method(T("void"), "M", signature));
    }
}
using Shared.CodeGeneration.Chained;
using static Shared.CodeGeneration.Chained.Blocks;
using Members = Shared.CodeGeneration.Chained.ClassChain<Shared.CodeGeneration.Chained.FileChain>;
using Signature = Shared.CodeGeneration.Chained.MethodChain<
    Shared.CodeGeneration.Chained.ClassChain<Shared.CodeGeneration.Chained.FileChain>>;
using Statements = Shared.CodeGeneration.Chained.MethodBodyChain<
    Shared.CodeGeneration.Chained.ClassChain<Shared.CodeGeneration.Chained.FileChain>>;

namespace Generator.InspectorAttributesEditor.Tests.Chained;

internal static class Snippets
{
    public static string InType(Func<Members, Members> members) => members(Code.File().Class("C")).EndClass().EndFile();
    public static string OfExpression(Expr expression) => InBody(body => body.Do(expression));
    public static string OfType(TypeRef type) => InType(members => members.Field(type, "f"));

    public static string InMethod(Func<Signature, Signature> signature)
    {
        return InType(type => signature(type.Method(T("void"), "M")).EndMethod());
    }

    public static string InBody(Func<Statements, Statements> statements)
    {
        return InType(type => statements(type.Method(T("void"), "M").Body()).EndMethod());
    }
}
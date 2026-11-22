using Microsoft.CodeAnalysis.CSharp;
using NUnit.Framework;
using Shared.CodeGeneration.Chained;
using Shared.CodeGeneration.Chained.Extensions;
using static Generator.InspectorAttributesEditor.Tests.Chained.Snippets;
using static Microsoft.CodeAnalysis.CSharp.SyntaxFactory;
using static Shared.CodeGeneration.Chained.Blocks;
using static Shared.CodeGeneration.Chained.Extensions.Nodes;

namespace Generator.InspectorAttributesEditor.Tests.Chained;

internal static class SampleBlocks
{
    public static TSelf Constant<TSelf>(this TypeChain<TSelf> type, string name, int value)
        where TSelf : TypeChain<TSelf>
    {
        return type.Public().Const().Field(T("int"), name, Lit(value));
    }

    public static TSelf Checkpoint<TSelf>(this StatementChain<TSelf> body, string label)
        where TSelf : StatementChain<TSelf>
    {
        return body.Statement(LabeledStatement(label, EmptyStatement()));
    }

    public static Expr Checked(Expr value)
    {
        return Expression(CheckedExpression(SyntaxKind.CheckedExpression, value.Node!));
    }

    public static TypeRef Nint() => Type(IdentifierName("nint"));
}

public sealed class ExtensionTests
{
    [TestCaseSource(nameof(Positive))]
    public void ExtensionBlockReadsLikeBuiltIn(Func<string> actual, string expected)
    {
        var code = actual();
        CodeAssert.AreEqual(expected, code);
        CodeAssert.Parses(code);
    }

    [TestCaseSource(nameof(FalseCondition))]
    public void FalseConditionWritesNothing(Func<string> actual, string expected)
    {
        CodeAssert.AreEqual(expected, actual());
    }

    private static IEnumerable<TestCaseData> Positive()
    {
        yield return Case("Type", static () => OfType(Type(PredefinedType(Token(SyntaxKind.ObjectKeyword))).Array()),
            Expected.OfType("object[]"));
        yield return Case("Expression", static () => OfExpression(Id("a").Assign(Expression(CheckedExpression(
                SyntaxKind.CheckedExpression, IdentifierName("b"))).Binary(BinaryOperator.Add, Lit(1)))),
            Expected.OfExpression("a = checked(b) + 1"));
        yield return Case("Statement", static () => InBody(static body => body.Comment("Jump").Ref()
                .Statement(GotoStatement(SyntaxKind.GotoStatement, IdentifierName("end"))).Var("x", Lit(1))),
            Expected.InBody("// Jump\ngoto end;\nvar x = 1;"));
        yield return Case("Member", static () => InType(static type => type.Gap().Attribute("A").Public()
                .Member(DelegateDeclaration(PredefinedType(Token(SyntaxKind.VoidKeyword)), "D"))
                .Field(T("int"), "f")),
            Expected.InType("[A]\npublic delegate void D();\nint f;"));
        yield return Case("MetaBlock", static () => InType(static type => type.Constant("A", 1).Constant("B", 2)
                .Private().Field(T("int"), "f")),
            Expected.InType("public const int A = 1;\npublic const int B = 2;\nprivate int f;"));
        yield return Case("StatementBlock", static () => InBody(static body => body.Checkpoint("start").Return()),
            Expected.InBody("start:\n    ;\nreturn;"));
        yield return Case("ValueBlocks", static () => InType(static type => type.Field(SampleBlocks.Nint(), "n",
            SampleBlocks.Checked(Id("x")))), Expected.InType("nint n = checked(x);"));
    }

    private static IEnumerable<TestCaseData> FalseCondition()
    {
        yield return Case("Type", static () => InType(static type => type.Field(Type(IdentifierName("A"), false),
            "f")), Expected.InType(""));
        yield return Case("Expression", static () => OfExpression(Id("f").Call([Expression(IdentifierName("a"),
            false)])), Expected.OfExpression("f()"));
        yield return Case("Statement", static () => InBody(static body => body.Comment("Keep").Ref()
                .Statement(BreakStatement(), false).Var("x", Lit(1))),
            Expected.InBody("// Keep\nvar x = 1;"));
        yield return Case("Member", static () => InType(static type => type.Comment("Keep").Public()
                .Member(DelegateDeclaration(PredefinedType(Token(SyntaxKind.VoidKeyword)), "D"), false)
                .Field(T("int"), "f")),
            Expected.InType("// Keep\nint f;"));
    }

    private static TestCaseData Case(string block, Func<string> actual, string expected)
    {
        return new TestCaseData(actual, Expected.ConvertToTabs(expected)).SetArgDisplayNames(block);
    }
}

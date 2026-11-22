using Microsoft.CodeAnalysis;
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

    public static TSelf GoTo<TSelf>(this StatementChain<TSelf> body, string label, bool when = true)
        where TSelf : StatementChain<TSelf>
    {
        return body.Statement(GotoStatement(SyntaxKind.GotoStatement, IdentifierName(label)), when);
    }

    public static TSelf VoidDelegate<TSelf>(this DeclarationChain<TSelf> type, string name, bool when = true)
        where TSelf : DeclarationChain<TSelf>
    {
        return type.Member(DelegateDeclaration(PredefinedType(Token(SyntaxKind.VoidKeyword)), name), when);
    }

    public static StatementBlockChain<TSelf> CheckedBlock<TSelf>(this StatementChain<TSelf> body, bool when = true)
        where TSelf : StatementChain<TSelf>
    {
        return body.Statement(static block => CheckedStatement(SyntaxKind.CheckedStatement, block), when);
    }

    public static TSelf CommentedDelegate<TSelf>(this DeclarationChain<TSelf> type, string name)
        where TSelf : DeclarationChain<TSelf>
    {
        return type.Member(DelegateDeclaration(PredefinedType(Token(SyntaxKind.VoidKeyword)), name)
            .WithLeadingTrivia(Comment("// Own"), LineFeed));
    }

    public static TSelf UnsafeDelegate<TSelf>(this DeclarationChain<TSelf> type, string name)
        where TSelf : DeclarationChain<TSelf>
    {
        return type.Member(DelegateDeclaration(PredefinedType(Token(SyntaxKind.VoidKeyword)), name)
            .AddAttributeLists(AttributeList(SingletonSeparatedList(Attribute(IdentifierName("Own")))))
            .AddModifiers(Token(SyntaxKind.UnsafeKeyword)));
    }

    public static Expr Checked(Expr value, bool when = true)
    {
        return Expression(value, static node => CheckedExpression(SyntaxKind.CheckedExpression, node), when);
    }

    public static Expr Await(Expr value) => Expression(value, static node => AwaitExpression(node));

    public static Expr FromEnd(Expr value)
    {
        return Expression(value, static node => PrefixUnaryExpression(SyntaxKind.IndexExpression, node));
    }

    public static TypeRef ObjectType(bool when = true) => Type(PredefinedType(Token(SyntaxKind.ObjectKeyword)), when);
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
        yield return Case("Type", static () => OfType(SampleBlocks.ObjectType().Array()), Expected.OfType("object[]"));
        yield return Case("Expression", static () => OfExpression(Id("a").Assign(SampleBlocks.Checked(Id("b"))
            .Binary(BinaryOperator.Add, Lit(1)))), Expected.OfExpression("a = checked(b) + 1"));
        yield return Case("Statement", static () => InBody(static body => body.Comment("Jump").Ref().GoTo("end")
            .Var("x", Lit(1))), Expected.InBody("// Jump\ngoto end;\nvar x = 1;"));
        yield return Case("Member", static () => InType(static type => type.Gap().Attribute("A").Public()
            .VoidDelegate("D").Field(T("int"), "f")), Expected.InType("[A]\npublic delegate void D();\nint f;"));
        yield return Case("MetaBlock", static () => InType(static type => type.Constant("A", 1).Constant("B", 2)
                .Private().Field(T("int"), "f")),
            Expected.InType("public const int A = 1;\npublic const int B = 2;\nprivate int f;"));
        yield return Case("StatementBlock", static () => InBody(static body => body.Checkpoint("start").Return()),
            Expected.InBody("start:\n    ;\nreturn;"));
        yield return Case("ValueBlocks", static () => InType(static type => type.Field(SampleBlocks.Nint(), "n",
            SampleBlocks.Checked(Id("x")))), Expected.InType("nint n = checked(x);"));
        yield return Case("AwaitBeforeMember", static () => OfExpression(Id("a").Assign(SampleBlocks.Await(Id("t"))
            .Dot("Result"))), Expected.OfExpression("a = (await t).Result"));
        yield return Case("IndexBeforeMember", static () => OfExpression(Id("a").Assign(SampleBlocks.FromEnd(Id("i"))
            .Dot("Value"))), Expected.OfExpression("a = (^i).Value"));
        yield return Case("StatementWithBody", static () => InBody(static body => body.Comment("Keep").CheckedBlock()
                .Return().EndStatement().Break()),
            Expected.InBody("// Keep\nchecked\n{\n    return;\n}\n\nbreak;"));
        yield return Case("MemberOwnTrivia", static () => InType(static type => type.Comment("Lead").Attribute("X")
            .Public().CommentedDelegate("D")), Expected.InType("// Lead\n// Own\n[X]\npublic delegate void D();"));
        yield return Case("MemberOwnPrefixes", static () => InType(static type => type.Attribute("X").Public()
            .UnsafeDelegate("D")), Expected.InType("[X]\n[Own]\npublic unsafe delegate void D();"));
        yield return Case("GapBeforeMember", static () => InType(static type => type.Field(T("int"), "f").Gap()
            .VoidDelegate("D")), Expected.InType("int f;\n\ndelegate void D();"));
        yield return Case("GapBeforeStatement", static () => InBody(static body => body.Break().Gap().GoTo("end")),
            Expected.InBody("break;\n\ngoto end;"));
        yield return Case("MemberInFile", static () => Code.File().Public().VoidDelegate("D").Namespace("N")
                .Internal().VoidDelegate("E").EndNamespace().EndFile(),
            "public delegate void D();\nnamespace N\n{\n    internal delegate void E();\n}");
        yield return Case("StatementInBranches", static () => InBody(static body => body
                .If(Id("a")).GoTo("end").Else().GoTo("start").EndIf()
                .Try().GoTo("retry").Finally().GoTo("done").EndTry()),
            Expected.InBody("if (a)\n{\n    goto end;\n}\nelse\n{\n    goto start;\n}\n\ntry\n{\n    goto retry;\n}\n" +
                "finally\n{\n    goto done;\n}"));
    }

    private static IEnumerable<TestCaseData> FalseCondition()
    {
        yield return Case("Type", static () => InType(static type => type.Field(SampleBlocks.ObjectType(false), "f")),
            Expected.InType(""));
        yield return Case("Expression", static () => OfExpression(Id("f").Call([SampleBlocks.Checked(Id("a"),
            false)])), Expected.OfExpression("f()"));
        yield return Case("Statement", static () => InBody(static body => body.Comment("Keep").Ref().GoTo("end", false)
            .Var("x", Lit(1))), Expected.InBody("// Keep\nvar x = 1;"));
        yield return Case("Member", static () => InType(static type => type.Comment("Keep").Public()
            .VoidDelegate("D", false).Field(T("int"), "f")), Expected.InType("// Keep\nint f;"));
        yield return Case("WrappedEmptyValue", static () => OfExpression(Id("f").Call([SampleBlocks.Checked(Id("a",
            false))])), Expected.OfExpression("f()"));
        yield return Case("StatementWithBody", static () => InBody(static body => body.Comment("Keep").Ref()
                .CheckedBlock(false).Return().EndStatement().Var("x", Lit(1))),
            Expected.InBody("// Keep\nvar x = 1;"));
    }

    private static TestCaseData Case(string block, Func<string> actual, string expected)
    {
        return new TestCaseData(actual, Expected.ConvertToTabs(expected)).SetArgDisplayNames(block);
    }
}

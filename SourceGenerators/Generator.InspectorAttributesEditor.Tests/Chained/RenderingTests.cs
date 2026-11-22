using NUnit.Framework;
using Shared.CodeGeneration.Chained;
using static Generator.InspectorAttributesEditor.Tests.Chained.Snippets;
using static Shared.CodeGeneration.Chained.BinaryOperator;
using static Shared.CodeGeneration.Chained.Blocks;

namespace Generator.InspectorAttributesEditor.Tests.Chained;

public sealed class RenderingTests
{
    [TestCaseSource(nameof(Cases))]
    public void RendersValidCSharp(Func<string> actual, string expected) => CodeAssert.AreEqual(expected, actual());

    [Test]
    public void GapAtFileStartWritesNoBlankLine()
    {
        Assert.That(Code.File().Gap().Class("A").EndClass().EndFile(), Does.StartWith("class A"));
    }

    [Test]
    public void DefineAfterUsingIsRejected()
    {
        Assert.Throws<InvalidOperationException>(static () => Code.File().Using("System").Define("X"));
    }

    [Test]
    public void ArmBeforeOmittedSeparatorHasNoTrailingSpace()
    {
        var text = OfExpression(Id("x").Switch()
            .IfDirective("A").Arm(Lit(1), Id("a")).ElseDirective().Arm(Lit(1), Id("b")).EndIfDirective().EndSwitch());

        Assert.That(text.Split('\n'), Has.None.EndsWith(" "));
    }

    private static IEnumerable<TestCaseData> Cases()
    {
        yield return Case("DoubleWholeNumber", static () => OfExpression(Lit(1.0)), Expected.OfExpression("1D"));
        yield return Case("DoubleFraction", static () => OfExpression(Lit(1.5)), Expected.OfExpression("1.5"));
        yield return Case("Float", static () => OfExpression(Lit(2.0f)), Expected.OfExpression("2F"));
        yield return Case("FloatNaN", static () => OfExpression(Lit(float.NaN)), Expected.OfExpression("float.NaN"));
        yield return Case("DoubleInfinity", static () => OfExpression(Lit(double.NegativeInfinity)),
            Expected.OfExpression("double.NegativeInfinity"));
        yield return Case("NegativeReceiver", static () => OfExpression(Lit(-1).Dot("CompareTo").Call([Id("x")])),
            Expected.OfExpression("(-1).CompareTo(x)"));
        yield return Case("CastOfNegative", static () => OfExpression(Cast(T("E"), Lit(-1))),
            Expected.OfExpression("(E)(-1)"));
        yield return Case("CastOfAddress", static () => OfExpression(Cast(T("P"), AddressOf(Id("x")))),
            Expected.OfExpression("(P)(&x)"));
        yield return Case("UnbracedIfKeepsElse", static () => InBody(static body => body
                .If(Id("a"), braced: false).If(Id("b"), braced: false).Return().EndIf()
                .Else().Break().EndIf()),
            Expected.InBody("if (a)\n{\n    if (b)\n        return;\n}\nelse\n{\n    break;\n}"));
        yield return Case("IsWithBinaryPattern", static () => OfExpression(Id("x").Is(Id("A").Binary(Or, Id("B")))),
            Expected.OfExpression("x is (A || B)"));
        yield return Case("IfDirectiveKeyword", static () => Code.File().IfDirective("true").Class("A").EndClass()
            .EndIfDirective().EndFile(), "#if true\nclass A\n{\n}\n#endif");
        yield return Case("GapAfterOpenBrace", static () => InType(static type => type.Gap().Field(T("int"), "a")),
            Expected.InType("int a;"));
        yield return Case("ExplicitForStaysOnItsMember", static () => InType(static type => type
                .ExplicitFor(T("I")).Field(T("int"), "f").Method(T("void"), "M").EndMethod()),
            Expected.InType("int f;\nvoid M();"));
        yield return Case("PrefixesStayBeforeNamespace", static () => Code.File().Public().Attribute("A")
                .Namespace("N").Class("Inner").EndClass().EndNamespace().Class("Outer").EndClass().EndFile(),
            "namespace N\n{\n    class Inner\n    {\n    }\n}\n\nclass Outer\n{\n}");
        yield return Case("PrefixesStayBeforeUsing", static () => Code.File().Public().Using("System").Class("A")
            .EndClass().EndFile(), "using System;\n\nclass A\n{\n}");
        yield return Case("StatementPrefixesStayBeforeStatement", static () => InBody(static body => body
            .Ref().Do(Id("a").Call()).Var("y", Id("z"))), Expected.InBody("a();\nvar y = z;"));
        yield return Case("DisabledTypeKeepsDirective", static () => Code.File().IfDirective("X").Class("A", false)
            .EndClass().EndIfDirective().Class("B").EndClass().EndFile(), "#if X\n#endif\nclass B\n{\n}");
        yield return Case("DisabledMethodKeepsComment", static () => InType(static type => type.Comment("Keep")
            .Method(T("void"), "M", false).EndMethod().Field(T("int"), "f")), Expected.InType("// Keep\nint f;"));
        yield return Case("DisabledNamespaceKeepsDocComment", static () => Code.File().DocComment(Lines("Keep"))
            .Namespace("N", false).EndNamespace().Class("A").EndClass().EndFile(), "/// Keep\nclass A\n{\n}");
        yield return Case("DisabledConstructorKeepsGap", static () => InType(static type => type.Field(T("int"), "x")
            .Gap().Constructor(false).EndConstructor().Field(T("int"), "y")), Expected.InType("int x;\n\nint y;"));
        yield return Case("DisabledIfKeepsGap", static () => InBody(static body => body.Break()
            .Gap().If(Id("a"), when: false).Return().EndIf().Return()), Expected.InBody("break;\n\nreturn;"));
        yield return Case("DisabledForeachKeepsComment", static () => InBody(static body => body.Comment("Keep")
            .Foreach("i", Id("items"), false).EndForeach().Break()), Expected.InBody("// Keep\nbreak;"));
        yield return Case("DisabledSwitchKeepsComment", static () => InBody(static body => body.Comment("Keep")
            .Switch(Id("x"), false).EndSwitch().Break()), Expected.InBody("// Keep\nbreak;"));
        yield return Case("DisabledFixedKeepsComment", static () => InBody(static body => body.Comment("Keep")
                .Fixed(T("void").Pointer(), "p", AddressOf(Id("x")), false).EndFixed().Break()),
            Expected.InBody("// Keep\nbreak;"));
        yield return Case("DisabledCaseKeepsDirective", static () => InBody(static body => body.Switch(Id("x"))
                .IfDirective("X").Case(Lit(1), false).Break().EndCase().EndIfDirective().Default().Break().EndCase()
                .EndSwitch()),
            "class C\n{\n    void M()\n    {\n        switch (x)\n        {\n#if X\n#endif\n            default:\n" +
            "                break;\n        }\n    }\n}");
        yield return Case("DisabledDefaultKeepsItsDirectives", static () => InBody(static body => body.Switch(Id("x"))
                .Case(Lit(1)).Break().EndCase().Default(false).IfDirective("X").Break().EndIfDirective().EndCase()
                .EndSwitch()),
            Expected.InBody("switch (x)\n{\n    case 1:\n        break;\n}"));
        yield return Case("DisabledCaseWritesNoComment", static () => InBody(static body => body.Switch(Id("x"))
                .Case(Lit(1), false).Break().Comment("Inside").EndCase().Default().Break().EndCase().EndSwitch()),
            Expected.InBody("switch (x)\n{\n    default:\n        break;\n}"));
        yield return Case("DisabledCaseWritesNoModifier", static () => InBody(static body => body.Switch(Id("x"))
                .Case(Lit(1), false).Ref().EndCase().Case(Lit(2)).Var("y", Lit(1)).EndCase().EndSwitch()),
            Expected.InBody("switch (x)\n{\n    case 2:\n        var y = 1;\n}"));
        yield return Case("CasePrefixesStayInCase", static () => InBody(static body => body.Switch(Id("x"))
                .Case(Lit(1)).Break().Ref().EndCase().Case(Lit(2)).Var("y", Lit(1)).EndCase().EndSwitch()),
            Expected.InBody("switch (x)\n{\n    case 1:\n        break;\n    case 2:\n        var y = 1;\n}"));
    }

    private static TestCaseData Case(string name, Func<string> actual, string expected)
    {
        return new TestCaseData(actual, Expected.ConvertToTabs(expected)).SetArgDisplayNames(name);
    }
}
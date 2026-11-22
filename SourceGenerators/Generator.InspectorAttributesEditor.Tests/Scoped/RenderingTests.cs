using NUnit.Framework;
using Shared.CodeGeneration.Scoped;
using static Generator.InspectorAttributesEditor.Tests.Scoped.Snippets;
using static Shared.CodeGeneration.Scoped.BinaryOperator;
using static Shared.CodeGeneration.Scoped.Blocks;

namespace Generator.InspectorAttributesEditor.Tests.Scoped;

public sealed class RenderingTests
{
    [TestCaseSource(nameof(Cases))]
    public void RendersValidCSharp(Func<string> actual, string expected) => CodeAssert.AreEqual(expected, actual());

    [Test]
    public void GapAtFileStartWritesNoBlankLine()
    {
        Assert.That(Code.File(static file => file.Gap().Class("A", static _ => { })), Does.StartWith("class A"));
    }

    [Test]
    public void DefineAfterUsingIsRejected()
    {
        Assert.Throws<InvalidOperationException>(static () =>
            Code.File(static file => file.Using("System").Define("X")));
    }

    [Test]
    public void ArmBeforeOmittedSeparatorHasNoTrailingSpace()
    {
        var text = OfExpression(Id("x").Switch(static arms => arms
            .IfDirective("A").Arm(Lit(1), Id("a")).ElseDirective().Arm(Lit(1), Id("b")).EndIfDirective()));

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
                .If(Id("a"), static then => then.If(Id("b"), static inner => inner.Return(), braced: false),
                    braced: false)
                .Else(static other => other.Break())),
            Expected.InBody("if (a)\n{\n    if (b)\n        return;\n}\nelse\n{\n    break;\n}"));
        yield return Case("IsWithBinaryPattern", static () => OfExpression(Id("x").Is(Id("A").Binary(Or, Id("B")))),
            Expected.OfExpression("x is (A || B)"));
        yield return Case("IfDirectiveKeyword", static () => Code.File(static file => file.IfDirective("true")
            .Class("A", static _ => { }).EndIfDirective()), "#if true\nclass A\n{\n}\n#endif");
        yield return Case("GapAfterOpenBrace", static () => InType(static type => type.Gap().Field(T("int"), "a")),
            Expected.InType("int a;"));
        yield return Case("ExplicitForStaysOnItsMember", static () => InType(static type => type
                .ExplicitFor(T("I")).Field(T("int"), "f").Method(T("void"), "M", static _ => { })),
            Expected.InType("int f;\nvoid M();"));
        yield return Case("PrefixesStayBeforeNamespace", static () => Code.File(static file => file
            .Public().Attribute("A").Namespace("N", static scope => scope.Class("Inner", static _ => { }))
            .Class("Outer", static _ => { })), "namespace N\n{\n    class Inner\n    {\n    }\n}\n\nclass Outer\n{\n}");
        yield return Case("PrefixesStayBeforeUsing", static () => Code.File(static file => file
            .Public().Using("System").Class("A", static _ => { })), "using System;\n\nclass A\n{\n}");
        yield return Case("StatementPrefixesStayBeforeStatement", static () => InBody(static body => body
            .Ref().Do(Id("a").Call()).Var("y", Id("z"))), Expected.InBody("a();\nvar y = z;"));
        yield return Case("CasePrefixesStayInCase", static () => InBody(static body => body.Switch(Id("x"),
                static sections => sections.Case(Lit(1), static section => section.Break().Ref())
                    .Case(Lit(2), static section => section.Var("y", Lit(1))))),
            Expected.InBody("switch (x)\n{\n    case 1:\n        break;\n    case 2:\n        var y = 1;\n}"));
    }

    private static TestCaseData Case(string name, Func<string> actual, string expected)
    {
        return new TestCaseData(actual, Expected.ConvertToTabs(expected)).SetArgDisplayNames(name);
    }
}
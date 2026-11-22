using NUnit.Framework;
using Shared.CodeGeneration.Scoped;
using static Generator.InspectorAttributesEditor.Tests.Scoped.Snippets;
using static Shared.CodeGeneration.Scoped.BinaryOperator;
using static Shared.CodeGeneration.Scoped.Blocks;

namespace Generator.InspectorAttributesEditor.Tests.Scoped;

public sealed class RenderingTests
{
    [TestCaseSource(nameof(Cases))]
    public void RendersValidCSharp(Func<string> actual, string expected)
    {
        var code = actual();
        CodeAssert.AreEqual(expected, code);
        CodeAssert.Parses(code);
    }

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
        yield return Case("CoalesceGroupsRight", static () => OfExpression(Id("a").Coalesce(Id("b").Coalesce(Id("c")))),
            Expected.OfExpression("a ?? b ?? c"));
        yield return Case("CoalesceOfCoalesce", static () => OfExpression(Id("a").Coalesce(Id("b")).Coalesce(Id("c"))),
            Expected.OfExpression("(a ?? b) ?? c"));
        yield return Case("TernaryOfTernary", static () => OfExpression(Id("a").Ternary(Id("b"), Id("c"))
            .Ternary(Id("d"), Id("e"))), Expected.OfExpression("(a ? b : c) ? d : e"));
        yield return Case("AsOfCoalesce", static () => OfExpression(Id("a").Coalesce(Id("b")).As(T("C"))),
            Expected.OfExpression("(a ?? b) as C"));
        yield return Case("MinusOfNegative", static () => OfExpression(Unary(UnaryOperator.Minus, Lit(-1))),
            Expected.OfExpression("-(-1)"));
        yield return Case("MinusOfMinus", static () => OfExpression(Unary(UnaryOperator.Minus,
            Unary(UnaryOperator.Minus, Id("a")))), Expected.OfExpression("-(-a)"));
        yield return Case("NotOfBinary", static () => OfExpression(Not(Id("a").Binary(And, Id("b")))),
            Expected.OfExpression("!(a && b)"));
        yield return Case("NotPatternOfBinary", static () => OfExpression(Id("x").Is(Not(Id("A").Binary(Or, Id("B"))))),
            Expected.OfExpression("x is not (A || B)"));
        yield return Case("ConversionHasNoSpaceBeforeParameters", static () => InType(static type => type
                .Public().Static().Conversion(ConversionKind.Explicit, T("C"), static signature => signature
                    .Param(T("int"), "x").Arrow(New(T("C"))))),
            Expected.InType("public static explicit operator C(int x) => new C();"));
        yield return Case("BlankLineFollowsStatementBlock", static () => InBody(static body => body
                .If(Id("a"), static then => then.Return()).Break()),
            Expected.InBody("if (a)\n{\n    return;\n}\n\nbreak;"));
        yield return Case("DisabledRecordKeepsComment", static () => InType(static type => type.Comment("Keep")
            .Public().Record("R", static _ => { }, false).Field(T("int"), "f")), Expected.InType("// Keep\nint f;"));
        yield return Case("DisabledPropertyKeepsGap", static () => InType(static type => type.Field(T("int"), "x")
                .Gap().Public().Property(T("int"), "P", static accessors => accessors.Get(), when: false)
                .Field(T("int"), "y")),
            Expected.InType("int x;\n\nint y;"));
        yield return Case("DisabledIndexerKeepsComment", static () => InType(static type => type.Comment("Keep")
                .Public().Indexer(T("int"), static indexer => indexer.Get(), false).Field(T("int"), "f")),
            Expected.InType("// Keep\nint f;"));
        yield return Case("DisabledOperatorKeepsComment", static () => InType(static type => type.Comment("Keep")
                .Public().Operator(T("C"), Add, static _ => { }, false).Field(T("int"), "f")),
            Expected.InType("// Keep\nint f;"));
        yield return Case("DisabledConversionKeepsComment", static () => InType(static type => type.Comment("Keep")
                .Public().Conversion(ConversionKind.Implicit, T("int"), static _ => { }, false).Field(T("int"), "f")),
            Expected.InType("// Keep\nint f;"));
        yield return Case("DisabledLoopsKeepComments", static () => InBody(static body => body
                .Comment("For").Ref().For("i", Lit(0), Id("c"), Id("s"), static _ => { }, false)
                .Comment("While").While(Id("a"), static _ => { }, false)
                .Comment("DoWhile").DoWhile(static _ => { }, Id("a"), false)
                .Var("x", Lit(1))),
            Expected.InBody("// For\n// While\n// DoWhile\nvar x = 1;"));
        yield return Case("DisabledBlocksKeepComments", static () => InBody(static body => body
                .Comment("Using").Using("s", Id("x"), static _ => { }, false)
                .Comment("Lock").Lock(Id("x"), static _ => { }, false)
                .Comment("Unsafe").Unsafe(static _ => { }, false)
                .Comment("Try").Ref().Try(static _ => { }, false).Finally(static _ => { })
                .Var("x", Lit(1))),
            Expected.InBody("// Using\n// Lock\n// Unsafe\n// Try\nvar x = 1;"));
        yield return Case("IndexerParamDropsPrefixes", static () => InType(static type => type.Indexer(T("int"),
                static indexer => indexer.Private().Param(T("int"), "i", false).Private().Param(T("int"), "j")
                    .Get(Id("j")).Set())),
            Expected.InType("int this[int j] { get => j; set; }"));
        yield return Case("OperatorExplicitFor", static () => InType(static type => type.Static()
                .ExplicitFor(T("I", [T("C")])).Operator(T("C"), Add, static signature => signature
                    .Param(T("C"), "a").Param(T("C"), "b").Arrow(Id("a")))),
            Expected.InType("static C I<C>.operator +(C a, C b) => a;"));
        yield return Case("ConversionExplicitFor", static () => InType(static type => type.Static()
                .ExplicitFor(T("I")).Conversion(ConversionKind.Explicit, T("int"), static signature => signature
                    .Param(T("C"), "c").Arrow(Lit(0)))),
            Expected.InType("static explicit I.operator int(C c) => 0;"));
        yield return Case("CoalesceWithThrow", static () => OfExpression(Id("a").Assign(Id("b")
            .Coalesce(ThrowExpression(New(T("E")))))), Expected.OfExpression("a = b ?? throw new E()"));
        yield return Case("AsBeforeLess", static () => OfExpression(Id("a").Assign(Id("o").As(T("C"))
            .Binary(Less, Id("y")))), Expected.OfExpression("a = (o as C) < y"));
        yield return Case("BlankLineFollowsMemberBlock", static () => InType(static type => type
                .Method(T("void"), "M", static method => method.Body(static _ => { })).Field(T("int"), "f")),
            Expected.InType("void M()\n{\n}\n\nint f;"));
        yield return Case("NoBlankLineBeforeElse", static () => InBody(static body => body
                .If(Id("a"), static then => then.Return()).Else(static otherwise => otherwise.Break())),
            Expected.InBody("if (a)\n{\n    return;\n}\nelse\n{\n    break;\n}"));
    }

    [Test]
    public void CompoundAssignWithoutCompoundFormIsRejected()
    {
        Assert.Throws<ArgumentOutOfRangeException>(static () => Id("a").CompoundAssign(Equal, Id("b")));
    }

    [Test]
    public void OperatorThatCannotBeOverloadedIsRejected()
    {
        Assert.Throws<ArgumentOutOfRangeException>(static () => InType(static type => type
            .Operator(T("bool"), And, static _ => { })));
    }

    private static TestCaseData Case(string name, Func<string> actual, string expected)
    {
        return new TestCaseData(actual, Expected.ConvertToTabs(expected)).SetArgDisplayNames(name);
    }
}
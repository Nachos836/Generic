using NUnit.Framework;
using Shared.CodeGeneration.Chained;
using static Generator.InspectorAttributesEditor.Tests.Chained.Snippets;
using static Shared.CodeGeneration.Chained.BinaryOperator;
using static Shared.CodeGeneration.Chained.Blocks;

namespace Generator.InspectorAttributesEditor.Tests.Chained;

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
        yield return Case("CoalesceGroupsRight", static () => OfExpression(Id("a").Assign(Id("b")
            .Coalesce(Id("c").Coalesce(Id("d"))))), Expected.OfExpression("a = b ?? c ?? d"));
        yield return Case("CoalesceOfCoalesce", static () => OfExpression(Id("a").Assign(Id("b").Coalesce(Id("c"))
            .Coalesce(Id("d")))), Expected.OfExpression("a = (b ?? c) ?? d"));
        yield return Case("TernaryOfTernary", static () => OfExpression(Id("x").Assign(Id("a").Ternary(Id("b"),
            Id("c")).Ternary(Id("d"), Id("e")))), Expected.OfExpression("x = (a ? b : c) ? d : e"));
        yield return Case("AsOfCoalesce", static () => OfExpression(Id("x").Assign(Id("a").Coalesce(Id("b"))
            .As(T("C")))), Expected.OfExpression("x = (a ?? b) as C"));
        yield return Case("MinusOfNegative", static () => OfExpression(Id("x").Assign(Unary(UnaryOperator.Minus,
            Lit(-1)))), Expected.OfExpression("x = -(-1)"));
        yield return Case("MinusOfMinus", static () => OfExpression(Id("x").Assign(Unary(UnaryOperator.Minus,
            Unary(UnaryOperator.Minus, Id("a"))))), Expected.OfExpression("x = -(-a)"));
        yield return Case("NotOfBinary", static () => OfExpression(Id("x").Assign(Not(Id("a").Binary(And, Id("b"))))),
            Expected.OfExpression("x = !(a && b)"));
        yield return Case("NotPatternOfBinary", static () => OfExpression(Id("x").Assign(Id("y")
            .Is(Not(Id("A").Binary(Or, Id("B")))))), Expected.OfExpression("x = y is not (A || B)"));
        yield return Case("CoalesceWithThrow", static () => OfExpression(Id("a").Assign(Id("b")
            .Coalesce(ThrowExpression(New(T("E")))))), Expected.OfExpression("a = b ?? throw new E()"));
        yield return Case("AsBeforeLess", static () => OfExpression(Id("a").Assign(Id("o").As(T("C"))
            .Binary(Less, Id("y")))), Expected.OfExpression("a = (o as C) < y"));
        yield return Case("ConversionHasNoSpaceBeforeParameters", static () => InType(static type => type.Public()
                .Static().Conversion(ConversionKind.Explicit, T("C")).Param(T("int"), "x").Arrow(New(T("C")))
                .EndOperator()),
            Expected.InType("public static explicit operator C(int x) => new C();"));
        yield return Case("OperatorExplicitFor", static () => InType(static type => type.Static()
                .ExplicitFor(T("I", [T("C")])).Operator(T("C"), Add).Param(T("C"), "a").Param(T("C"), "b")
                .Arrow(Id("a")).EndOperator()),
            Expected.InType("static C I<C>.operator +(C a, C b) => a;"));
        yield return Case("ConversionExplicitFor", static () => InType(static type => type.Static()
                .ExplicitFor(T("I")).Conversion(ConversionKind.Explicit, T("int")).Param(T("C"), "c").Arrow(Lit(0))
                .EndOperator()),
            Expected.InType("static explicit I.operator int(C c) => 0;"));
        yield return Case("IndexerParamDropsPrefixes", static () => InType(static type => type.Indexer(T("int"))
                .Private().Param(T("int"), "i", false).Private().Param(T("int"), "j").Get(Id("j")).Set()
                .EndIndexer()),
            Expected.InType("int this[int j] { get => j; set; }"));
        yield return Case("BlankLineFollowsStatementBlock", static () => InBody(static body => body.If(Id("a"))
            .Return().EndIf().Break()), Expected.InBody("if (a)\n{\n    return;\n}\n\nbreak;"));
        yield return Case("BlankLineFollowsMemberBlock", static () => InType(static type => type
                .Method(T("void"), "M").Body().EndMethod().Field(T("int"), "f")),
            Expected.InType("void M()\n{\n}\n\nint f;"));
        yield return Case("DisabledRecordKeepsComment", static () => InType(static type => type.Comment("Keep")
            .Public().Record("R", false).EndRecord().Field(T("int"), "f")), Expected.InType("// Keep\nint f;"));
        yield return Case("DisabledPropertyKeepsGap", static () => InType(static type => type.Field(T("int"), "x")
                .Gap().Public().Property(T("int"), "P", when: false).Get().EndProperty().Field(T("int"), "y")),
            Expected.InType("int x;\n\nint y;"));
        yield return Case("DisabledIndexerKeepsComment", static () => InType(static type => type.Comment("Keep")
                .Public().Indexer(T("int"), false).Get().EndIndexer().Field(T("int"), "f")),
            Expected.InType("// Keep\nint f;"));
        yield return Case("DisabledOperatorKeepsComment", static () => InType(static type => type.Comment("Keep")
                .Public().Operator(T("C"), Add, false).EndOperator().Field(T("int"), "f")),
            Expected.InType("// Keep\nint f;"));
        yield return Case("DisabledConversionKeepsComment", static () => InType(static type => type.Comment("Keep")
                .Public().Conversion(ConversionKind.Implicit, T("int"), false).EndOperator().Field(T("int"), "f")),
            Expected.InType("// Keep\nint f;"));
        yield return Case("DisabledLoopsKeepComments", static () => InBody(static body => body
                .Comment("For").Ref().For("i", Lit(0), Id("c"), Id("s"), false).EndFor()
                .Comment("While").While(Id("a"), false).EndWhile()
                .Comment("DoWhile").DoWhile(false).EndDoWhile(Id("a"))
                .Var("x", Lit(1))),
            Expected.InBody("// For\n// While\n// DoWhile\nvar x = 1;"));
        yield return Case("EmptyDoWhileConditionKeepsTrivia", static () => InBody(static body => body
                .Var("y", Lit(0)).Gap().Comment("Keep").IfDirective("X").DoWhile().Break().EndDoWhile(Id("a", false))
                .Var("x", Lit(1)).EndIfDirective().Return()),
            "class C\n{\n    void M()\n    {\n        var y = 0;\n\n        // Keep\n#if X\n        var x = 1;\n" +
            "#endif\n        return;\n    }\n}");
        yield return Case("DisabledBlocksKeepComments", static () => InBody(static body => body
                .Comment("Using").Using("s", Id("x"), false).EndUsing()
                .Comment("Lock").Lock(Id("x"), false).EndLock()
                .Comment("Unsafe").Unsafe(false).EndUnsafe()
                .Comment("Try").Ref().Try(false).Finally().EndTry()
                .Var("x", Lit(1))),
            Expected.InBody("// Using\n// Lock\n// Unsafe\n// Try\nvar x = 1;"));
    }

    [Test]
    public void CompoundAssignWithoutCompoundFormIsRejected()
    {
        Assert.Throws<ArgumentOutOfRangeException>(static () => Id("a").CompoundAssign(Equal, Id("b")));
    }

    [Test]
    public void OperatorThatCannotBeOverloadedIsRejected()
    {
        Assert.Throws<ArgumentOutOfRangeException>(static () => InType(static type => type.Operator(T("bool"), And)
            .EndOperator()));
    }

    private static TestCaseData Case(string name, Func<string> actual, string expected)
    {
        return new TestCaseData(actual, Expected.ConvertToTabs(expected)).SetArgDisplayNames(name);
    }
}
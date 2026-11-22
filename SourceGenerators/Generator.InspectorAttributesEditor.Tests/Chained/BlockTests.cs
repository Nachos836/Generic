using NUnit.Framework;
using Shared.CodeGeneration.Chained;
using static Generator.InspectorAttributesEditor.Tests.Chained.Snippets;
using static Shared.CodeGeneration.Chained.BinaryOperator;
using static Shared.CodeGeneration.Chained.Blocks;

namespace Generator.InspectorAttributesEditor.Tests.Chained;

public sealed class BlockTests
{
    [TestCaseSource(nameof(Positive))]
    public void BlockWritesItsCode(Func<string> actual, string expected)
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
        yield return Case("File", static () => Code.File().Class("A").EndClass().EndFile(), "class A\n{\n}");
        yield return Case("Nullable", static () => Code.File().Nullable().Class("A").EndClass().EndFile(),
            "#nullable enable\nclass A\n{\n}");
        yield return Case("Using", static () => Code.File().Using("System.Collections.Generic").Class("A").EndClass()
            .EndFile(), "using System.Collections.Generic;\n\nclass A\n{\n}");
        yield return Case("UsingStatic", static () => Code.File().UsingStatic("System.Math").Class("A").EndClass()
            .EndFile(), "using static System.Math;\n\nclass A\n{\n}");
        yield return Case("Namespace", static () => Code.File().Namespace("A.B").Class("C").EndClass().EndNamespace()
            .EndFile(), "namespace A.B\n{\n    class C\n    {\n    }\n}");

        yield return Case("Comment", static () => InType(static type => type.Comment("Note").Field(T("int"), "x")),
            Expected.InType("// Note\nint x;"));
        yield return Case("DocComment", static () => InType(static type => type
                .DocComment(Lines("<summary>", "Text", "</summary>")).Field(T("int"), "x")),
            Expected.InType("/// <summary>\n/// Text\n/// </summary>\nint x;"));
        yield return Case("Gap",
            static () => InType(static type => type.Field(T("int"), "x").Gap().Field(T("int"), "y")),
            Expected.InType("int x;\n\nint y;"));
        yield return Case("Define", static () => Code.File().Define("X").Class("A").EndClass().EndFile(),
            "#define X\nclass A\n{\n}");
        yield return Case("IfDirective", static () => Code.File().IfDirective("X").Class("A").EndClass()
            .EndIfDirective().EndFile(), "#if X\nclass A\n{\n}\n#endif");
        yield return Case("IfDirectiveExpression", static () => Code.File().IfDirective(Id("X").Binary(And, Id("Y")))
            .Class("A").EndClass().EndIfDirective().EndFile(), "#if X && Y\nclass A\n{\n}\n#endif");
        yield return Case("ElseDirective", static () => InBody(static body => body.IfDirective("X").Break()
                .ElseDirective().Return().EndIfDirective()),
            "class C\n{\n    void M()\n    {\n#if X\n        break;\n#else\n        return;\n#endif\n    }\n}");
        yield return Case("EndIfDirective", static () => InBody(static body => body.IfDirective("X").Break()
            .EndIfDirective()), "class C\n{\n    void M()\n    {\n#if X\n        break;\n#endif\n    }\n}");

        yield return Case("Class", static () => InType(static type => type.Class("N").EndClass()),
            Expected.InType("class N\n{\n}"));
        yield return Case("Struct", static () => InType(static type => type.Struct("N").EndStruct()),
            Expected.InType("struct N\n{\n}"));
        yield return Case("Interface", static () => InType(static type => type.Interface("N").EndInterface()),
            Expected.InType("interface N\n{\n}"));
        yield return Case("Enum", static () => InType(static type => type.Enum("E").EnumMember("A").EndEnum()),
            Expected.InType("enum E\n{\n    A\n}"));
        yield return Case("EnumUnderlyingType", static () => InType(static type => type.Enum("E").Inherits(T("ulong"))
            .EnumMember("A").EndEnum()), Expected.InType("enum E : ulong\n{\n    A\n}"));
        yield return Case("Inherits", static () => Code.File().Class("A").Inherits(T("B")).EndClass().EndFile(),
            "class A : B\n{\n}");
        yield return Case("Implements", static () => Code.File().Class("A").Inherits(T("B")).Implements(T("I"))
            .EndClass().EndFile(), "class A : B, I\n{\n}");
        yield return Case("TypeParam", static () => Code.File().Interface("I").TypeParam("T", Variance.Out)
            .EndInterface().EndFile(), "interface I<out T>\n{\n}");
        yield return Case("Where", static () => Code.File().Class("A").TypeParam("T")
                .Where("T", [Constraint.Unmanaged, T("I")]).EndClass().EndFile(),
            "class A<T>\n    where T : unmanaged, I\n{\n}");
        yield return Case("EnumMember", static () => InType(static type => type.Enum("E").EnumMember("A", Lit(1))
            .EnumMember("B").EndEnum()), Expected.InType("enum E\n{\n    A = 1,\n    B\n}"));

        yield return Case("Attribute", static () => InType(static type => type.Attribute("A")
            .Attribute("B", [Lit(1), Named("x", Lit(2))])
            .Field(T("int"), "f")), Expected.InType("[A, B(1, x: 2)]\nint f;"));
        yield return Case("SplitAttributes", static () => InType(static type => type.Attribute("A").SplitAttributes()
            .Attribute("B").Field(T("int"), "f")), Expected.InType("[A]\n[B]\nint f;"));

        yield return Case("Public", static () => InType(static type => type.Public().Field(T("int"), "f")),
            Expected.InType("public int f;"));
        yield return Case("Internal", static () => InType(static type => type.Internal().Field(T("int"), "f")),
            Expected.InType("internal int f;"));
        yield return Case("Private", static () => InType(static type => type.Private().Field(T("int"), "f")),
            Expected.InType("private int f;"));
        yield return Case("Static", static () => InType(static type => type.Static().Field(T("int"), "f")),
            Expected.InType("static int f;"));
        yield return Case("Readonly", static () => InType(static type => type.Readonly().Field(T("int"), "f")),
            Expected.InType("readonly int f;"));
        yield return Case("Sealed", static () => InType(static type => type.Sealed().Class("N").EndClass()),
            Expected.InType("sealed class N\n{\n}"));
        yield return Case("Override", static () => InType(static type => type.Override().Method(T("void"), "M")
            .EndMethod()), Expected.InType("override void M();"));
        yield return Case("Partial", static () => InType(static type => type.Partial().Class("N").EndClass()),
            Expected.InType("partial class N\n{\n}"));
        yield return Case("Unsafe", static () => InType(static type => type.Unsafe().Field(T("int"), "f")),
            Expected.InType("unsafe int f;"));
        yield return Case("Ref", static () => InType(static type => type.Ref().Readonly().Method(T("int"), "M")
            .Arrow(Ref(Id("x"))).EndMethod()), Expected.InType("ref readonly int M() => ref x;"));
        yield return Case("Protected", static () => InType(static type => type.Protected().Field(T("int"), "f")),
            Expected.InType("protected int f;"));
        yield return Case("FileLocal", static () => Code.File().FileLocal().Class("A").EndClass().EndFile(),
            "file class A\n{\n}");
        yield return Case("Const", static () => InType(static type => type.Const().Field(T("int"), "f", Lit(1))),
            Expected.InType("const int f = 1;"));
        yield return Case("Abstract", static () => InType(static type => type.Abstract().Class("N").EndClass()),
            Expected.InType("abstract class N\n{\n}"));
        yield return Case("Virtual", static () => InType(static type => type.Virtual().Method(T("void"), "M")
            .EndMethod()), Expected.InType("virtual void M();"));
        yield return Case("NewModifier", static () => InType(static type => type.New().Field(T("int"), "f")),
            Expected.InType("new int f;"));
        yield return Case("Extern", static () => InType(static type => type.Static().Extern().Method(T("void"), "M")
            .EndMethod()), Expected.InType("static extern void M();"));
        yield return Case("Async", static () => InType(static type => type.Async().Method(T("Task"), "M").Body()
            .EndMethod()), Expected.InType("async Task M()\n{\n}"));
        yield return Case("Required", static () => InType(static type => type.Required().Field(T("int"), "f")),
            Expected.InType("required int f;"));
        yield return Case("Volatile", static () => InType(static type => type.Volatile().Field(T("int"), "f")),
            Expected.InType("volatile int f;"));
        yield return Case("Record", static () => InType(static type => type.Record("R").Field(T("int"), "f")
            .EndRecord()), Expected.InType("record R\n{\n    int f;\n}"));

        yield return Case("Field", static () => InType(static type => type.Field(T("int"), "f", Lit(1))),
            Expected.InType("int f = 1;"));
        yield return Case("Event", static () => InType(static type => type.Public().Event(T("Action").Nullable(), "E")),
            Expected.InType("public event Action? E;"));
        yield return Case("Constructor", static () => InType(static type => type.Public().Constructor()
                .Param(T("int"), "x").Body().Return().EndConstructor()),
            Expected.InType("public C(int x)\n{\n    return;\n}"));
        yield return Case("CallsThis", static () => InType(static type => type.Constructor().CallsThis([Lit(1)])
            .Body().EndConstructor()), Expected.InType("C() : this(1)\n{\n}"));
        yield return Case("CallsBase", static () => InType(static type => type.Constructor().CallsBase([Lit(1)])
            .Body().EndConstructor()), Expected.InType("C() : base(1)\n{\n}"));
        yield return Case("Property", static () => InType(static type => type.Public().Property(T("int"), "P")
            .Get().Set().EndProperty()), Expected.InType("public int P { get; set; }"));
        yield return Case("PropertyValue", static () => InType(static type => type.Property(T("int"), "P", Lit(1))
            .Get().EndProperty()), Expected.InType("int P { get; } = 1;"));
        yield return Case("PropertyExplicitFor", static () => InType(static type => type.ExplicitFor(T("I"))
            .Property(T("int"), "P").Get().EndProperty()), Expected.InType("int I.P { get; }"));
        yield return Case("Get", static () => InType(static type => type.Property(T("int"), "P")
                .GetBody().Return(Id("p")).EndGet().EndProperty()),
            Expected.InType("int P\n{\n    get\n    {\n        return p;\n    }\n}"));
        yield return Case("GetArrow", static () => InType(static type => type.Property(T("int"), "P").Get(Id("p"))
            .EndProperty()), Expected.InType("int P { get => p; }"));
        yield return Case("Set", static () => InType(static type => type.Property(T("int"), "P").Get().Private()
                .Set(Id("p").Assign(Id("value"))).EndProperty()),
            Expected.InType("int P { get; private set => p = value; }"));
        yield return Case("SetBody", static () => InType(static type => type.Property(T("int"), "P")
                .SetBody().Do(Id("p").Assign(Id("value"))).EndSet().EndProperty()),
            Expected.InType("int P\n{\n    set\n    {\n        p = value;\n    }\n}"));
        yield return Case("Init", static () => InType(static type => type.Property(T("int"), "P").Get().Init()
            .EndProperty()), Expected.InType("int P { get; init; }"));
        yield return Case("InitArrow", static () => InType(static type => type.Property(T("int"), "P")
            .Init(Id("p").Assign(Id("value"))).EndProperty()), Expected.InType("int P { init => p = value; }"));
        yield return Case("InitBody", static () => InType(static type => type.Property(T("int"), "P")
                .InitBody().Do(Id("p").Assign(Id("value"))).EndInit().EndProperty()),
            Expected.InType("int P\n{\n    init\n    {\n        p = value;\n    }\n}"));
        yield return Case("AccessorProtected", static () => InType(static type => type.Property(T("int"), "P").Get()
            .Protected().Set().EndProperty()), Expected.InType("int P { get; protected set; }"));
        yield return Case("AccessorInternal", static () => InType(static type => type.Property(T("int"), "P").Get()
            .Internal().Set().EndProperty()), Expected.InType("int P { get; internal set; }"));
        yield return Case("AccessorReadonly", static () => InType(static type => type.Property(T("int"), "P")
            .Readonly().Get(Id("p")).EndProperty()), Expected.InType("int P { readonly get => p; }"));
        yield return Case("Indexer", static () => InType(static type => type.Public().Indexer(T("int"))
            .Param(T("int"), "i").Get(Id("i")).EndIndexer()), Expected.InType("public int this[int i] { get => i; }"));
        yield return Case("Operator", static () => InType(static type => type.Public().Static().Operator(T("C"), Add)
                .Param(T("C"), "a").Param(T("C"), "b").Arrow(Id("a")).EndOperator()),
            Expected.InType("public static C operator +(C a, C b) => a;"));
        yield return Case("OperatorBody", static () => InType(static type => type.Public().Static()
                .Operator(T("C"), UnaryOperator.Minus).Param(T("C"), "a").Body().Return(Id("a")).EndOperator()),
            Expected.InType("public static C operator -(C a)\n{\n    return a;\n}"));
        yield return Case("Conversion", static () => InType(static type => type.Public().Static()
                .Conversion(ConversionKind.Implicit, T("int")).Param(T("C"), "c").Arrow(Lit(0)).EndOperator()),
            Expected.InType("public static implicit operator int(C c) => 0;"));
        yield return Case("Method", static () => InType(static type => type.Method(T("int"), "M").Arrow(Lit(1))
            .EndMethod()), Expected.InType("int M() => 1;"));
        yield return Case("MethodBody", static () => InType(static type => type.Method(T("void"), "M").Body().Return()
            .EndMethod()), Expected.InType("void M()\n{\n    return;\n}"));
        yield return Case("MethodTypeParam", static () => InType(static type => type.Method(T("void"), "M")
                .TypeParam("T").Param(T("T"), "x").Where("T", [Constraint.Struct]).EndMethod()),
            Expected.InType("void M<T>(T x)\n    where T : struct;"));
        yield return Case("ExplicitFor", static () => InType(static type => type.ExplicitFor(T("I"))
            .Method(T("void"), "M").EndMethod()), Expected.InType("void I.M();"));
        yield return Case("Param", static () => InMethod(static method => method.Param(T("int"), "x", Lit(1))
            .Param(T("int"), "y")), Expected.InMethod("int x = 1, int y"));
        yield return Case("ParamRef", static () => InMethod(static method => method.Ref().Param(T("int"), "x")),
            Expected.InMethod("ref int x"));
        yield return Case("ParamIn", static () => InMethod(static method => method.In().Param(T("int"), "x")),
            Expected.InMethod("in int x"));
        yield return Case("ParamThis", static () => InMethod(static method => method.This().Param(T("int"), "x")),
            Expected.InMethod("this int x"));
        yield return Case("ParamReadonly", static () => InMethod(static method => method.Ref().Readonly()
            .Param(T("int"), "x")), Expected.InMethod("ref readonly int x"));
        yield return Case("ParamAttribute", static () => InMethod(static method => method.Attribute("A")
            .Param(T("int"), "x")), Expected.InMethod("[A] int x"));

        yield return Case("Var", static () => InBody(static body => body.Var("x", Lit(1))),
            Expected.InBody("var x = 1;"));
        yield return Case("VarRef", static () => InBody(static body => body.Ref().Readonly().Var("x", Ref(Id("y")))),
            Expected.InBody("ref readonly var x = ref y;"));
        yield return Case("Do", static () => InBody(static body => body.Do(Id("a").Call())), Expected.InBody("a();"));
        yield return Case("If", static () => InBody(static body => body.If(Id("a")).Return().EndIf()),
            Expected.InBody("if (a)\n{\n    return;\n}"));
        yield return Case("IfUnbraced", static () => InBody(static body => body.If(Id("a"), braced: false).Return()
            .EndIf()), Expected.InBody("if (a)\n    return;"));
        yield return Case("ElseIf", static () => InBody(static body => body.If(Id("a")).Return().ElseIf(Id("b"))
            .Break().EndIf()), Expected.InBody("if (a)\n{\n    return;\n}\nelse if (b)\n{\n    break;\n}"));
        yield return Case("Else", static () => InBody(static body => body.If(Id("a")).Return().Else().Break().EndIf()),
            Expected.InBody("if (a)\n{\n    return;\n}\nelse\n{\n    break;\n}"));
        yield return Case("Foreach", static () => InBody(static body => body.Foreach("i", Id("items"))
            .Do(Id("i").Call()).EndForeach()), Expected.InBody("foreach (var i in items)\n{\n    i();\n}"));
        yield return Case("Switch", static () => InBody(static body => body.Switch(Id("x")).Case(Lit(1)).Break()
            .EndCase().EndSwitch()), Expected.InBody("switch (x)\n{\n    case 1:\n        break;\n}"));
        yield return Case("Case", static () => InBody(static body => body.Switch(Id("x")).Case(Lit(1)).Return()
                .EndCase().Case(Lit(2)).Break().EndCase().EndSwitch()),
            Expected.InBody("switch (x)\n{\n    case 1:\n        return;\n    case 2:\n        break;\n}"));
        yield return Case("Default", static () => InBody(static body => body.Switch(Id("x")).Default().Break()
            .EndCase().EndSwitch()), Expected.InBody("switch (x)\n{\n    default:\n        break;\n}"));
        yield return Case("Break", static () => InBody(static body => body.Break()), Expected.InBody("break;"));
        yield return Case("Return", static () => InBody(static body => body.Return(Lit(1))),
            Expected.InBody("return 1;"));
        yield return Case("Throw", static () => InBody(static body => body.Throw(New(T("E")))),
            Expected.InBody("throw new E();"));
        yield return Case("Fixed", static () => InBody(static body => body.Fixed(T("void").Pointer(), "p",
            AddressOf(Id("x"))).Return().EndFixed()), Expected.InBody("fixed (void* p = &x)\n{\n    return;\n}"));
        yield return Case("For", static () => InBody(static body => body.For("i", Lit(0),
                Id("i").Binary(Less, Id("n")), Unary(UnaryOperator.Increment, Id("i"))).Continue().EndFor()),
            Expected.InBody("for (var i = 0; i < n; ++i)\n{\n    continue;\n}"));
        yield return Case("While", static () => InBody(static body => body.While(Id("a")).Break().EndWhile()),
            Expected.InBody("while (a)\n{\n    break;\n}"));
        yield return Case("DoWhile", static () => InBody(static body => body.DoWhile().Break().EndDoWhile(Id("a"))),
            Expected.InBody("do\n{\n    break;\n}\nwhile (a);"));
        yield return Case("Continue", static () => InBody(static body => body.Continue()),
            Expected.InBody("continue;"));
        yield return Case("YieldReturn", static () => InBody(static body => body.YieldReturn(Lit(1))),
            Expected.InBody("yield return 1;"));
        yield return Case("YieldBreak", static () => InBody(static body => body.YieldBreak()),
            Expected.InBody("yield break;"));
        yield return Case("UsingStatement", static () => InBody(static body => body.Using("s", Id("open").Call())
            .Return().EndUsing()), Expected.InBody("using (var s = open())\n{\n    return;\n}"));
        yield return Case("Lock", static () => InBody(static body => body.Lock(Id("gate")).Return().EndLock()),
            Expected.InBody("lock (gate)\n{\n    return;\n}"));
        yield return Case("UnsafeStatement", static () => InBody(static body => body.Unsafe().Return().EndUnsafe()),
            Expected.InBody("unsafe\n{\n    return;\n}"));
        yield return Case("Try", static () => InBody(static body => body.Try().Return().Finally().Break().EndTry()),
            Expected.InBody("try\n{\n    return;\n}\nfinally\n{\n    break;\n}"));
        yield return Case("Catch", static () => InBody(static body => body.Try().Return().Catch(T("E"), "e")
                .Throw(Id("e")).Catch().Break().EndTry()),
            Expected.InBody("try\n{\n    return;\n}\ncatch (E e)\n{\n    throw e;\n}\ncatch\n{\n    break;\n}"));
        yield return Case("Finally", static () => InBody(static body => body.Try().Return().Catch(T("E")).Break()
                .Finally().Break().EndTry().Return()),
            Expected.InBody("try\n{\n    return;\n}\ncatch (E)\n{\n    break;\n}\nfinally\n{\n    break;\n}\n\n" +
                            "return;"));

        yield return Case("Id", static () => OfExpression(Id("a")), Expected.OfExpression("a"));
        yield return Case("IdKeyword", static () => OfExpression(Id("event")), Expected.OfExpression("@event"));
        yield return Case("Dot", static () => OfExpression(Id("a").Dot("b")), Expected.OfExpression("a.b"));
        yield return Case("NullDot", static () => OfExpression(Id("a").NullDot("b").Call()),
            Expected.OfExpression("a?.b()"));
        yield return Case("Call", static () => OfExpression(Id("a").Call([Id("b"), Lit(1)])),
            Expected.OfExpression("a(b, 1)"));
        yield return Case("TypeArgs", static () => OfExpression(Id("a").Dot("M").TypeArgs([T("int")]).Call()),
            Expected.OfExpression("a.M<int>()"));
        yield return Case("Index", static () => OfExpression(Id("a").Index([Id("i")])), Expected.OfExpression("a[i]"));
        yield return Case("New", static () => OfExpression(New(T("A"), [Lit(1)])), Expected.OfExpression("new A(1)"));
        yield return Case("NewTargetTyped", static () => OfExpression(New([Lit(1)])), Expected.OfExpression("new(1)"));
        yield return Case("Init", static () => OfExpression(New(T("A")).Init([Id("x").Assign(Lit(1))])),
            Expected.OfExpression("new A()\n{\n    x = 1\n}"));
        yield return Case("InitInArgument",
            static () => OfExpression(Id("f").Call([New(T("A")).Init([Id("x").Assign(Lit(1))])])),
            Expected.OfExpression("f(new A() { x = 1 })"));
        yield return Case("Lit", static () => OfExpression(Id("f").Call([Lit("s\""), Lit(1), Lit(true), Lit('c')])),
            Expected.OfExpression("f(\"s\\\"\", 1, true, 'c')"));
        yield return Case("Default", static () => OfExpression(Id("f").Call([Default(), Default(T("int"))])),
            Expected.OfExpression("f(default, default(int))"));
        yield return Case("This", static () => OfExpression(This().Dot("x")), Expected.OfExpression("this.x"));
        yield return Case("Assign", static () => OfExpression(Id("a").Assign(Lit(1))), Expected.OfExpression("a = 1"));
        yield return Case("Binary", static () => OfExpression(Id("a").Binary(Add, Id("b")).Binary(Multiply, Id("c"))),
            Expected.OfExpression("(a + b) * c"));
        yield return Case("NullForgiving", static () => OfExpression(Id("a").NullForgiving()),
            Expected.OfExpression("a!"));
        yield return Case("AddressOf", static () => OfExpression(AddressOf(Id("a"))), Expected.OfExpression("&a"));
        yield return Case("Is", static () => OfExpression(Id("a").Is(Lit(false))), Expected.OfExpression("a is false"));
        yield return Case("Cast", static () => OfExpression(Cast(T("int"), Id("a")).Dot("b")),
            Expected.OfExpression("((int)a).b"));
        yield return Case("TypeOf", static () => OfExpression(TypeOf(T("A"))), Expected.OfExpression("typeof(A)"));
        yield return Case("SizeOf", static () => OfExpression(SizeOf(T("A"))), Expected.OfExpression("sizeof(A)"));
        yield return Case("NameOf", static () => OfExpression(NameOf(Id("a"))), Expected.OfExpression("nameof(a)"));
        yield return Case("Lambda", static () => OfExpression(Id("f").Call([Lambda().Arrow(Id("a").Call())])),
            Expected.OfExpression("f(() => a())"));
        yield return Case("StaticLambda",
            static () => OfExpression(Id("f").Call([StaticLambda(["x"]).Body().Return().EndLambda()])),
            Expected.OfExpression("f(static (x) =>\n{\n    return;\n})"));
        yield return Case("SwitchExpression", static () => OfExpression(Id("x").Switch().Arm(Lit(1), Id("a"))
            .EndSwitch()), Expected.OfExpression("x switch\n{\n    1 => a\n}"));
        yield return Case("Arm", static () => OfExpression(Id("x").Switch().Arm(Lit(1), Id("a")).Arm(Discard(), Id("b"))
            .EndSwitch()), Expected.OfExpression("x switch\n{\n    1 => a,\n    _ => b\n}"));
        yield return Case("Discard", static () => OfExpression(Discard().Assign(Id("a"))),
            Expected.OfExpression("_ = a"));
        yield return Case("ThrowExpression", static () => OfExpression(Id("a").Assign(ThrowExpression(New(T("E"))))),
            Expected.OfExpression("a = throw new E()"));
        yield return Case("RefArg", static () => OfExpression(Id("f").Call([RefArg(Id("a"))])),
            Expected.OfExpression("f(ref a)"));
        yield return Case("InArg", static () => OfExpression(Id("f").Call([InArg(Id("a"))])),
            Expected.OfExpression("f(in a)"));
        yield return Case("Named", static () => OfExpression(Id("f").Call([Named("x", Lit(1))])),
            Expected.OfExpression("f(x: 1)"));
        yield return Case("RefExpression", static () => OfExpression(Id("x").Assign(Ref(Id("a")))),
            Expected.OfExpression("x = ref a"));
        yield return Case("Null", static () => OfExpression(Id("a").Assign(Null())), Expected.OfExpression("a = null"));
        yield return Case("Base", static () => OfExpression(Base().Dot("M").Call()), Expected.OfExpression("base.M()"));
        yield return Case("CompoundAssign", static () => OfExpression(Id("a").CompoundAssign(Add, Lit(1))),
            Expected.OfExpression("a += 1"));
        yield return Case("Unary", static () => OfExpression(Id("a").Assign(Unary(UnaryOperator.Minus, Id("b")))),
            Expected.OfExpression("a = -b"));
        yield return Case("Not", static () => OfExpression(Id("a").Assign(Not(Id("b").Dot("c")))),
            Expected.OfExpression("a = !b.c"));
        yield return Case("NotPattern", static () => OfExpression(Id("a").Assign(Id("b").Is(Not(Null())))),
            Expected.OfExpression("a = b is not null"));
        yield return Case("As", static () => OfExpression(Id("a").Assign(Id("b").As(T("C")))),
            Expected.OfExpression("a = b as C"));
        yield return Case("Ternary", static () => OfExpression(Id("a").Assign(Id("c").Ternary(Lit(1), Lit(2)))),
            Expected.OfExpression("a = c ? 1 : 2"));
        yield return Case("Coalesce", static () => OfExpression(Id("a").Assign(Id("b").Coalesce(Id("c")))),
            Expected.OfExpression("a = b ?? c"));
        yield return Case("OutArg", static () => OfExpression(Id("f").Call([OutArg(Id("a"))])),
            Expected.OfExpression("f(out a)"));

        yield return Case("T", static () => OfType(T("List", [T("int")])), Expected.OfType("List<int>"));
        yield return Case("TQualified", static () => OfType(T("System.Collections.Generic.List", [T("int")])),
            Expected.OfType("System.Collections.Generic.List<int>"));
        yield return Case("Predefined", static () => OfType(T("ulong")), Expected.OfType("ulong"));
        yield return Case("Nested", static () => OfType(T("A", [T("int")]).Nested("B")), Expected.OfType("A<int>.B"));
        yield return Case("NullableType", static () => OfType(T("int").Nullable()), Expected.OfType("int?"));
        yield return Case("Pointer", static () => OfType(T("int").Pointer()), Expected.OfType("int*"));
        yield return Case("Array", static () => OfType(T("int").Array()), Expected.OfType("int[]"));
        yield return Case("Tuple", static () => OfType(Tuple([T("int"), T("string")])),
            Expected.OfType("(int, string)"));

        yield return Case("ForEach", static () => InType(static type => type.ForEach(["a", "b"],
            static (scope, name) => scope.Field(T("int"), name))), Expected.InType("int a;\nint b;"));
    }

    private static IEnumerable<TestCaseData> FalseCondition()
    {
        yield return Case("File", static () => Code.File(false).Class("A").EndClass().EndFile(), "");
        yield return Case("Nullable", static () => Code.File().Nullable(false).Class("A").EndClass().EndFile(),
            "class A\n{\n}");
        yield return Case("Using", static () => Code.File().Using("System", false).Class("A").EndClass().EndFile(),
            "class A\n{\n}");
        yield return Case("UsingStatic", static () => Code.File().UsingStatic("System.Math", false).Class("A")
            .EndClass().EndFile(), "class A\n{\n}");
        yield return Case("Namespace", static () => Code.File().Namespace("A", false).Class("C").EndClass()
            .EndNamespace().Class("B").EndClass().EndFile(), "class B\n{\n}");

        yield return Case("Comment", static () => InType(static type => type.Comment("Note", false)
            .Field(T("int"), "x")), Expected.InType("int x;"));
        yield return Case("DocComment",
            static () => InType(static type => type.DocComment(Lines("Text"), false).Field(T("int"), "x")),
            Expected.InType("int x;"));
        yield return Case("Gap", static () => InType(static type => type.Field(T("int"), "x").Gap(false)
            .Field(T("int"), "y")), Expected.InType("int x;\nint y;"));
        yield return Case("Define", static () => Code.File().Define("X", false).Class("A").EndClass().EndFile(),
            "class A\n{\n}");
        yield return Case("IfDirective", static () => Code.File().IfDirective("X", false).Class("A").EndClass()
            .EndFile(), "class A\n{\n}");
        yield return Case("ElseDirective", static () => InBody(static body => body.Break().ElseDirective(false)
            .Return()), Expected.InBody("break;\nreturn;"));
        yield return Case("EndIfDirective", static () => InBody(static body => body.Break().EndIfDirective(false)),
            Expected.InBody("break;"));

        yield return Case("Class", static () => InType(static type => type.Public().Class("N", false).EndClass()
            .Field(T("int"), "x")), Expected.InType("int x;"));
        yield return Case("Struct", static () => InType(static type => type.Struct("N", false).EndStruct()),
            Expected.InType(""));
        yield return Case("Interface", static () => InType(static type => type.Interface("N", false).EndInterface()),
            Expected.InType(""));
        yield return Case("Enum", static () => InType(static type => type.Enum("E", false).EndEnum()),
            Expected.InType(""));
        yield return Case("EnumUnderlyingType", static () => InType(static type => type.Enum("E")
            .Inherits(T("ulong"), false).EnumMember("A").EndEnum()), Expected.InType("enum E\n{\n    A\n}"));
        yield return Case("Inherits", static () => Code.File().Class("A").Inherits(T("B"), false).EndClass()
            .EndFile(), "class A\n{\n}");
        yield return Case("Implements", static () => Code.File().Class("A").Implements(T("I"), false).EndClass()
            .EndFile(), "class A\n{\n}");
        yield return Case("TypeParam", static () => Code.File().Interface("I").TypeParam("T", when: false)
            .EndInterface().EndFile(), "interface I\n{\n}");
        yield return Case("Where", static () => Code.File().Class("A").TypeParam("T")
            .Where("T", [Constraint.Unmanaged], false).EndClass().EndFile(), "class A<T>\n{\n}");
        yield return Case("EnumMember", static () => InType(static type => type.Enum("E").EnumMember("A", when: false)
            .EnumMember("B").EndEnum()), Expected.InType("enum E\n{\n    B\n}"));

        yield return Case("Attribute", static () => InType(static type => type.Attribute("A", when: false)
            .Field(T("int"), "f")), Expected.InType("int f;"));
        yield return Case("SplitAttributes", static () => InType(static type => type.Attribute("A")
            .SplitAttributes(false).Attribute("B").Field(T("int"), "f")), Expected.InType("[A, B]\nint f;"));

        yield return Case("Public", static () => InType(static type => type.Public(false).Field(T("int"), "f")),
            Expected.InType("int f;"));
        yield return Case("Internal", static () => InType(static type => type.Internal(false).Field(T("int"), "f")),
            Expected.InType("int f;"));
        yield return Case("Private", static () => InType(static type => type.Private(false).Field(T("int"), "f")),
            Expected.InType("int f;"));
        yield return Case("Static", static () => InType(static type => type.Static(false).Field(T("int"), "f")),
            Expected.InType("int f;"));
        yield return Case("Readonly", static () => InType(static type => type.Readonly(false).Field(T("int"), "f")),
            Expected.InType("int f;"));
        yield return Case("Sealed", static () => InType(static type => type.Sealed(false).Class("N").EndClass()),
            Expected.InType("class N\n{\n}"));
        yield return Case("Override", static () => InType(static type => type.Override(false).Method(T("void"), "M")
            .EndMethod()), Expected.InType("void M();"));
        yield return Case("Partial", static () => InType(static type => type.Partial(false).Class("N").EndClass()),
            Expected.InType("class N\n{\n}"));
        yield return Case("Unsafe", static () => InType(static type => type.Unsafe(false).Field(T("int"), "f")),
            Expected.InType("int f;"));
        yield return Case("Ref", static () => InType(static type => type.Ref(false).Method(T("int"), "M")
            .Arrow(Id("x")).EndMethod()), Expected.InType("int M() => x;"));
        yield return Case("Protected", static () => InType(static type => type.Protected(false).Field(T("int"), "f")),
            Expected.InType("int f;"));
        yield return Case("FileLocal", static () => Code.File().FileLocal(false).Class("A").EndClass().EndFile(),
            "class A\n{\n}");
        yield return Case("Const", static () => InType(static type => type.Const(false).Field(T("int"), "f")),
            Expected.InType("int f;"));
        yield return Case("Abstract", static () => InType(static type => type.Abstract(false).Class("N").EndClass()),
            Expected.InType("class N\n{\n}"));
        yield return Case("Virtual", static () => InType(static type => type.Virtual(false).Method(T("void"), "M")
            .EndMethod()), Expected.InType("void M();"));
        yield return Case("NewModifier", static () => InType(static type => type.New(false).Field(T("int"), "f")),
            Expected.InType("int f;"));
        yield return Case("Extern", static () => InType(static type => type.Extern(false).Method(T("void"), "M")
            .EndMethod()), Expected.InType("void M();"));
        yield return Case("Async", static () => InType(static type => type.Async(false).Method(T("void"), "M")
            .EndMethod()), Expected.InType("void M();"));
        yield return Case("Required", static () => InType(static type => type.Required(false).Field(T("int"), "f")),
            Expected.InType("int f;"));
        yield return Case("Volatile", static () => InType(static type => type.Volatile(false).Field(T("int"), "f")),
            Expected.InType("int f;"));
        yield return Case("Record", static () => InType(static type => type.Public().Record("R", false).EndRecord()
            .Field(T("int"), "x")), Expected.InType("int x;"));

        yield return Case("Field", static () => InType(static type => type.Private().Field(T("int"), "f", when: false)),
            Expected.InType(""));
        yield return Case("Event", static () => InType(static type => type.Event(T("Action"), "E", false)),
            Expected.InType(""));
        yield return Case("Constructor", static () => InType(static type => type.Constructor(false).EndConstructor()),
            Expected.InType(""));
        yield return Case("CallsThis", static () => InType(static type => type.Constructor().CallsThis(when: false)
            .Body().EndConstructor()), Expected.InType("C()\n{\n}"));
        yield return Case("CallsBase", static () => InType(static type => type.Constructor().CallsBase(when: false)
            .Body().EndConstructor()), Expected.InType("C()\n{\n}"));
        yield return Case("Property", static () => InType(static type => type.Public().Property(T("int"), "P",
            when: false).Get().EndProperty().Field(T("int"), "f")), Expected.InType("int f;"));
        yield return Case("PropertyValue", static () => InType(static type => type.Property(T("int"), "P",
            Lit(1, false)).Get().EndProperty()), Expected.InType("int P { get; }"));
        yield return Case("Get", static () => InType(static type => type.Property(T("int"), "P").GetBody(false)
            .Return().EndGet().Set().EndProperty()), Expected.InType("int P { set; }"));
        yield return Case("GetArrow", static () => InType(static type => type.Property(T("int"), "P")
            .Get(Id("p"), false).Set().EndProperty()), Expected.InType("int P { set; }"));
        yield return Case("Set", static () => InType(static type => type.Property(T("int"), "P").Get().Private()
            .Set(false).Init().EndProperty()), Expected.InType("int P { get; init; }"));
        yield return Case("SetBody", static () => InType(static type => type.Property(T("int"), "P").Get()
            .SetBody(false).EndSet().EndProperty()), Expected.InType("int P { get; }"));
        yield return Case("Init", static () => InType(static type => type.Property(T("int"), "P").Get().Init(false)
            .EndProperty()), Expected.InType("int P { get; }"));
        yield return Case("InitArrow", static () => InType(static type => type.Property(T("int"), "P").Get()
            .Init(Id("p"), false).EndProperty()), Expected.InType("int P { get; }"));
        yield return Case("InitBody", static () => InType(static type => type.Property(T("int"), "P").Get()
            .InitBody(false).EndInit().EndProperty()), Expected.InType("int P { get; }"));
        yield return Case("AccessorModifier", static () => InType(static type => type.Property(T("int"), "P").Get()
            .Private(false).Set().EndProperty()), Expected.InType("int P { get; set; }"));
        yield return Case("AccessorProtected", static () => InType(static type => type.Property(T("int"), "P").Get()
            .Protected(false).Set().EndProperty()), Expected.InType("int P { get; set; }"));
        yield return Case("AccessorInternal", static () => InType(static type => type.Property(T("int"), "P").Get()
            .Internal(false).Set().EndProperty()), Expected.InType("int P { get; set; }"));
        yield return Case("AccessorReadonly", static () => InType(static type => type.Property(T("int"), "P")
            .Readonly(false).Get(Id("p")).EndProperty()), Expected.InType("int P { get => p; }"));
        yield return Case("Indexer", static () => InType(static type => type.Public().Indexer(T("int"), false).Get()
            .EndIndexer().Field(T("int"), "f")), Expected.InType("int f;"));
        yield return Case("IndexerParam", static () => InType(static type => type.Indexer(T("int"))
                .Param(T("int"), "i", false).Param(T("int"), "j").Get(Id("j")).EndIndexer()),
            Expected.InType("int this[int j] { get => j; }"));
        yield return Case("Operator", static () => InType(static type => type.Public().Static()
            .Operator(T("C"), Add, false).EndOperator().Field(T("int"), "f")), Expected.InType("int f;"));
        yield return Case("OperatorBody", static () => InType(static type => type.Operator(T("C"), Add)
            .Body(false).Return().EndOperator()), Expected.InType("C operator +();"));
        yield return Case("UnaryOperatorDeclaration", static () => InType(static type => type
            .Operator(T("C"), UnaryOperator.Minus, false).EndOperator()), Expected.InType(""));
        yield return Case("Conversion", static () => InType(static type => type.Public()
                .Conversion(ConversionKind.Explicit, T("int"), false).EndOperator().Field(T("int"), "f")),
            Expected.InType("int f;"));
        yield return Case("Method", static () => InType(static type => type.Method(T("int"), "M", false).EndMethod()),
            Expected.InType(""));
        yield return Case("ExplicitFor", static () => InType(static type => type.ExplicitFor(T("I"), false)
            .Method(T("void"), "M").EndMethod()), Expected.InType("void M();"));
        yield return Case("Param", static () => InMethod(static method => method.Ref().Param(T("int"), "x", when: false)
            .Param(T("int"), "y")), Expected.InMethod("int y"));
        yield return Case("ParamIn", static () => InMethod(static method => method.In(false).Param(T("int"), "x")),
            Expected.InMethod("int x"));
        yield return Case("ParamThis", static () => InMethod(static method => method.This(false).Param(T("int"), "x")),
            Expected.InMethod("int x"));
        yield return Case("ParamReadonly", static () => InMethod(static method => method.Ref().Readonly(false)
            .Param(T("int"), "x")), Expected.InMethod("ref int x"));
        yield return Case("ParamAttribute", static () => InMethod(static method => method.Attribute("A", when: false)
            .Param(T("int"), "x")), Expected.InMethod("int x"));
        yield return Case("MethodTypeParam", static () => InMethod(static method => method.TypeParam("T", when: false)),
            Expected.InMethod(""));
        yield return Case("MethodWhere", static () => InType(static type => type.Method(T("void"), "M").TypeParam("T")
            .Where("T", [Constraint.Struct], false).EndMethod()), Expected.InType("void M<T>();"));
        yield return Case("MethodBody", static () => InType(static type => type.Method(T("void"), "M").Body(false)
            .Return().EndMethod()), Expected.InType("void M();"));
        yield return Case("MethodArrow", static () => InType(static type => type.Method(T("void"), "M")
            .Arrow(Id("a").Call(), false).EndMethod()), Expected.InType("void M();"));

        yield return Case("Var", static () => InBody(static body => body.Ref().Var("x", Lit(1), false).Break()),
            Expected.InBody("break;"));
        yield return Case("VarRef", static () => InBody(static body => body.Ref(false).Var("x", Lit(1))),
            Expected.InBody("var x = 1;"));
        yield return Case("VarReadonly", static () => InBody(static body => body.Ref().Readonly(false)
            .Var("x", Ref(Id("y")))), Expected.InBody("ref var x = ref y;"));
        yield return Case("Do", static () => InBody(static body => body.Do(Id("a").Call(), false)),
            Expected.InBody(""));
        yield return Case("If", static () => InBody(static body => body.If(Id("a"), when: false).Return().Else()
            .Break().EndIf()), Expected.InBody(""));
        yield return Case("ElseIf", static () => InBody(static body => body.If(Id("a")).Return()
            .ElseIf(Id("b"), when: false).Break().EndIf()), Expected.InBody("if (a)\n{\n    return;\n}"));
        yield return Case("Else", static () => InBody(static body => body.If(Id("a")).Return().Else(when: false)
            .Break().EndIf()), Expected.InBody("if (a)\n{\n    return;\n}"));
        yield return Case("Foreach", static () => InBody(static body => body.Foreach("i", Id("items"), false)
            .EndForeach()), Expected.InBody(""));
        yield return Case("Switch", static () => InBody(static body => body.Switch(Id("x"), false).EndSwitch()),
            Expected.InBody(""));
        yield return Case("Case", static () => InBody(static body => body.Switch(Id("x")).Case(Lit(1), false).Return()
                .EndCase().Case(Lit(2)).Break().EndCase().EndSwitch()),
            Expected.InBody("switch (x)\n{\n    case 2:\n        break;\n}"));
        yield return Case("Default", static () => InBody(static body => body.Switch(Id("x")).Default(false).Break()
            .EndCase().EndSwitch()), Expected.InBody("switch (x)\n{\n}"));
        yield return Case("Break", static () => InBody(static body => body.Break(false)), Expected.InBody(""));
        yield return Case("Return", static () => InBody(static body => body.Return(Lit(1), false)),
            Expected.InBody(""));
        yield return Case("Throw", static () => InBody(static body => body.Throw(New(T("E")), false)),
            Expected.InBody(""));
        yield return Case("Fixed", static () => InBody(static body => body.Fixed(T("void").Pointer(), "p",
            AddressOf(Id("x")), false).EndFixed()), Expected.InBody(""));
        yield return Case("For", static () => InBody(static body => body.For("i", Lit(0), Id("c"), Id("s"), false)
            .Break().EndFor()), Expected.InBody(""));
        yield return Case("ForParts", static () => InBody(static body => body.For("i", Lit(0, false), Id("c", false),
            Id("s", false)).Break().EndFor()), Expected.InBody("for (;;)\n{\n    break;\n}"));
        yield return Case("While", static () => InBody(static body => body.While(Id("a"), false).Break().EndWhile()),
            Expected.InBody(""));
        yield return Case("DoWhile", static () => InBody(static body => body.DoWhile(false).Break()
            .EndDoWhile(Id("a"))), Expected.InBody(""));
        yield return Case("DoWhileCondition", static () => InBody(static body => body.DoWhile().Break()
            .EndDoWhile(Id("a", false))), Expected.InBody(""));
        yield return Case("Continue", static () => InBody(static body => body.Continue(false)), Expected.InBody(""));
        yield return Case("YieldReturn", static () => InBody(static body => body.YieldReturn(Lit(1), false)),
            Expected.InBody(""));
        yield return Case("YieldBreak", static () => InBody(static body => body.YieldBreak(false)),
            Expected.InBody(""));
        yield return Case("UsingStatement", static () => InBody(static body => body.Using("s", Id("x"), false)
            .EndUsing()), Expected.InBody(""));
        yield return Case("Lock", static () => InBody(static body => body.Lock(Id("gate"), false).EndLock()),
            Expected.InBody(""));
        yield return Case("UnsafeStatement", static () => InBody(static body => body.Unsafe(false).EndUnsafe()),
            Expected.InBody(""));
        yield return Case("Try", static () => InBody(static body => body.Try(false).Return().Catch().Break()
            .Finally().Break().EndTry()), Expected.InBody(""));
        yield return Case("Catch", static () => InBody(static body => body.Try().Return().Catch(T("E"), "e", false)
            .Break().Finally().Break().EndTry()), Expected.InBody("try\n{\n    return;\n}\nfinally\n{\n    break;\n}"));
        yield return Case("CatchAll", static () => InBody(static body => body.Try().Return().Catch(false).Break()
            .Finally().Break().EndTry()), Expected.InBody("try\n{\n    return;\n}\nfinally\n{\n    break;\n}"));
        yield return Case("Finally", static () => InBody(static body => body.Try().Return().Catch().Break()
            .Finally(false).Return().EndTry()), Expected.InBody("try\n{\n    return;\n}\ncatch\n{\n    break;\n}"));

        yield return Case("Id", static () => OfExpression(Id("f").Call([Id("a", false), Id("b")])),
            Expected.OfExpression("f(b)"));
        yield return Case("Dot", static () => OfExpression(Id("a").Dot("b", false)), Expected.OfExpression("a"));
        yield return Case("NullDot", static () => OfExpression(Id("a").NullDot("b", false)),
            Expected.OfExpression("a"));
        yield return Case("Call", static () => OfExpression(Id("a").Call(when: false)), Expected.OfExpression("a"));
        yield return Case("TypeArgs", static () => OfExpression(Id("a").TypeArgs([T("int")], false).Call()),
            Expected.OfExpression("a()"));
        yield return Case("Index", static () => OfExpression(Id("a").Index([Id("i")], false)),
            Expected.OfExpression("a"));
        yield return Case("New", static () => OfExpression(Id("f").Call([New(T("A"), when: false)])),
            Expected.OfExpression("f()"));
        yield return Case("NewTargetTyped", static () => OfExpression(Id("f").Call([New([Lit(1)], false)])),
            Expected.OfExpression("f()"));
        yield return Case("Init", static () => OfExpression(New(T("A")).Init([Id("x").Assign(Lit(1))], false)),
            Expected.OfExpression("new A()"));
        yield return Case("Lit", static () => OfExpression(Id("f").Call([Lit(1, false)])),
            Expected.OfExpression("f()"));
        yield return Case("Default", static () => OfExpression(Id("f").Call([Default(when: false)])),
            Expected.OfExpression("f()"));
        yield return Case("This", static () => OfExpression(Id("f").Call([This(false)])),
            Expected.OfExpression("f()"));
        yield return Case("Assign", static () => OfExpression(Id("a").Assign(Lit(1), false)),
            Expected.OfExpression("a"));
        yield return Case("Binary", static () => OfExpression(Id("a").Binary(Add, Id("b"), false)),
            Expected.OfExpression("a"));
        yield return Case("NullForgiving", static () => OfExpression(Id("a").NullForgiving(false)),
            Expected.OfExpression("a"));
        yield return Case("AddressOf", static () => OfExpression(Id("f").Call([AddressOf(Id("a"), false)])),
            Expected.OfExpression("f()"));
        yield return Case("Is", static () => OfExpression(Id("a").Is(Lit(false), false)), Expected.OfExpression("a"));
        yield return Case("Cast", static () => OfExpression(Id("f").Call([Cast(T("int"), Id("a"), false)])),
            Expected.OfExpression("f()"));
        yield return Case("TypeOf", static () => OfExpression(Id("f").Call([TypeOf(T("A"), false)])),
            Expected.OfExpression("f()"));
        yield return Case("SizeOf", static () => OfExpression(Id("f").Call([SizeOf(T("A"), false)])),
            Expected.OfExpression("f()"));
        yield return Case("NameOf", static () => OfExpression(Id("f").Call([NameOf(Id("a"), false)])),
            Expected.OfExpression("f()"));
        yield return Case("Lambda", static () => OfExpression(Id("f").Call([Lambda(when: false).Arrow(Id("a"))])),
            Expected.OfExpression("f()"));
        yield return Case("StaticLambda", static () => OfExpression(Id("f").Call([
            StaticLambda(when: false)
                .Arrow(Id("a"))
        ])), Expected.OfExpression("f()"));
        yield return Case("LambdaArrow", static () => OfExpression(Id("f").Call([Lambda().Arrow(Id("a"), false)])),
            Expected.OfExpression("f()"));
        yield return Case("LambdaBody", static () => OfExpression(Id("f").Call([
            Lambda().Body(false).Return()
                .EndLambda()
        ])), Expected.OfExpression("f()"));
        yield return Case("SwitchExpression", static () => OfExpression(Id("x").Switch(false).Arm(Lit(1), Id("a"))
            .EndSwitch()), Expected.OfExpression("x"));
        yield return Case("Arm", static () => OfExpression(Id("x").Switch().Arm(Lit(1), Id("a"), false)
            .Arm(Discard(), Id("b")).EndSwitch()), Expected.OfExpression("x switch\n{\n    _ => b\n}"));
        yield return Case("Discard", static () => OfExpression(Id("f").Call([Discard(false)])),
            Expected.OfExpression("f()"));
        yield return Case("ThrowExpression", static () => OfExpression(Id("a")
            .Assign(ThrowExpression(New(T("E")), false))), Expected.OfExpression("a"));
        yield return Case("RefArg", static () => OfExpression(Id("f").Call([RefArg(Id("a"), false)])),
            Expected.OfExpression("f()"));
        yield return Case("InArg", static () => OfExpression(Id("f").Call([InArg(Id("a"), false)])),
            Expected.OfExpression("f()"));
        yield return Case("Named", static () => OfExpression(Id("f").Call([Named("x", Lit(1), false)])),
            Expected.OfExpression("f()"));
        yield return Case("RefExpression", static () => OfExpression(Id("f").Call([Ref(Id("a"), false)])),
            Expected.OfExpression("f()"));
        yield return Case("Null", static () => OfExpression(Id("f").Call([Null(false)])), Expected.OfExpression("f()"));
        yield return Case("Base", static () => OfExpression(Id("f").Call([Base(false)])), Expected.OfExpression("f()"));
        yield return Case("CompoundAssign", static () => OfExpression(Id("a").CompoundAssign(Add, Lit(1), false)),
            Expected.OfExpression("a"));
        yield return Case("Unary", static () => OfExpression(Id("f").Call([
            Unary(UnaryOperator.Minus, Id("b"),
                false)
        ])), Expected.OfExpression("f()"));
        yield return Case("Not", static () => OfExpression(Id("f").Call([Not(Id("b"), false)])),
            Expected.OfExpression("f()"));
        yield return Case("As", static () => OfExpression(Id("b").As(T("C"), false)), Expected.OfExpression("b"));
        yield return Case("Ternary", static () => OfExpression(Id("c").Ternary(Lit(1), Lit(2), false)),
            Expected.OfExpression("c"));
        yield return Case("Coalesce", static () => OfExpression(Id("b").Coalesce(Id("c"), false)),
            Expected.OfExpression("b"));
        yield return Case("OutArg", static () => OfExpression(Id("f").Call([OutArg(Id("a"), false)])),
            Expected.OfExpression("f()"));

        yield return Case("T", static () => OfExpression(Id("f").TypeArgs([T("int", when: false), T("A")]).Call()),
            Expected.OfExpression("f<A>()"));
        yield return Case("Predefined", static () => InType(static type => type.Field(T("ulong", when: false), "f")),
            Expected.InType(""));
        yield return Case("Nested", static () => OfType(T("A").Nested("B", when: false)), Expected.OfType("A"));
        yield return Case("NullableType", static () => OfType(T("int").Nullable(false)), Expected.OfType("int"));
        yield return Case("Pointer", static () => OfType(T("int").Pointer(false)), Expected.OfType("int"));
        yield return Case("Array", static () => OfType(T("int").Array(false)), Expected.OfType("int"));
        yield return Case("Tuple", static () => InType(static type => type.Field(Tuple([T("int")], false), "f")),
            Expected.InType(""));

        yield return Case("ForEach", static () => InType(static type => type.ForEach(["a"],
            static (scope, name) => scope.Field(T("int"), name), false)), Expected.InType(""));
    }

    private static TestCaseData Case(string block, Func<string> actual, string expected)
    {
        return new TestCaseData(actual, Expected.ConvertToTabs(expected)).SetArgDisplayNames(block);
    }
}
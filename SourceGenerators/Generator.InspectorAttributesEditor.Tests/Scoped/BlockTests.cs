using NUnit.Framework;
using Shared.CodeGeneration.Scoped;
using static Generator.InspectorAttributesEditor.Tests.Scoped.Snippets;
using static Shared.CodeGeneration.Scoped.BinaryOperator;
using static Shared.CodeGeneration.Scoped.Blocks;

namespace Generator.InspectorAttributesEditor.Tests.Scoped;

public sealed class BlockTests
{
    private static readonly Action<TypeScope> Empty = static _ => { };

    [TestCaseSource(nameof(Positive))]
    public void BlockWritesItsCode(Func<string> actual, string expected) => CodeAssert.AreEqual(expected, actual());

    [TestCaseSource(nameof(FalseCondition))]
    public void FalseConditionWritesNothing(Func<string> actual, string expected)
    {
        CodeAssert.AreEqual(expected, actual());
    }

    private static IEnumerable<TestCaseData> Positive()
    {
        yield return Case("File", () => Code.File(file => file.Class("A", Empty)), "class A\n{\n}");
        yield return Case("Nullable", () => Code.File(file => file.Nullable().Class("A", Empty)),
            "#nullable enable\nclass A\n{\n}");
        yield return Case("Using", () => Code.File(file => file.Using("System.Collections.Generic").Class("A", Empty)),
            "using System.Collections.Generic;\n\nclass A\n{\n}");
        yield return Case("UsingStatic", () => Code.File(file => file.UsingStatic("System.Math").Class("A", Empty)),
            "using static System.Math;\n\nclass A\n{\n}");
        yield return Case("Namespace", () => Code.File(file => file.Namespace("A.B", ns => ns.Class("C", Empty))),
            Expected.Tabs("namespace A.B\n{\n    class C\n    {\n    }\n}"));

        yield return Case("Comment", () => InType(type => type.Comment("Note").Field(T("int"), "x")),
            Expected.InType("// Note\nint x;"));
        yield return Case("DocComment", () => InType(type => type.DocComment(Lines("<summary>", "Text", "</summary>"))
            .Field(T("int"), "x")), Expected.InType("/// <summary>\n/// Text\n/// </summary>\nint x;"));
        yield return Case("Gap", () => InType(type => type.Field(T("int"), "x").Gap().Field(T("int"), "y")),
            Expected.InType("int x;\n\nint y;"));
        yield return Case("Define", () => Code.File(file => file.Define("X").Class("A", Empty)),
            "#define X\nclass A\n{\n}");
        yield return Case("IfDirective",
            () => Code.File(file => file.IfDirective("X").Class("A", Empty).EndIfDirective()),
            "#if X\nclass A\n{\n}\n#endif");
        yield return Case("IfDirectiveExpression", () => Code.File(file => file
            .IfDirective(Id("X").Binary(And, Id("Y")))
            .Class("A", Empty).EndIfDirective()), "#if X && Y\nclass A\n{\n}\n#endif");
        yield return Case("ElseDirective", () => InBody(body => body.IfDirective("X").Break().ElseDirective().Return()
            .EndIfDirective()), Expected.Tabs("class C\n{\n    void M()\n    {\n#if X\n        break;\n#else\n" +
                                              "        return;\n#endif\n    }\n}"));
        yield return Case("EndIfDirective", () => InBody(body => body.IfDirective("X").Break().EndIfDirective()),
            Expected.Tabs("class C\n{\n    void M()\n    {\n#if X\n        break;\n#endif\n    }\n}"));

        yield return Case("Class", () => InType(type => type.Class("N", Empty)), Expected.InType("class N\n{\n}"));
        yield return Case("Struct", () => InType(type => type.Struct("N", Empty)), Expected.InType("struct N\n{\n}"));
        yield return Case("Interface", () => InType(type => type.Interface("N", static _ => { })),
            Expected.InType("interface N\n{\n}"));
        yield return Case("Enum", () => InType(type => type.Enum("E", members => members.EnumMember("A"))),
            Expected.InType("enum E\n{\n    A\n}"));
        yield return Case("EnumUnderlyingType", () => InType(type => type.Enum("E", members => members
            .Inherits(T("ulong"))
            .EnumMember("A"))), Expected.InType("enum E : ulong\n{\n    A\n}"));
        yield return Case("Inherits", () => Code.File(file => file.Class("A", type => type.Inherits(T("B")))),
            "class A : B\n{\n}");
        yield return Case("Implements", () => Code.File(file => file.Class("A", type => type.Inherits(T("B"))
            .Implements(T("I")))), "class A : B, I\n{\n}");
        yield return Case("TypeParam",
            () => Code.File(file => file.Interface("I", type => type.TypeParam("T", Variance.Out))),
            "interface I<out T>\n{\n}");
        yield return Case("Where", () => Code.File(file => file.Class("A", type => type.TypeParam("T")
            .Where("T", [Constraint.Unmanaged, T("I")]))), "class A<T>\n    where T : unmanaged, I\n{\n}");
        yield return Case("EnumMember", () => InType(type => type.Enum("E", members => members.EnumMember("A", Lit(1))
            .EnumMember("B"))), Expected.InType("enum E\n{\n    A = 1,\n    B\n}"));

        yield return Case("Attribute", () => InType(type => type.Attribute("A")
            .Attribute("B", [Lit(1), Named("x", Lit(2))])
            .Field(T("int"), "f")), Expected.InType("[A, B(1, x: 2)]\nint f;"));
        yield return Case("SplitAttributes", () => InType(type => type.Attribute("A").SplitAttributes().Attribute("B")
            .Field(T("int"), "f")), Expected.InType("[A]\n[B]\nint f;"));

        yield return Case("Public", () => InType(type => type.Public().Field(T("int"), "f")),
            Expected.InType("public int f;"));
        yield return Case("Internal", () => InType(type => type.Internal().Field(T("int"), "f")),
            Expected.InType("internal int f;"));
        yield return Case("Private", () => InType(type => type.Private().Field(T("int"), "f")),
            Expected.InType("private int f;"));
        yield return Case("Static", () => InType(type => type.Static().Field(T("int"), "f")),
            Expected.InType("static int f;"));
        yield return Case("Readonly", () => InType(type => type.Readonly().Field(T("int"), "f")),
            Expected.InType("readonly int f;"));
        yield return Case("Sealed", () => InType(type => type.Sealed().Class("N", Empty)),
            Expected.InType("sealed class N\n{\n}"));
        yield return Case("Override", () => InType(type => type.Override().Method(T("void"), "M", static _ => { })),
            Expected.InType("override void M();"));
        yield return Case("Partial", () => InType(type => type.Partial().Class("N", Empty)),
            Expected.InType("partial class N\n{\n}"));
        yield return Case("Unsafe", () => InType(type => type.Unsafe().Field(T("int"), "f")),
            Expected.InType("unsafe int f;"));
        yield return Case("Ref", () => InType(type => type.Ref().Readonly().Method(T("int"), "M", method => method
            .Arrow(Ref(Id("x"))))), Expected.InType("ref readonly int M() => ref x;"));

        yield return Case("Field", () => InType(type => type.Field(T("int"), "f", Lit(1))),
            Expected.InType("int f = 1;"));
        yield return Case("Event", () => InType(type => type.Public().Event(T("Action").Nullable(), "E")),
            Expected.InType("public event Action? E;"));
        yield return Case("Constructor", () => InType(type => type.Public().Constructor(constructor => constructor
            .Param(T("int"), "x").Body(body => body.Return()))), Expected.InType("public C(int x)\n{\n    return;\n}"));
        yield return Case("CallsThis", () => InType(type => type.Constructor(constructor => constructor
            .CallsThis([Lit(1)])
            .Body(static _ => { }))), Expected.InType("C() : this(1)\n{\n}"));
        yield return Case("Method", () => InType(type => type.Method(T("int"), "M", method => method.Arrow(Lit(1)))),
            Expected.InType("int M() => 1;"));
        yield return Case("MethodBody", () => InType(type => type.Method(T("void"), "M", method => method
            .Body(body => body.Return()))), Expected.InType("void M()\n{\n    return;\n}"));
        yield return Case("MethodTypeParam", () => InType(type => type.Method(T("void"), "M", method => method
                .TypeParam("T").Param(T("T"), "x").Where("T", [Constraint.Struct]))),
            Expected.InType("void M<T>(T x)\n    where T : struct;"));
        yield return Case("ExplicitFor", () => InType(type => type.ExplicitFor(T("I")).Method(T("void"), "M",
            static _ => { })), Expected.InType("void I.M();"));
        yield return Case("Param", () => InMethod(method => method.Param(T("int"), "x", Lit(1)).Param(T("int"), "y")),
            Expected.InMethod("int x = 1, int y"));
        yield return Case("ParamRef", () => InMethod(method => method.Ref().Param(T("int"), "x")),
            Expected.InMethod("ref int x"));
        yield return Case("ParamIn", () => InMethod(method => method.In().Param(T("int"), "x")),
            Expected.InMethod("in int x"));
        yield return Case("ParamThis", () => InMethod(method => method.This().Param(T("int"), "x")),
            Expected.InMethod("this int x"));
        yield return Case("ParamReadonly", static () => InMethod(static method => method.Ref().Readonly()
            .Param(T("int"), "x")), Expected.InMethod("ref readonly int x"));
        yield return Case("ParamAttribute", static () => InMethod(static method => method.Attribute("A")
            .Param(T("int"), "x")), Expected.InMethod("[A] int x"));

        yield return Case("Var", () => InBody(body => body.Var("x", Lit(1))), Expected.InBody("var x = 1;"));
        yield return Case("VarRef", () => InBody(body => body.Ref().Readonly().Var("x", Ref(Id("y")))),
            Expected.InBody("ref readonly var x = ref y;"));
        yield return Case("Do", () => InBody(body => body.Do(Id("a").Call())), Expected.InBody("a();"));
        yield return Case("If", () => InBody(body => body.If(Id("a"), then => then.Return())),
            Expected.InBody("if (a)\n{\n    return;\n}"));
        yield return Case("IfUnbraced", () => InBody(body => body.If(Id("a"), then => then.Return(), braced: false)),
            Expected.InBody("if (a)\n    return;"));
        yield return Case("ElseIf", () => InBody(body => body.If(Id("a"), then => then.Return())
                .ElseIf(Id("b"), then => then.Break())),
            Expected.InBody("if (a)\n{\n    return;\n}\nelse if (b)\n{\n    break;\n}"));
        yield return Case("Else",
            () => InBody(body => body.If(Id("a"), then => then.Return()).Else(other => other.Break())),
            Expected.InBody("if (a)\n{\n    return;\n}\nelse\n{\n    break;\n}"));
        yield return Case("Foreach",
            () => InBody(body => body.Foreach("i", Id("items"), loop => loop.Do(Id("i").Call()))),
            Expected.InBody("foreach (var i in items)\n{\n    i();\n}"));
        yield return Case("Switch", () => InBody(body => body.Switch(Id("x"), sections => sections
                .Case(Lit(1), section => section.Break()))),
            Expected.InBody("switch (x)\n{\n    case 1:\n        break;\n}"));
        yield return Case("Case", () => InBody(body => body.Switch(Id("x"), sections => sections
                .Case(Lit(1), section => section.Return()).Case(Lit(2), section => section.Break()))),
            Expected.InBody("switch (x)\n{\n    case 1:\n        return;\n    case 2:\n        break;\n}"));
        yield return Case("Default", () => InBody(body => body.Switch(Id("x"), sections => sections
            .Default(section => section.Break()))), Expected.InBody("switch (x)\n{\n    default:\n        break;\n}"));
        yield return Case("Break", () => InBody(body => body.Break()), Expected.InBody("break;"));
        yield return Case("Return", () => InBody(body => body.Return(Lit(1))), Expected.InBody("return 1;"));
        yield return Case("Throw", () => InBody(body => body.Throw(New(T("E")))), Expected.InBody("throw new E();"));
        yield return Case("Fixed", () => InBody(body => body.Fixed(T("void").Pointer(), "p", AddressOf(Id("x")),
            block => block.Return())), Expected.InBody("fixed (void* p = &x)\n{\n    return;\n}"));

        yield return Case("Id", () => OfExpression(Id("a")), Expected.OfExpression("a"));
        yield return Case("IdKeyword", () => OfExpression(Id("event")), Expected.OfExpression("@event"));
        yield return Case("Dot", () => OfExpression(Id("a").Dot("b")), Expected.OfExpression("a.b"));
        yield return Case("NullDot", () => OfExpression(Id("a").NullDot("b").Call()), Expected.OfExpression("a?.b()"));
        yield return Case("Call", () => OfExpression(Id("a").Call([Id("b"), Lit(1)])),
            Expected.OfExpression("a(b, 1)"));
        yield return Case("TypeArgs", () => OfExpression(Id("a").Dot("M").TypeArgs([T("int")]).Call()),
            Expected.OfExpression("a.M<int>()"));
        yield return Case("Index", () => OfExpression(Id("a").Index([Id("i")])), Expected.OfExpression("a[i]"));
        yield return Case("New", () => OfExpression(New(T("A"), [Lit(1)])), Expected.OfExpression("new A(1)"));
        yield return Case("NewTargetTyped", () => OfExpression(New([Lit(1)])), Expected.OfExpression("new(1)"));
        yield return Case("Init", () => OfExpression(New(T("A")).Init([Id("x").Assign(Lit(1))])),
            Expected.OfExpression("new A()\n{\n    x = 1\n}"));
        yield return Case("InitInArgument",
            () => OfExpression(Id("f").Call([New(T("A")).Init([Id("x").Assign(Lit(1))])])),
            Expected.OfExpression("f(new A() { x = 1 })"));
        yield return Case("Lit", () => OfExpression(Id("f").Call([Lit("s\""), Lit(1), Lit(true), Lit('c')])),
            Expected.OfExpression("f(\"s\\\"\", 1, true, 'c')"));
        yield return Case("Default", () => OfExpression(Id("f").Call([Default(), Default(T("int"))])),
            Expected.OfExpression("f(default, default(int))"));
        yield return Case("This", () => OfExpression(This().Dot("x")), Expected.OfExpression("this.x"));
        yield return Case("Assign", () => OfExpression(Id("a").Assign(Lit(1))), Expected.OfExpression("a = 1"));
        yield return Case("Binary", () => OfExpression(Id("a").Binary(Add, Id("b")).Binary(Multiply, Id("c"))),
            Expected.OfExpression("(a + b) * c"));
        yield return Case("NullForgiving", () => OfExpression(Id("a").NullForgiving()), Expected.OfExpression("a!"));
        yield return Case("AddressOf", () => OfExpression(AddressOf(Id("a"))), Expected.OfExpression("&a"));
        yield return Case("Is", () => OfExpression(Id("a").Is(Lit(false))), Expected.OfExpression("a is false"));
        yield return Case("Cast", () => OfExpression(Cast(T("int"), Id("a")).Dot("b")),
            Expected.OfExpression("((int)a).b"));
        yield return Case("TypeOf", () => OfExpression(TypeOf(T("A"))), Expected.OfExpression("typeof(A)"));
        yield return Case("SizeOf", () => OfExpression(SizeOf(T("A"))), Expected.OfExpression("sizeof(A)"));
        yield return Case("NameOf", () => OfExpression(NameOf(Id("a"))), Expected.OfExpression("nameof(a)"));
        yield return Case("Lambda", () => OfExpression(Id("f").Call([Lambda().Arrow(Id("a").Call())])),
            Expected.OfExpression("f(() => a())"));
        yield return Case("StaticLambda",
            () => OfExpression(Id("f").Call([StaticLambda(["x"]).Body(body => body.Return())])),
            Expected.OfExpression("f(static (x) =>\n{\n    return;\n})"));
        yield return Case("SwitchExpression", () => OfExpression(Id("x").Switch(arms => arms.Arm(Lit(1), Id("a")))),
            Expected.OfExpression("x switch\n{\n    1 => a\n}"));
        yield return Case("Arm",
            () => OfExpression(Id("x").Switch(arms => arms.Arm(Lit(1), Id("a")).Arm(Discard(), Id("b")))),
            Expected.OfExpression("x switch\n{\n    1 => a,\n    _ => b\n}"));
        yield return Case("Discard", () => OfExpression(Discard().Assign(Id("a"))), Expected.OfExpression("_ = a"));
        yield return Case("ThrowExpression", () => OfExpression(Id("a").Assign(ThrowExpression(New(T("E"))))),
            Expected.OfExpression("a = throw new E()"));
        yield return Case("RefArg", () => OfExpression(Id("f").Call([RefArg(Id("a"))])),
            Expected.OfExpression("f(ref a)"));
        yield return Case("InArg", () => OfExpression(Id("f").Call([InArg(Id("a"))])),
            Expected.OfExpression("f(in a)"));
        yield return Case("Named", () => OfExpression(Id("f").Call([Named("x", Lit(1))])),
            Expected.OfExpression("f(x: 1)"));
        yield return Case("RefExpression", () => OfExpression(Ref(Id("a"))), Expected.OfExpression("ref a"));

        yield return Case("T", () => OfType(T("List", [T("int")])), Expected.OfType("List<int>"));
        yield return Case("TQualified", () => OfType(T("System.Collections.Generic.List", [T("int")])),
            Expected.OfType("System.Collections.Generic.List<int>"));
        yield return Case("Predefined", () => OfType(T("ulong")), Expected.OfType("ulong"));
        yield return Case("Nested", () => OfType(T("A", [T("int")]).Nested("B")), Expected.OfType("A<int>.B"));
        yield return Case("NullableType", () => OfType(T("int").Nullable()), Expected.OfType("int?"));
        yield return Case("Pointer", () => OfType(T("int").Pointer()), Expected.OfType("int*"));

        yield return Case("ForEach",
            () => InType(type => type.ForEach(["a", "b"], (scope, name) => scope.Field(T("int"), name))),
            Expected.InType("int a;\nint b;"));
    }

    private static IEnumerable<TestCaseData> FalseCondition()
    {
        yield return Case("File", () => Code.File(file => file.Class("A", Empty), when: false), "");
        yield return Case("Nullable", () => Code.File(file => file.Nullable(false).Class("A", Empty)), "class A\n{\n}");
        yield return Case("Using", () => Code.File(file => file.Using("System", false).Class("A", Empty)),
            "class A\n{\n}");
        yield return Case("UsingStatic",
            () => Code.File(file => file.UsingStatic("System.Math", false).Class("A", Empty)),
            "class A\n{\n}");
        yield return Case("Namespace", () => Code.File(file => file.Namespace("A", ns => ns.Class("C", Empty), false)
            .Class("B", Empty)), "class B\n{\n}");

        yield return Case("Comment", () => InType(type => type.Comment("Note", false).Field(T("int"), "x")),
            Expected.InType("int x;"));
        yield return Case("DocComment",
            () => InType(type => type.DocComment(Lines("Text"), false).Field(T("int"), "x")),
            Expected.InType("int x;"));
        yield return Case("Gap", () => InType(type => type.Field(T("int"), "x").Gap(false).Field(T("int"), "y")),
            Expected.InType("int x;\nint y;"));
        yield return Case("Define", () => Code.File(file => file.Define("X", false).Class("A", Empty)),
            "class A\n{\n}");
        yield return Case("IfDirective", () => Code.File(file => file.IfDirective("X", false).Class("A", Empty)),
            "class A\n{\n}");
        yield return Case("ElseDirective", () => InBody(body => body.Break().ElseDirective(false).Return()),
            Expected.InBody("break;\nreturn;"));
        yield return Case("EndIfDirective", () => InBody(body => body.Break().EndIfDirective(false)),
            Expected.InBody("break;"));

        yield return Case("Class", () => InType(type => type.Public().Class("N", Empty, false).Field(T("int"), "x")),
            Expected.InType("int x;"));
        yield return Case("Struct", () => InType(type => type.Struct("N", Empty, false)), Expected.InType(""));
        yield return Case("Interface", () => InType(type => type.Interface("N", static _ => { }, false)),
            Expected.InType(""));
        yield return Case("Enum", () => InType(type => type.Enum("E", static _ => { }, false)), Expected.InType(""));
        yield return Case("EnumUnderlyingType", () => InType(type => type.Enum("E", members => members
            .Inherits(T("ulong"), false).EnumMember("A"))), Expected.InType("enum E\n{\n    A\n}"));
        yield return Case("Inherits", () => Code.File(file => file.Class("A", type => type.Inherits(T("B"), false))),
            "class A\n{\n}");
        yield return Case("Implements",
            () => Code.File(file => file.Class("A", type => type.Implements(T("I"), false))),
            "class A\n{\n}");
        yield return Case("TypeParam",
            () => Code.File(file => file.Interface("I", type => type.TypeParam("T", when: false))),
            "interface I\n{\n}");
        yield return Case("Where", () => Code.File(file => file.Class("A", type => type.TypeParam("T")
            .Where("T", [Constraint.Unmanaged], false))), "class A<T>\n{\n}");
        yield return Case("EnumMember", () => InType(type => type.Enum("E", members => members
            .EnumMember("A", when: false)
            .EnumMember("B"))), Expected.InType("enum E\n{\n    B\n}"));

        yield return Case("Attribute", () => InType(type => type.Attribute("A", when: false).Field(T("int"), "f")),
            Expected.InType("int f;"));
        yield return Case("SplitAttributes", () => InType(type => type.Attribute("A").SplitAttributes(false)
            .Attribute("B")
            .Field(T("int"), "f")), Expected.InType("[A, B]\nint f;"));

        yield return Case("Public", () => InType(type => type.Public(false).Field(T("int"), "f")),
            Expected.InType("int f;"));
        yield return Case("Internal", () => InType(type => type.Internal(false).Field(T("int"), "f")),
            Expected.InType("int f;"));
        yield return Case("Private", () => InType(type => type.Private(false).Field(T("int"), "f")),
            Expected.InType("int f;"));
        yield return Case("Static", () => InType(type => type.Static(false).Field(T("int"), "f")),
            Expected.InType("int f;"));
        yield return Case("Readonly", () => InType(type => type.Readonly(false).Field(T("int"), "f")),
            Expected.InType("int f;"));
        yield return Case("Sealed", () => InType(type => type.Sealed(false).Class("N", Empty)),
            Expected.InType("class N\n{\n}"));
        yield return Case("Override",
            () => InType(type => type.Override(false).Method(T("void"), "M", static _ => { })),
            Expected.InType("void M();"));
        yield return Case("Partial", () => InType(type => type.Partial(false).Class("N", Empty)),
            Expected.InType("class N\n{\n}"));
        yield return Case("Unsafe", () => InType(type => type.Unsafe(false).Field(T("int"), "f")),
            Expected.InType("int f;"));
        yield return Case("Ref",
            () => InType(type => type.Ref(false).Method(T("int"), "M", method => method.Arrow(Id("x")))),
            Expected.InType("int M() => x;"));

        yield return Case("Field", () => InType(type => type.Private().Field(T("int"), "f", when: false)),
            Expected.InType(""));
        yield return Case("Event", () => InType(type => type.Event(T("Action"), "E", false)), Expected.InType(""));
        yield return Case("Constructor", () => InType(type => type.Constructor(static _ => { }, false)),
            Expected.InType(""));
        yield return Case("CallsThis", () => InType(type => type.Constructor(constructor => constructor
            .CallsThis(when: false)
            .Body(static _ => { }))), Expected.InType("C()\n{\n}"));
        yield return Case("Method", () => InType(type => type.Method(T("int"), "M", static _ => { }, false)),
            Expected.InType(""));
        yield return Case("ExplicitFor", () => InType(type => type.ExplicitFor(T("I"), false).Method(T("void"), "M",
            static _ => { })), Expected.InType("void M();"));
        yield return Case("Param",
            () => InMethod(method => method.Ref().Param(T("int"), "x", when: false).Param(T("int"), "y")),
            Expected.InMethod("int y"));
        yield return Case("ParamIn", () => InMethod(method => method.In(false).Param(T("int"), "x")),
            Expected.InMethod("int x"));
        yield return Case("ParamThis", () => InMethod(method => method.This(false).Param(T("int"), "x")),
            Expected.InMethod("int x"));
        yield return Case("ParamReadonly", static () => InMethod(static method => method.Ref().Readonly(false)
            .Param(T("int"), "x")), Expected.InMethod("ref int x"));
        yield return Case("ParamAttribute", static () => InMethod(static method => method.Attribute("A", when: false)
            .Param(T("int"), "x")), Expected.InMethod("int x"));
        yield return Case("MethodTypeParam", static () => InMethod(static method => method.TypeParam("T", when: false)),
            Expected.InMethod(""));
        yield return Case("MethodWhere", static () => InType(static type => type.Method(T("void"), "M", static method =>
            method.TypeParam("T").Where("T", [Constraint.Struct], false))), Expected.InType("void M<T>();"));
        yield return Case("MethodBody", static () => InType(static type => type.Method(T("void"), "M", static method =>
            method.Body(static body => body.Return(), false))), Expected.InType("void M();"));
        yield return Case("MethodArrow", static () => InType(static type => type.Method(T("void"), "M", static method =>
            method.Arrow(Id("a").Call(), false))), Expected.InType("void M();"));

        yield return Case("Var", () => InBody(body => body.Ref().Var("x", Lit(1), false).Break()),
            Expected.InBody("break;"));
        yield return Case("VarRef", static () => InBody(static body => body.Ref(false).Var("x", Lit(1))),
            Expected.InBody("var x = 1;"));
        yield return Case("VarReadonly", static () => InBody(static body => body.Ref().Readonly(false)
            .Var("x", Ref(Id("y")))), Expected.InBody("ref var x = ref y;"));
        yield return Case("Do", () => InBody(body => body.Do(Id("a").Call(), false)), Expected.InBody(""));
        yield return Case("If", () => InBody(body => body.If(Id("a"), then => then.Return(), when: false)
            .Else(other => other.Break())), Expected.InBody(""));
        yield return Case("ElseIf", () => InBody(body => body.If(Id("a"), then => then.Return())
            .ElseIf(Id("b"), then => then.Break(), when: false)), Expected.InBody("if (a)\n{\n    return;\n}"));
        yield return Case("Else", () => InBody(body => body.If(Id("a"), then => then.Return())
            .Else(other => other.Break(), when: false)), Expected.InBody("if (a)\n{\n    return;\n}"));
        yield return Case("Foreach", () => InBody(body => body.Foreach("i", Id("items"), static _ => { }, false)),
            Expected.InBody(""));
        yield return Case("Switch", () => InBody(body => body.Switch(Id("x"), static _ => { }, false)),
            Expected.InBody(""));
        yield return Case("Case", () => InBody(body => body.Switch(Id("x"), sections => sections
                .Case(Lit(1), section => section.Return(), false).Case(Lit(2), section => section.Break()))),
            Expected.InBody("switch (x)\n{\n    case 2:\n        break;\n}"));
        yield return Case("Default", () => InBody(body => body.Switch(Id("x"), sections => sections
            .Default(section => section.Break(), false))), Expected.InBody("switch (x)\n{\n}"));
        yield return Case("Break", () => InBody(body => body.Break(false)), Expected.InBody(""));
        yield return Case("Return", () => InBody(body => body.Return(Lit(1), false)), Expected.InBody(""));
        yield return Case("Throw", () => InBody(body => body.Throw(New(T("E")), false)), Expected.InBody(""));
        yield return Case("Fixed", () => InBody(body => body.Fixed(T("void").Pointer(), "p", AddressOf(Id("x")),
            static _ => { }, false)), Expected.InBody(""));

        yield return Case("Id", () => OfExpression(Id("f").Call([Id("a", false), Id("b")])),
            Expected.OfExpression("f(b)"));
        yield return Case("Dot", () => OfExpression(Id("a").Dot("b", false)), Expected.OfExpression("a"));
        yield return Case("NullDot", () => OfExpression(Id("a").NullDot("b", false)), Expected.OfExpression("a"));
        yield return Case("Call", () => OfExpression(Id("a").Call(when: false)), Expected.OfExpression("a"));
        yield return Case("TypeArgs", () => OfExpression(Id("a").TypeArgs([T("int")], false).Call()),
            Expected.OfExpression("a()"));
        yield return Case("Index", () => OfExpression(Id("a").Index([Id("i")], false)), Expected.OfExpression("a"));
        yield return Case("New", () => OfExpression(Id("f").Call([New(T("A"), when: false)])),
            Expected.OfExpression("f()"));
        yield return Case("Init", () => OfExpression(New(T("A")).Init([Id("x").Assign(Lit(1))], false)),
            Expected.OfExpression("new A()"));
        yield return Case("Lit", () => OfExpression(Id("f").Call([Lit(1, false)])), Expected.OfExpression("f()"));
        yield return Case("Default", () => OfExpression(Id("f").Call([Default(when: false)])),
            Expected.OfExpression("f()"));
        yield return Case("This", () => OfExpression(Id("f").Call([This(false)])), Expected.OfExpression("f()"));
        yield return Case("Assign", () => OfExpression(Id("a").Assign(Lit(1), false)), Expected.OfExpression("a"));
        yield return Case("Binary", () => OfExpression(Id("a").Binary(Add, Id("b"), false)),
            Expected.OfExpression("a"));
        yield return Case("NullForgiving", () => OfExpression(Id("a").NullForgiving(false)),
            Expected.OfExpression("a"));
        yield return Case("AddressOf", () => OfExpression(Id("f").Call([AddressOf(Id("a"), false)])),
            Expected.OfExpression("f()"));
        yield return Case("Is", () => OfExpression(Id("a").Is(Lit(false), false)), Expected.OfExpression("a"));
        yield return Case("Cast", () => OfExpression(Id("f").Call([Cast(T("int"), Id("a"), false)])),
            Expected.OfExpression("f()"));
        yield return Case("TypeOf", () => OfExpression(Id("f").Call([TypeOf(T("A"), false)])),
            Expected.OfExpression("f()"));
        yield return Case("SizeOf", () => OfExpression(Id("f").Call([SizeOf(T("A"), false)])),
            Expected.OfExpression("f()"));
        yield return Case("NameOf", () => OfExpression(Id("f").Call([NameOf(Id("a"), false)])),
            Expected.OfExpression("f()"));
        yield return Case("Lambda", () => OfExpression(Id("f").Call([Lambda(when: false).Arrow(Id("a"))])),
            Expected.OfExpression("f()"));
        yield return Case("StaticLambda", static () => OfExpression(Id("f").Call([StaticLambda(when: false)
            .Arrow(Id("a"))])), Expected.OfExpression("f()"));
        yield return Case("LambdaArrow", static () => OfExpression(Id("f").Call([Lambda().Arrow(Id("a"), false)])),
            Expected.OfExpression("f()"));
        yield return Case("LambdaBody", static () => OfExpression(Id("f").Call([Lambda()
            .Body(static body => body.Return(), false)])), Expected.OfExpression("f()"));
        yield return Case("NewTargetTyped", static () => OfExpression(Id("f").Call([New([Lit(1)], false)])),
            Expected.OfExpression("f()"));
        yield return Case("SwitchExpression",
            () => OfExpression(Id("x").Switch(arms => arms.Arm(Lit(1), Id("a")), false)),
            Expected.OfExpression("x"));
        yield return Case("Arm", () => OfExpression(Id("x").Switch(arms => arms.Arm(Lit(1), Id("a"), false)
            .Arm(Discard(), Id("b")))), Expected.OfExpression("x switch\n{\n    _ => b\n}"));
        yield return Case("Discard", () => OfExpression(Id("f").Call([Discard(false)])), Expected.OfExpression("f()"));
        yield return Case("ThrowExpression", () => OfExpression(Id("a").Assign(ThrowExpression(New(T("E")), false))),
            Expected.OfExpression("a"));
        yield return Case("RefArg", () => OfExpression(Id("f").Call([RefArg(Id("a"), false)])),
            Expected.OfExpression("f()"));
        yield return Case("InArg", () => OfExpression(Id("f").Call([InArg(Id("a"), false)])),
            Expected.OfExpression("f()"));
        yield return Case("Named", () => OfExpression(Id("f").Call([Named("x", Lit(1), false)])),
            Expected.OfExpression("f()"));
        yield return Case("RefExpression", () => OfExpression(Id("f").Call([Ref(Id("a"), false)])),
            Expected.OfExpression("f()"));

        yield return Case("T", () => OfExpression(Id("f").TypeArgs([T("int", when: false), T("A")]).Call()),
            Expected.OfExpression("f<A>()"));
        yield return Case("Predefined", static () => InType(static type => type.Field(T("ulong", when: false), "f")),
            Expected.InType(""));
        yield return Case("Nested", () => OfType(T("A").Nested("B", when: false)), Expected.OfType("A"));
        yield return Case("NullableType", () => OfType(T("int").Nullable(false)), Expected.OfType("int"));
        yield return Case("Pointer", () => OfType(T("int").Pointer(false)), Expected.OfType("int"));

        yield return Case("ForEach", () => InType(type => type.ForEach(["a"],
            (scope, name) => scope.Field(T("int"), name),
            false)), Expected.InType(""));
    }

    private static TestCaseData Case(string block, Func<string> actual, string expected)
    {
        return new TestCaseData(actual, Expected.Tabs(expected)).SetArgDisplayNames(block);
    }
}
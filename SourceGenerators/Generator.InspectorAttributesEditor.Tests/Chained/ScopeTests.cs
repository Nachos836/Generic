using NUnit.Framework;

namespace Generator.InspectorAttributesEditor.Tests.Chained;

public sealed class ScopeTests
{
    private const string MemberMissing = "CS1061";

    [TestCase("Code.File().Class(\"A\").Method(T(\"void\"), \"M\").Body().EndMethod().EndClass().EndFile()")]
    [TestCase("Code.File().Namespace(\"N\").Class(\"A\").EndClass().EndNamespace().EndFile()")]
    [TestCase("Code.File().Class(\"A\").Method(T(\"void\"), \"M\").Body().If(Id(\"x\")).Return().Else().Break()" +
              ".EndIf().EndMethod().EndClass().EndFile()")]
    [TestCase("Code.File().Class(\"A\").Method(T(\"void\"), \"M\").Body().Switch(Id(\"x\")).Case(Lit(1)).Break()" +
              ".EndCase().EndSwitch().EndMethod().EndClass().EndFile()")]
    [TestCase("Code.File().Interface(\"I\").Method(T(\"void\"), \"M\").EndMethod().Event(T(\"Action\"), \"E\")" +
              ".EndInterface().EndFile()")]
    public void MatchingClosingVerbsCompile(string chain)
    {
        Assert.That(CompileCheck.CollectErrors(DescribeProbe(chain)), Is.Empty);
    }

    [TestCase("Code.File().Class(\"A\").Method(T(\"void\"), \"M\").Body().EndClass().EndFile()")]
    [TestCase("Code.File().Namespace(\"N\").EndClass().EndFile()")]
    [TestCase("Code.File().Class(\"A\").EndNamespace().EndFile()")]
    [TestCase("Code.File().Class(\"A\").Struct(\"S\").EndClass().EndClass().EndFile()")]
    [TestCase("Code.File().Class(\"A\").Method(T(\"void\"), \"M\").Body().If(Id(\"x\")).EndMethod().EndClass()" +
              ".EndFile()")]
    [TestCase("Code.File().Class(\"A\").Method(T(\"void\"), \"M\").Body().Switch(Id(\"x\")).Case(Lit(1))" +
              ".EndSwitch().EndMethod().EndClass().EndFile()")]
    public void ForeignClosingVerbFailsToCompile(string chain)
    {
        Assert.That(CompileCheck.CollectErrors(DescribeProbe(chain)), Is.EqualTo(new[] { MemberMissing }));
    }

    [TestCase("Code.File().Interface(\"I\").Constructor().EndConstructor().EndInterface().EndFile()")]
    [TestCase("Code.File().Interface(\"I\").Field(T(\"int\"), \"f\").EndInterface().EndFile()")]
    [TestCase("Code.File().Interface(\"I\").Inherits(T(\"B\")).EndInterface().EndFile()")]
    public void InterfaceRejectsClassMembers(string chain)
    {
        Assert.That(CompileCheck.CollectErrors(DescribeProbe(chain)), Is.EqualTo(new[] { MemberMissing }));
    }

    [TestCase("Body().For(\"i\", Lit(0), Id(\"c\"), Id(\"s\")).EndFor().While(Id(\"a\")).EndWhile().DoWhile()" +
              ".EndDoWhile(Id(\"a\")).EndMethod()")]
    [TestCase("Body().Using(\"s\", Id(\"x\")).EndUsing().Lock(Id(\"x\")).EndLock().Unsafe().EndUnsafe().EndMethod()")]
    [TestCase("Body().Try().Catch(T(\"E\"), \"e\").Catch().Finally().EndTry().Try().Catch().EndTry().EndMethod()")]
    public void MatchingStatementVerbsCompile(string body)
    {
        var chain = $"Code.File().Class(\"A\").Method(T(\"void\"), \"M\").{body}.EndClass().EndFile()";
        Assert.That(CompileCheck.CollectErrors(DescribeProbe(chain)), Is.Empty);
    }

    [TestCase("Property(T(\"int\"), \"P\").GetBody().EndGet().SetBody().EndSet().InitBody().EndInit().EndProperty()")]
    [TestCase("Indexer(T(\"int\")).Param(T(\"int\"), \"i\").Get().EndIndexer()")]
    [TestCase("Operator(T(\"A\"), Add).Body().EndOperator().Conversion(ConversionKind.Implicit, T(\"int\"))" +
              ".EndOperator()")]
    [TestCase("Record(\"R\").EndRecord()")]
    public void MatchingMemberVerbsCompile(string member)
    {
        var chain = $"Code.File().Class(\"A\").{member}.EndClass().EndFile()";
        Assert.That(CompileCheck.CollectErrors(DescribeProbe(chain)), Is.Empty);
    }

    [TestCase("Body().For(\"i\", Lit(0), Id(\"c\"), Id(\"s\")).EndWhile().EndMethod()")]
    [TestCase("Body().DoWhile().EndWhile().EndMethod()")]
    [TestCase("Body().Using(\"s\", Id(\"x\")).EndLock().EndMethod()")]
    [TestCase("Body().Try().EndFor().EndMethod()")]
    [TestCase("Body().Try().Finally().EndFinally().EndMethod()")]
    public void ForeignStatementVerbFailsToCompile(string body)
    {
        var chain = $"Code.File().Class(\"A\").Method(T(\"void\"), \"M\").{body}.EndClass().EndFile()";
        Assert.That(CompileCheck.CollectErrors(DescribeProbe(chain)), Is.EqualTo(new[] { MemberMissing }));
    }

    [TestCase("Property(T(\"int\"), \"P\").GetBody().EndSet().EndProperty()")]
    [TestCase("Property(T(\"int\"), \"P\").EndIndexer()")]
    [TestCase("Operator(T(\"A\"), Add).Body().EndMethod()")]
    [TestCase("Record(\"R\").EndClass()")]
    [TestCase("Property(T(\"int\"), \"P\").Comment(\"c\").EndProperty()")]
    [TestCase("Indexer(T(\"int\")).Gap().EndIndexer()")]
    public void ForeignMemberVerbFailsToCompile(string member)
    {
        var chain = $"Code.File().Class(\"A\").{member}.EndClass().EndFile()";
        Assert.That(CompileCheck.CollectErrors(DescribeProbe(chain)), Is.EqualTo(new[] { MemberMissing }));
    }

    [TestCase("Private")]
    [TestCase("Protected")]
    [TestCase("Virtual")]
    [TestCase("Override")]
    [TestCase("New")]
    [TestCase("Extern")]
    [TestCase("Async")]
    [TestCase("Const")]
    [TestCase("Volatile")]
    [TestCase("Required")]
    public void ContainerRejectsMemberModifiers(string modifier)
    {
        var chain = $"Code.File().{modifier}().Class(\"A\").EndClass().EndFile()";
        Assert.That(CompileCheck.CollectErrors(DescribeProbe(chain)), Is.EqualTo(new[] { MemberMissing }));
    }

    [TestCase("Code.File().Class(\"A\").FileLocal().EndClass().EndFile()")]
    [TestCase("Code.File().Interface(\"I\").Const().EndInterface().EndFile()")]
    [TestCase("Code.File().Interface(\"I\").Volatile().EndInterface().EndFile()")]
    [TestCase("Code.File().Interface(\"I\").Required().EndInterface().EndFile()")]
    public void ScopeRejectsForeignModifier(string chain)
    {
        Assert.That(CompileCheck.CollectErrors(DescribeProbe(chain)), Is.EqualTo(new[] { MemberMissing }));
    }

    [TestCase("Code.File().Class(\"A\").Member(null!).EndClass().EndFile()")]
    [TestCase("Code.File().Class(\"A\").Method(T(\"void\"), \"M\").Body().Statement(null!).EndMethod().EndClass()" +
              ".EndFile()")]
    [TestCase("Code.File().Class(\"A\").AddExtension(null!, true).EndClass().EndFile()")]
    public void RoslynBlocksNeedExtensionsNamespace(string chain)
    {
        Assert.That(CompileCheck.CollectErrors(DescribeProbe(chain)), Is.EqualTo(new[] { MemberMissing }));
    }

    private static string DescribeProbe(string chain)
    {
        return $$"""
                 using Shared.CodeGeneration.Chained;
                 using static Shared.CodeGeneration.Chained.BinaryOperator;
                 using static Shared.CodeGeneration.Chained.Blocks;

                 internal static class Probe
                 {
                     public static string Run() => {{chain}};
                 }
                 """;
    }
}
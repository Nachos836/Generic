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

    private static string DescribeProbe(string chain)
    {
        return $$"""
                 using Shared.CodeGeneration.Chained;
                 using static Shared.CodeGeneration.Chained.Blocks;

                 internal static class Probe
                 {
                     public static string Run() => {{chain}};
                 }
                 """;
    }
}

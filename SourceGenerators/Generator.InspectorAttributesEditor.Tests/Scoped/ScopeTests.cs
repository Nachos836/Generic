using NUnit.Framework;

namespace Generator.InspectorAttributesEditor.Tests.Scoped;

public sealed class ScopeTests
{
    private const string MemberMissing = "CS1061";

    [TestCase("scope.Method(T(\"void\"), \"M\", static _ => { })")]
    [TestCase("scope.Event(T(\"Action\"), \"E\")")]
    public void InterfaceOffersItsMembers(string block)
    {
        Assert.That(CompileCheck.CollectErrors(DescribeInterface(block)), Is.Empty);
    }

    [TestCase("scope.Constructor(static _ => { })")]
    [TestCase("scope.Field(T(\"int\"), \"f\")")]
    [TestCase("scope.Inherits(T(\"B\"))")]
    public void InterfaceRejectsClassMembers(string block)
    {
        Assert.That(CompileCheck.CollectErrors(DescribeInterface(block)), Is.EqualTo(new[] { MemberMissing }));
    }

    private static string DescribeInterface(string block)
    {
        return $$"""
                 using Shared.CodeGeneration.Scoped;
                 using static Shared.CodeGeneration.Scoped.Blocks;

                 internal static class Probe
                 {
                     public static string Run() => Code.File(static file => file
                         .Interface("I", static scope => {{block}}));
                 }
                 """;
    }
}
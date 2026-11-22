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
        var probe = DescribeProbe($"Code.File(static file => file.{modifier}().Class(\"A\", static _ => {{ }}))");
        Assert.That(CompileCheck.CollectErrors(probe), Is.EqualTo(new[] { MemberMissing }));
    }

    [Test]
    public void TypeRejectsFileLocal()
    {
        var probe = DescribeProbe("Code.File(static file => file.Class(\"A\", static scope => scope.FileLocal()))");
        Assert.That(CompileCheck.CollectErrors(probe), Is.EqualTo(new[] { MemberMissing }));
    }

    [TestCase("Comment(\"c\")")]
    [TestCase("Gap()")]
    [TestCase("IfDirective(\"X\")")]
    public void AccessorsRejectTrivia(string block)
    {
        var probe = DescribeProbe("Code.File(static file => file.Class(\"A\", static scope => scope" +
            $".Property(T(\"int\"), \"P\", static accessors => accessors.{block}.Get())))");
        Assert.That(CompileCheck.CollectErrors(probe), Is.EqualTo(new[] { MemberMissing }));
    }

    [TestCase("scope.Const()")]
    [TestCase("scope.Volatile()")]
    [TestCase("scope.Required()")]
    public void InterfaceRejectsFieldModifiers(string block)
    {
        Assert.That(CompileCheck.CollectErrors(DescribeInterface(block)), Is.EqualTo(new[] { MemberMissing }));
    }

    private static string DescribeInterface(string block)
    {
        return DescribeProbe($"Code.File(static file => file.Interface(\"I\", static scope => {block}))");
    }

    private static string DescribeProbe(string description)
    {
        return $$"""
                 using Shared.CodeGeneration.Scoped;
                 using static Shared.CodeGeneration.Scoped.Blocks;

                 internal static class Probe
                 {
                     public static string Run() => {{description}};
                 }
                 """;
    }
}
using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Shared.CodeGeneration.Scoped.Rendering;
using static Microsoft.CodeAnalysis.CSharp.SyntaxFactory;

namespace Shared.CodeGeneration.Scoped;

internal sealed class SwitchScope : Scope<SwitchScope>
{
    private readonly List<SwitchSectionSyntax> _sections = [];

    internal SwitchScope() : base(new())
    {
    }

    private protected override SwitchScope Next => this;

    public SwitchScope Case(Expr label, Action<BodyScope> body, bool when = true)
    {
        return when && label.Node is { } node ? AddSection(CaseSwitchLabel(node), body) : this;
    }

    public SwitchScope Default(Action<BodyScope> body, bool when = true)
    {
        return when ? AddSection(DefaultSwitchLabel(), body) : this;
    }

    internal SwitchStatementSyntax Complete(SwitchStatementSyntax statement)
    {
        return statement
            .WithSections(List(_sections))
            .WithCloseBraceToken(Lead.Close(Token(SyntaxKind.CloseBraceToken)));
    }

    private SwitchScope AddSection(SwitchLabelSyntax label, Action<BodyScope> body)
    {
        _sections.Add(SwitchSection(SingletonList(Lead.Attach(label)), List(BodyScope.BuildSection(Lead, body))));
        return this;
    }
}

internal sealed class ArmScope : Scope<ArmScope>
{
    private readonly List<SwitchExpressionArmSyntax> _arms = [];

    internal ArmScope() : base(new())
    {
    }

    private protected override ArmScope Next => this;

    public ArmScope Arm(Expr pattern, Expr result, bool when = true)
    {
        if (when && pattern.Node is { } patternNode && result.Node is { } resultNode)
        {
            _arms.Add(Lead.Attach(SwitchExpressionArm(Expr.BuildPattern(patternNode), resultNode)));
        }

        return this;
    }

    internal SwitchExpressionSyntax Complete(SwitchExpressionSyntax expression)
    {
        return expression
            .WithArms(SeparatedList(_arms, Enumerable.Range(1, Math.Max(_arms.Count - 1, 0)).Select(ChooseSeparator)))
            .WithCloseBraceToken(Lead.Close(Token(SyntaxKind.CloseBraceToken)));
    }

    private SyntaxToken ChooseSeparator(int nextArm)
    {
        var opensAlternative =
            HasDirective(_arms[nextArm], SyntaxKind.ElseDirectiveTrivia, SyntaxKind.ElifDirectiveTrivia);

        var closesBeforeEnd = _arms.Skip(nextArm + 1)
            .Any(static arm => HasDirective(arm, SyntaxKind.EndIfDirectiveTrivia));
        return opensAlternative && closesBeforeEnd is false
            ? MissingToken(SyntaxKind.CommaToken)
            : Token(SyntaxKind.CommaToken);
    }

    private static bool HasDirective(SyntaxNode node, params SyntaxKind[] kinds)
    {
        return node.GetLeadingTrivia().Any(trivia => kinds.Any(kind => trivia.IsKind(kind)));
    }
}
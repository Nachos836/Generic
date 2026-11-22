using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Shared.CodeGeneration.Chained.Rendering;
using static Microsoft.CodeAnalysis.CSharp.SyntaxFactory;

namespace Shared.CodeGeneration.Chained;

internal sealed class SwitchChain<TOuter> : Chain<SwitchChain<TOuter>> where TOuter : class, IStatementHost
{
    private readonly List<SwitchSectionSyntax> _sections = [];
    private readonly TOuter _outer = default!;
    private readonly Leading _leading = default!;
    private readonly SwitchStatementSyntax _header = default!;
    private readonly bool _active;

    internal SwitchChain(TOuter outer, Leading leading, SwitchStatementSyntax header, bool active) : base(new())
    {
        _outer = outer;
        _leading = leading;
        _header = header;
        _active = active;
    }

    public CaseChain<TOuter> Case(Expr label, bool when = true)
    {
        var active = when && label.Node is not null;
        SwitchLabelSyntax caseLabel = CaseSwitchLabel(label.Node ?? IdentifierName("_"));
        return new(this, active ? Lead.Attach(caseLabel) : caseLabel, active, Lead);
    }

    public CaseChain<TOuter> Default(bool when = true)
    {
        SwitchLabelSyntax defaultLabel = DefaultSwitchLabel();
        return new(this, when ? Lead.Attach(defaultLabel) : defaultLabel, when, Lead);
    }

    public TOuter EndSwitch()
    {
        var statement = _header
            .WithSections(List(_sections))
            .WithCloseBraceToken(Lead.Close(Token(SyntaxKind.CloseBraceToken)));

        if (_active) _outer.AddStatement(_leading.Apply(statement));
        return _outer;
    }

    internal SwitchChain<TOuter> AddSection(SwitchLabelSyntax label, List<StatementSyntax> statements, bool active)
    {
        if (active) _sections.Add(SwitchSection(SingletonList(label), List(statements)));
        return this;
    }
}

internal sealed class CaseChain<TOuter> : StatementChain<CaseChain<TOuter>> where TOuter : class, IStatementHost
{
    private readonly SwitchChain<TOuter> _switch = default!;
    private readonly SwitchLabelSyntax _label = default!;
    private readonly bool _active;

    internal CaseChain(SwitchChain<TOuter> @switch, SwitchLabelSyntax label, bool active, Lead lead) : base(lead)
    {
        _switch = @switch;
        _label = label;
        _active = active;
    }

    public SwitchChain<TOuter> EndCase() => _switch.AddSection(_label, TakeStatements(), _active);
}

internal sealed class ArmChain : Chain<ArmChain>
{
    private readonly List<SwitchExpressionArmSyntax> _arms = [];
    private readonly Expr _receiver = default!;
    private readonly bool _active;

    internal ArmChain(Expr receiver, bool active) : base(new())
    {
        _receiver = receiver;
        _active = active;
    }

    public ArmChain Arm(Expr pattern, Expr result, bool when = true)
    {
        if (when && pattern.Node is { } patternNode && result.Node is { } resultNode)
        {
            _arms.Add(Lead.Attach(SwitchExpressionArm(Expr.BuildPattern(patternNode), resultNode)));
        }

        return this;
    }

    public Expr EndSwitch()
    {
        if (_active is false || _receiver.Node is null) return _receiver;

        var expression = SwitchExpression(Precedence.Parenthesize(_receiver.Node, Precedence.Unary))
            .WithArms(SeparatedList(_arms, Enumerable.Range(1, Math.Max(_arms.Count - 1, 0)).Select(ChooseSeparator)))
            .WithCloseBraceToken(Lead.Close(Token(SyntaxKind.CloseBraceToken)));
        return new(expression);
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
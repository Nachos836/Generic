using System.Collections.Generic;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Shared.CodeGeneration.Chained.Rendering;
using static Microsoft.CodeAnalysis.CSharp.SyntaxFactory;

namespace Shared.CodeGeneration.Chained;

internal abstract class StatementChain<TSelf> : Chain<TSelf>, IStatementHost where TSelf : StatementChain<TSelf>
{
    private readonly List<StatementSyntax> _statements = [];

    private protected StatementChain(Lead lead) : base(lead)
    {
    }

    public TSelf Ref(bool when = true) => AddModifier(SyntaxKind.RefKeyword, when);
    public TSelf Readonly(bool when = true) => AddModifier(SyntaxKind.ReadOnlyKeyword, when);

    public TSelf Var(string name, Expr? value = null, bool when = true)
    {
        if (when is false) return DropPrefixes();

        var (modifiers, type) = Declarations.SplitRef(Lead.TakeModifiers(), IdentifierName("var"));
        var variable = SingletonSeparatedList(Declarations.BuildVariable(name, value));
        return Add(LocalDeclarationStatement(VariableDeclaration(type, variable)).WithModifiers(modifiers));
    }

    public TSelf Do(Expr expression, bool when = true)
    {
        return when && expression.Node is { } node ? Add(ExpressionStatement(node)) : DropPrefixes();
    }

    public TSelf Break(bool when = true) => when ? Add(BreakStatement()) : DropPrefixes();

    public TSelf Return(Expr? value = null, bool when = true)
    {
        return when ? Add(ReturnStatement(value?.Node)) : DropPrefixes();
    }

    public TSelf Throw(Expr exception, bool when = true)
    {
        return when && exception.Node is { } node ? Add(ThrowStatement(node)) : DropPrefixes();
    }

    public IfChain<TSelf> If(Expr condition, bool braced = true, bool when = true)
    {
        var active = when && condition.Node is not null;
        return new(Self, Open(active).Leading, condition.Node, braced, active);
    }

    public ForeachChain<TSelf> Foreach(string name, Expr collection, bool when = true)
    {
        var active = when && collection.Node is not null;
        var header = ForEachStatement(IdentifierName("var"), Names.CreateIdentifier(name),
            collection.Node ?? IdentifierName("_"), Block());
        return new(Self, Open(active).Leading, header, active);
    }

    public SwitchChain<TSelf> Switch(Expr value, bool when = true)
    {
        var active = when && value.Node is not null;
        return new(Self, Open(active).Leading, SwitchStatement(value.Node ?? IdentifierName("_")), active);
    }

    public FixedChain<TSelf> Fixed(TypeRef type, string name, Expr value, bool when = true)
    {
        var active = when && type.Node is not null && value.Node is not null;
        var declarator = Declarations.BuildVariable(name, value);
        var declaration = VariableDeclaration(type.Node ?? IdentifierName("var"), SingletonSeparatedList(declarator));
        return new(Self, Open(active).Leading, FixedStatement(declaration, Block()), active);
    }

    void IStatementHost.AddStatement(StatementSyntax statement)
    {
        _statements.Add(statement);
        Lead.DropPrefixes();
    }

    private protected TSelf Add(StatementSyntax statement)
    {
        _statements.Add(Lead.Attach(statement));
        return DropPrefixes();
    }

    private protected BlockSyntax BuildBlock()
    {
        var block = Block(_statements).WithCloseBraceToken(Lead.Close(Token(SyntaxKind.CloseBraceToken)));
        _statements.Clear();
        return block;
    }

    private protected StatementSyntax BuildStatement(bool braced)
    {
        var closed = Lead.HasPending;
        var block = BuildBlock();
        var keepsBraces = braced || closed || block.Statements.Count != 1 || block.Statements[0] is IfStatementSyntax;
        return keepsBraces ? block : block.Statements[0];
    }

    private protected List<StatementSyntax> TakeStatements()
    {
        var statements = new List<StatementSyntax>(_statements);
        _statements.Clear();
        return statements;
    }
}

internal sealed class IfChain<TOuter> : StatementChain<IfChain<TOuter>> where TOuter : class, IStatementHost
{
    private readonly List<(ExpressionSyntax Condition, StatementSyntax Body)> _branches = [];
    private readonly TOuter _outer = default!;
    private readonly Leading _leading = default!;
    private readonly bool _active;
    private ExpressionSyntax? _condition = default;
    private bool _braced;
    private bool _branchActive;

    internal IfChain(TOuter outer, Leading leading, ExpressionSyntax? condition, bool braced, bool active)
        : base(new())
    {
        _outer = outer;
        _leading = leading;
        _condition = condition;
        _braced = braced;
        _active = active;
        _branchActive = active;
    }

    public IfChain<TOuter> ElseIf(Expr condition, bool braced = true, bool when = true)
    {
        CloseBranch();
        _condition = condition.Node;
        _braced = braced;
        _branchActive = when && condition.Node is not null;
        return this;
    }

    public ElseChain<TOuter> Else(bool braced = true, bool when = true)
    {
        CloseBranch();
        return new(this, braced, when);
    }

    public TOuter EndIf()
    {
        CloseBranch();
        return Finish(null);
    }

    internal TOuter Finish(StatementSyntax? otherwise)
    {
        if (_active && _branches.Count > 0) _outer.AddStatement(_leading.Apply(BuildIf(0, otherwise)));
        return _outer;
    }

    private void CloseBranch()
    {
        var body = BuildStatement(_braced);
        if (_branchActive && _condition != default) _branches.Add((_condition, body));
    }

    private IfStatementSyntax BuildIf(int index, StatementSyntax? otherwise)
    {
        var statement = IfStatement(_branches[index].Condition, _branches[index].Body);
        var next = index + 1 < _branches.Count ? BuildIf(index + 1, otherwise) : otherwise;
        return next == default ? statement : statement.WithElse(ElseClause(next));
    }
}

internal sealed class ElseChain<TOuter> : StatementChain<ElseChain<TOuter>> where TOuter : class, IStatementHost
{
    private readonly IfChain<TOuter> _if = default!;
    private readonly bool _braced;
    private readonly bool _active;

    internal ElseChain(IfChain<TOuter> @if, bool braced, bool active) : base(new())
    {
        _if = @if;
        _braced = braced;
        _active = active;
    }

    public TOuter EndIf()
    {
        var body = BuildStatement(_braced);
        return _if.Finish(_active ? body : null);
    }
}

internal sealed class ForeachChain<TOuter> : StatementChain<ForeachChain<TOuter>> where TOuter : class, IStatementHost
{
    private readonly TOuter _outer = default!;
    private readonly Leading _leading = default!;
    private readonly ForEachStatementSyntax _header = default!;
    private readonly bool _active;

    internal ForeachChain(TOuter outer, Leading leading, ForEachStatementSyntax header, bool active) : base(new())
    {
        _outer = outer;
        _leading = leading;
        _header = header;
        _active = active;
    }

    public TOuter EndForeach()
    {
        var statement = _header.WithStatement(BuildBlock());
        if (_active) _outer.AddStatement(_leading.Apply(statement));
        return _outer;
    }
}

internal sealed class FixedChain<TOuter> : StatementChain<FixedChain<TOuter>> where TOuter : class, IStatementHost
{
    private readonly TOuter _outer = default!;
    private readonly Leading _leading = default!;
    private readonly FixedStatementSyntax _header = default!;
    private readonly bool _active;

    internal FixedChain(TOuter outer, Leading leading, FixedStatementSyntax header, bool active) : base(new())
    {
        _outer = outer;
        _leading = leading;
        _header = header;
        _active = active;
    }

    public TOuter EndFixed()
    {
        var statement = _header.WithStatement(BuildBlock());
        if (_active) _outer.AddStatement(_leading.Apply(statement));
        return _outer;
    }
}
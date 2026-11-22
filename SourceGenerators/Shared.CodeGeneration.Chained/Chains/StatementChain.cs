using System;
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
    public TSelf Continue(bool when = true) => when ? Add(ContinueStatement()) : DropPrefixes();

    public TSelf YieldReturn(Expr value, bool when = true)
    {
        return when && value.Node is { } node
            ? Add(YieldStatement(SyntaxKind.YieldReturnStatement, node))
            : DropPrefixes();
    }

    public TSelf YieldBreak(bool when = true)
    {
        return when ? Add(YieldStatement(SyntaxKind.YieldBreakStatement)) : DropPrefixes();
    }

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

    public ForChain<TSelf> For(string name, Expr start, Expr condition, Expr step, bool when = true)
    {
        var declaration = start.Node is { } value ? BuildVariable(name, value) : null;
        var steps = step.Node is { } node ? SingletonSeparatedList(node) : default;
        var header = ForStatement(declaration, default, condition.Node, steps, Block());
        return new(Self, Open(when).Leading, header, when);
    }

    public WhileChain<TSelf> While(Expr condition, bool when = true)
    {
        var active = when && condition.Node is not null;
        var header = WhileStatement(condition.Node ?? IdentifierName("_"), Block());
        return new(Self, Open(active).Leading, header, active);
    }

    public DoWhileChain<TSelf> DoWhile(bool when = true) => new(Self, Open(when).Leading, when);

    public UsingChain<TSelf> Using(string name, Expr value, bool when = true)
    {
        var active = when && value.Node is not null;
        var header = UsingStatement(BuildVariable(name, value.Node ?? IdentifierName("_")), null, Block());
        return new(Self, Open(active).Leading, header, active);
    }

    public LockChain<TSelf> Lock(Expr value, bool when = true)
    {
        var active = when && value.Node is not null;
        return new(Self, Open(active).Leading, LockStatement(value.Node ?? IdentifierName("_"), Block()), active);
    }

    public UnsafeChain<TSelf> Unsafe(bool when = true) => new(Self, Open(when).Leading, when);
    public TryChain<TSelf> Try(bool when = true) => new(Self, Open(when).Leading, when);

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

    void IStatementHost.RestoreLeading(Leading leading) => Lead.Restore(leading);

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

    private static VariableDeclarationSyntax BuildVariable(string name, ExpressionSyntax value)
    {
        var declarator = VariableDeclarator(Names.CreateIdentifier(name)).WithInitializer(EqualsValueClause(value));
        return VariableDeclaration(IdentifierName("var"), SingletonSeparatedList(declarator));
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
        return _if.Finish(_active ? BuildStatement(_braced) : null);
    }
}

internal abstract class BlockChain<TSelf, TOuter> : StatementChain<TSelf>
    where TSelf : BlockChain<TSelf, TOuter>
    where TOuter : class, IStatementHost
{
    private readonly TOuter _outer = default!;
    private readonly Leading _leading = default!;
    private readonly bool _active;

    private protected BlockChain(TOuter outer, Leading leading, bool active) : base(new())
    {
        _outer = outer;
        _leading = leading;
        _active = active;
    }

    private protected TOuter Close(Func<BlockSyntax, StatementSyntax> build, bool active = true)
    {
        var statement = build(BuildBlock());

        if (_active && active)
        {
            _outer.AddStatement(_leading.Apply(statement));
        }
        else
        {
            _outer.RestoreLeading(_leading);
        }

        return _outer;
    }
}

internal sealed class ForeachChain<TOuter> : BlockChain<ForeachChain<TOuter>, TOuter>
    where TOuter : class, IStatementHost
{
    private readonly ForEachStatementSyntax _header = default!;

    internal ForeachChain(TOuter outer, Leading leading, ForEachStatementSyntax header, bool active)
        : base(outer, leading, active) => _header = header;

    public TOuter EndForeach() => Close(_header.WithStatement);
}

internal sealed class FixedChain<TOuter> : BlockChain<FixedChain<TOuter>, TOuter> where TOuter : class, IStatementHost
{
    private readonly FixedStatementSyntax _header = default!;

    internal FixedChain(TOuter outer, Leading leading, FixedStatementSyntax header, bool active)
        : base(outer, leading, active) => _header = header;

    public TOuter EndFixed() => Close(_header.WithStatement);
}

internal sealed class ForChain<TOuter> : BlockChain<ForChain<TOuter>, TOuter> where TOuter : class, IStatementHost
{
    private readonly ForStatementSyntax _header = default!;

    internal ForChain(TOuter outer, Leading leading, ForStatementSyntax header, bool active)
        : base(outer, leading, active) => _header = header;

    public TOuter EndFor() => Close(_header.WithStatement);
}

internal sealed class WhileChain<TOuter> : BlockChain<WhileChain<TOuter>, TOuter> where TOuter : class, IStatementHost
{
    private readonly WhileStatementSyntax _header = default!;

    internal WhileChain(TOuter outer, Leading leading, WhileStatementSyntax header, bool active)
        : base(outer, leading, active) => _header = header;

    public TOuter EndWhile() => Close(_header.WithStatement);
}

internal sealed class DoWhileChain<TOuter> : BlockChain<DoWhileChain<TOuter>, TOuter>
    where TOuter : class, IStatementHost
{
    internal DoWhileChain(TOuter outer, Leading leading, bool active) : base(outer, leading, active)
    {
    }

    public TOuter EndDoWhile(Expr condition)
    {
        var node = condition.Node ?? IdentifierName("_");
        return Close(block => DoStatement(block, node), condition.Node is not null);
    }
}

internal sealed class UsingChain<TOuter> : BlockChain<UsingChain<TOuter>, TOuter> where TOuter : class, IStatementHost
{
    private readonly UsingStatementSyntax _header = default!;

    internal UsingChain(TOuter outer, Leading leading, UsingStatementSyntax header, bool active)
        : base(outer, leading, active) => _header = header;

    public TOuter EndUsing() => Close(_header.WithStatement);
}

internal sealed class LockChain<TOuter> : BlockChain<LockChain<TOuter>, TOuter> where TOuter : class, IStatementHost
{
    private readonly LockStatementSyntax _header = default!;

    internal LockChain(TOuter outer, Leading leading, LockStatementSyntax header, bool active)
        : base(outer, leading, active) => _header = header;

    public TOuter EndLock() => Close(_header.WithStatement);
}

internal sealed class UnsafeChain<TOuter> : BlockChain<UnsafeChain<TOuter>, TOuter> where TOuter : class, IStatementHost
{
    internal UnsafeChain(TOuter outer, Leading leading, bool active) : base(outer, leading, active)
    {
    }

    public TOuter EndUnsafe() => Close(static block => UnsafeStatement(block));
}

internal sealed class TryChain<TOuter> : StatementChain<TryChain<TOuter>> where TOuter : class, IStatementHost
{
    private readonly List<CatchClauseSyntax> _catches = [];
    private readonly TOuter _outer = default!;
    private readonly Leading _leading = default!;
    private readonly bool _active;
    private BlockSyntax? _block = default;
    private CatchClauseSyntax? _catch = default;

    internal TryChain(TOuter outer, Leading leading, bool active) : base(new())
    {
        _outer = outer;
        _leading = leading;
        _active = active;
    }

    public TryChain<TOuter> Catch(TypeRef type, string? name = null, bool when = true)
    {
        CloseSection();

        var declaration = CatchDeclaration(type.Node ?? IdentifierName("_"));
        if (name != default) declaration = declaration.WithIdentifier(Names.CreateIdentifier(name));

        _catch = when && type.Node is not null ? CatchClause().WithDeclaration(declaration) : null;
        return this;
    }

    public TryChain<TOuter> Catch(bool when = true)
    {
        CloseSection();
        _catch = when ? CatchClause() : null;
        return this;
    }

    public FinallyChain<TOuter> Finally(bool when = true)
    {
        CloseSection();
        return new(this, when);
    }

    public TOuter EndTry()
    {
        CloseSection();
        return Finish(null);
    }

    internal TOuter Finish(FinallyClauseSyntax? @finally)
    {
        if (_active) _outer.AddStatement(_leading.Apply(TryStatement(_block!, List(_catches), @finally)));
        return _outer;
    }

    private void CloseSection()
    {
        var block = BuildBlock();

        if (_block == default)
        {
            _block = block;
            return;
        }

        if (_catch != default) _catches.Add(_catch.WithBlock(block));
        _catch = default;
    }
}

internal sealed class FinallyChain<TOuter> : StatementChain<FinallyChain<TOuter>> where TOuter : class, IStatementHost
{
    private readonly TryChain<TOuter> _try = default!;
    private readonly bool _active;

    internal FinallyChain(TryChain<TOuter> @try, bool active) : base(new())
    {
        _try = @try;
        _active = active;
    }

    public TOuter EndTry() => _try.Finish(_active ? FinallyClause(BuildBlock()) : null);
}
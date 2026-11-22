using System;
using System.Collections.Generic;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Shared.CodeGeneration.Scoped.Extensions;
using Shared.CodeGeneration.Scoped.Rendering;
using static Microsoft.CodeAnalysis.CSharp.SyntaxFactory;

namespace Shared.CodeGeneration.Scoped;

internal abstract class Statements : Scope<BodyScope>, IStatementExtensionHost
{
    private protected Statements(Lead lead, List<StatementSyntax> sink) : base(lead) => Sink = sink;

    private protected List<StatementSyntax> Sink { get; }

    public BodyScope Ref(bool when = true) => AddModifier(SyntaxKind.RefKeyword, when);
    public BodyScope Readonly(bool when = true) => AddModifier(SyntaxKind.ReadOnlyKeyword, when);

    public BodyScope Var(string name, Expr? value = null, bool when = true)
    {
        if (when is false) return Skip();

        var (modifiers, type) = Declarations.SplitRef(Lead.TakeModifiers(), IdentifierName("var"));
        var declarator = VariableDeclarator(Names.CreateIdentifier(name));
        if (value?.Node is { } node) declarator = declarator.WithInitializer(EqualsValueClause(node));
        return Add(LocalDeclarationStatement(VariableDeclaration(type, SingletonSeparatedList(declarator)))
            .WithModifiers(modifiers));
    }

    public BodyScope Do(Expr expression, bool when = true)
    {
        return when && expression.Node is { } node ? Add(ExpressionStatement(node)) : Skip();
    }

    public IfScope If(Expr condition, Action<BodyScope> then, bool braced = true, bool when = true)
    {
        if (when is false || condition.Node is null)
        {
            Skip();
            return new(Next, Lead, Sink, -1);
        }

        Add(IfStatement(condition.Node, BodyScope.BuildStatement(then, braced)));
        return new(Next, Lead, Sink, Sink.Count - 1);
    }

    public BodyScope Foreach(string name, Expr collection, Action<BodyScope> body, bool when = true)
    {
        if (when is false || collection.Node is null) return Skip();
        return Add(ForEachStatement(IdentifierName("var"), Names.CreateIdentifier(name), collection.Node,
            BodyScope.BuildBlock(body)));
    }

    public BodyScope For(string name, Expr start, Expr condition, Expr step, Action<BodyScope> body,
        bool when = true)
    {
        if (when is false) return Skip();

        var declaration = start.Node is { } value ? BuildVariable(name, value) : null;
        var steps = step.Node is { } node ? SingletonSeparatedList(node) : default;
        return Add(ForStatement(declaration, default, condition.Node, steps, BodyScope.BuildBlock(body)));
    }

    public BodyScope While(Expr condition, Action<BodyScope> body, bool when = true)
    {
        if (when is false || condition.Node is null) return Skip();
        return Add(WhileStatement(condition.Node, BodyScope.BuildBlock(body)));
    }

    public BodyScope DoWhile(Action<BodyScope> body, Expr condition, bool when = true)
    {
        if (when is false || condition.Node is null) return Skip();
        return Add(DoStatement(BodyScope.BuildBlock(body), condition.Node));
    }

    public BodyScope Using(string name, Expr value, Action<BodyScope> body, bool when = true)
    {
        if (when is false || value.Node is null) return Skip();
        return Add(UsingStatement(BuildVariable(name, value.Node), null, BodyScope.BuildBlock(body)));
    }

    public BodyScope Lock(Expr value, Action<BodyScope> body, bool when = true)
    {
        if (when is false || value.Node is null) return Skip();
        return Add(LockStatement(value.Node, BodyScope.BuildBlock(body)));
    }

    public BodyScope Unsafe(Action<BodyScope> body, bool when = true)
    {
        return when ? Add(UnsafeStatement(BodyScope.BuildBlock(body))) : Skip();
    }

    public TryScope Try(Action<BodyScope> body, bool when = true)
    {
        if (when is false)
        {
            Skip();
            return new(Next, Lead, Sink, -1);
        }

        Add(TryStatement().WithBlock(BodyScope.BuildBlock(body)));
        return new(Next, Lead, Sink, Sink.Count - 1);
    }

    public BodyScope Switch(Expr value, Action<SwitchScope> sections, bool when = true)
    {
        if (when is false || value.Node is null) return Skip();

        var scope = new SwitchScope();
        sections(scope);
        return Add(scope.Complete(SwitchStatement(value.Node)));
    }

    public BodyScope Break(bool when = true) => when ? Add(BreakStatement()) : Skip();
    public BodyScope Continue(bool when = true) => when ? Add(ContinueStatement()) : Skip();
    public BodyScope Return(Expr? value = null, bool when = true) => when ? Add(ReturnStatement(value?.Node)) : Skip();

    public BodyScope YieldReturn(Expr value, bool when = true)
    {
        if (when is false || value.Node is null) return Skip();
        return Add(YieldStatement(SyntaxKind.YieldReturnStatement, value.Node));
    }

    public BodyScope YieldBreak(bool when = true)
    {
        return when ? Add(YieldStatement(SyntaxKind.YieldBreakStatement)) : Skip();
    }

    public BodyScope Throw(Expr exception, bool when = true)
    {
        return when && exception.Node is { } node ? Add(ThrowStatement(node)) : Skip();
    }

    public BodyScope Fixed(TypeRef type, string name, Expr value, Action<BodyScope> body, bool when = true)
    {
        if (when is false || type.Node is null || value.Node is null) return Skip();

        var declarator = VariableDeclarator(Names.CreateIdentifier(name))
            .WithInitializer(EqualsValueClause(value.Node));
        return Add(FixedStatement(VariableDeclaration(type.Node, SingletonSeparatedList(declarator)),
            BodyScope.BuildBlock(body)));
    }

    BodyScope IStatementExtensionHost.AddExtension(StatementSyntax statement, bool when)
    {
        return when ? Add(statement) : Skip();
    }

    private protected BodyScope Add(StatementSyntax statement)
    {
        Sink.Add(Lead.Attach(statement));
        return Skip();
    }

    private static VariableDeclarationSyntax BuildVariable(string name, ExpressionSyntax value)
    {
        var declarator = VariableDeclarator(Names.CreateIdentifier(name)).WithInitializer(EqualsValueClause(value));
        return VariableDeclaration(IdentifierName("var"), SingletonSeparatedList(declarator));
    }

    private BodyScope AddModifier(SyntaxKind keyword, bool when)
    {
        if (when) Lead.AddModifier(keyword);
        return Next;
    }

    private BodyScope Skip()
    {
        Lead.DropPrefixes();
        return Next;
    }
}

internal sealed class BodyScope : Statements
{
    internal BodyScope(Lead lead, List<StatementSyntax> sink) : base(lead, sink)
    {
    }

    private protected override BodyScope Next => this;

    internal static BlockSyntax BuildBlock(Action<BodyScope> body) => Build(body).Block;

    internal static StatementSyntax BuildStatement(Action<BodyScope> body, bool braced)
    {
        var (block, closed) = Build(body);
        var keepsBraces = braced || closed || block.Statements.Count != 1 || block.Statements[0] is IfStatementSyntax;
        return keepsBraces ? block : block.Statements[0];
    }

    internal static List<StatementSyntax> BuildSection(Lead lead, Action<BodyScope> body)
    {
        var sink = new List<StatementSyntax>();
        body(new(lead, sink));
        return sink;
    }

    private static (BlockSyntax Block, bool Closed) Build(Action<BodyScope> body)
    {
        var sink = new List<StatementSyntax>();
        var lead = new Lead();
        body(new(lead, sink));

        var closed = lead.HasPending;
        return (SyntaxFactory.Block(sink).WithCloseBraceToken(lead.Close(Token(SyntaxKind.CloseBraceToken))), closed);
    }
}

internal sealed class IfScope : Statements
{
    private readonly BodyScope _parent = default!;
    private readonly int _index;

    internal IfScope(BodyScope parent, Lead lead, List<StatementSyntax> sink, int index) : base(lead, sink)
    {
        _parent = parent;
        _index = index;
    }

    private protected override BodyScope Next => _parent;

    public IfScope ElseIf(Expr condition, Action<BodyScope> then, bool braced = true, bool when = true)
    {
        if (_index < 0 || when is false || condition.Node is null) return this;

        Append(IfStatement(condition.Node, BodyScope.BuildStatement(then, braced)));
        return this;
    }

    public BodyScope Else(Action<BodyScope> body, bool braced = true, bool when = true)
    {
        if (_index >= 0 && when) Append(BodyScope.BuildStatement(body, braced));
        return _parent;
    }

    private void Append(StatementSyntax statement)
    {
        Sink[_index] = AppendElse((IfStatementSyntax)Sink[_index], ElseClause(statement));
    }

    private static IfStatementSyntax AppendElse(IfStatementSyntax statement, ElseClauseSyntax clause)
    {
        return statement.Else switch
        {
            null => statement.WithElse(clause),
            { Statement: IfStatementSyntax inner } => statement.WithElse(
                statement.Else.WithStatement(AppendElse(inner, clause))),
            _ => statement
        };
    }
}

internal sealed class TryScope : Statements
{
    private readonly BodyScope _parent = default!;
    private readonly int _index;

    internal TryScope(BodyScope parent, Lead lead, List<StatementSyntax> sink, int index) : base(lead, sink)
    {
        _parent = parent;
        _index = index;
    }

    private protected override BodyScope Next => _parent;
    private TryStatementSyntax Statement => (TryStatementSyntax)Sink[_index];

    public TryScope Catch(TypeRef type, string? name, Action<BodyScope> body, bool when = true)
    {
        if (_index < 0 || when is false || type.Node is null) return this;

        var declaration = CatchDeclaration(type.Node);
        if (name != default) declaration = declaration.WithIdentifier(Names.CreateIdentifier(name));

        AddCatch(CatchClause().WithDeclaration(declaration).WithBlock(BodyScope.BuildBlock(body)));
        return this;
    }

    public TryScope Catch(Action<BodyScope> body, bool when = true)
    {
        if (_index >= 0 && when) AddCatch(CatchClause().WithBlock(BodyScope.BuildBlock(body)));
        return this;
    }

    public BodyScope Finally(Action<BodyScope> body, bool when = true)
    {
        if (_index >= 0 && when) Sink[_index] = Statement.WithFinally(FinallyClause(BodyScope.BuildBlock(body)));
        return _parent;
    }

    private void AddCatch(CatchClauseSyntax clause) => Sink[_index] = Statement.AddCatches(clause);
}
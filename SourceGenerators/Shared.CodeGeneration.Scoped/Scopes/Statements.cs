using System;
using System.Collections.Generic;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Shared.CodeGeneration.Scoped.Rendering;
using static Microsoft.CodeAnalysis.CSharp.SyntaxFactory;

namespace Shared.CodeGeneration.Scoped;

internal abstract class Statements : Scope<BodyScope>
{
    private protected Statements(Lead lead, List<StatementSyntax> sink) : base(lead) => Sink = sink;

    private protected List<StatementSyntax> Sink { get; }

    public BodyScope Ref(bool when = true) => Modifier(SyntaxKind.RefKeyword, when);
    public BodyScope Readonly(bool when = true) => Modifier(SyntaxKind.ReadOnlyKeyword, when);

    public BodyScope Var(string name, Expr? value = null, bool when = true)
    {
        if (when is false) return Skip();

        var (modifiers, type) = Declarations.SplitRef(Lead.TakeModifiers(), IdentifierName("var"));
        var declarator = VariableDeclarator(Names.Identifier(name));
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

        Add(IfStatement(condition.Node, BodyScope.Statement(then, braced)));
        return new(Next, Lead, Sink, Sink.Count - 1);
    }

    public BodyScope Foreach(string name, Expr collection, Action<BodyScope> body, bool when = true)
    {
        if (when is false || collection.Node is null) return Skip();
        return Add(ForEachStatement(IdentifierName("var"), Names.Identifier(name), collection.Node,
            BodyScope.Block(body)));
    }

    public BodyScope Switch(Expr value, Action<SwitchScope> sections, bool when = true)
    {
        if (when is false || value.Node is null) return Skip();

        var scope = new SwitchScope();
        sections(scope);
        return Add(scope.Complete(SwitchStatement(value.Node)));
    }

    public BodyScope Break(bool when = true) => when ? Add(BreakStatement()) : Skip();
    public BodyScope Return(Expr? value = null, bool when = true) => when ? Add(ReturnStatement(value?.Node)) : Skip();

    public BodyScope Throw(Expr exception, bool when = true)
    {
        return when && exception.Node is { } node ? Add(ThrowStatement(node)) : Skip();
    }

    public BodyScope Fixed(TypeRef type, string name, Expr value, Action<BodyScope> body, bool when = true)
    {
        if (when is false || type.Node is null || value.Node is null) return Skip();

        var declarator = VariableDeclarator(Names.Identifier(name)).WithInitializer(EqualsValueClause(value.Node));
        return Add(FixedStatement(VariableDeclaration(type.Node, SingletonSeparatedList(declarator)),
            BodyScope.Block(body)));
    }

    private protected BodyScope Add(StatementSyntax statement)
    {
        Sink.Add(Lead.Attach(statement));
        return Next;
    }

    private BodyScope Modifier(SyntaxKind keyword, bool when)
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

    internal static BlockSyntax Block(Action<BodyScope> body) => Build(body).Block;

    internal static StatementSyntax Statement(Action<BodyScope> body, bool braced)
    {
        var (block, closed) = Build(body);
        return braced || block.Statements.Count != 1 || closed ? block : block.Statements[0];
    }

    internal static List<StatementSyntax> Section(Lead lead, Action<BodyScope> body)
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
    private readonly BodyScope _parent;
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

        Append(IfStatement(condition.Node, BodyScope.Statement(then, braced)));
        return this;
    }

    public BodyScope Else(Action<BodyScope> body, bool braced = true, bool when = true)
    {
        if (_index >= 0 && when) Append(BodyScope.Statement(body, braced));
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
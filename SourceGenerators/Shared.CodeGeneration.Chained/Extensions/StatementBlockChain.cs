using System;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Shared.CodeGeneration.Chained.Rendering;

namespace Shared.CodeGeneration.Chained.Extensions;

internal sealed class StatementBlockChain<TOuter> : BlockChain<StatementBlockChain<TOuter>, TOuter>
    where TOuter : class, IStatementHost
{
    private readonly Func<BlockSyntax, StatementSyntax> _wrap = default!;

    internal StatementBlockChain(TOuter outer, Leading leading, Func<BlockSyntax, StatementSyntax> wrap, bool active)
        : base(outer, leading, active)
    {
        _wrap = wrap;
    }

    public TOuter EndStatement() => Close(_wrap);
}

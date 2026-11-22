using System.Collections.Generic;
using System.Linq;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Shared.CodeGeneration.Chained.Rendering;
using static Microsoft.CodeAnalysis.CSharp.SyntaxFactory;

namespace Shared.CodeGeneration.Chained;

internal sealed class Lambda
{
    private readonly IReadOnlyList<string> _parameters = default!;
    private readonly bool _isStatic;
    private readonly bool _when;

    internal Lambda(IReadOnlyList<string>? parameters, bool isStatic, bool when)
    {
        _parameters = parameters ?? [];
        _isStatic = isStatic;
        _when = when;
    }

    public Expr Arrow(Expr value, bool when = true)
    {
        if (_when is false || when is false || value.Node is null) return Expr.Empty;
        return new(BuildHead().WithExpressionBody(value.Node));
    }

    public LambdaBodyChain Body(bool when = true) => new(this, _when && when);

    internal ParenthesizedLambdaExpressionSyntax BuildHead()
    {
        var lambda = ParenthesizedLambdaExpression()
            .WithParameterList(ParameterList(
                SeparatedList(_parameters.Select(static name => Parameter(Names.CreateIdentifier(name))))));
        return _isStatic ? lambda.WithModifiers(TokenList(Token(SyntaxKind.StaticKeyword))) : lambda;
    }
}

internal sealed class LambdaBodyChain : StatementChain<LambdaBodyChain>
{
    private readonly Lambda _lambda = default!;
    private readonly bool _active;

    internal LambdaBodyChain(Lambda lambda, bool active) : base(new())
    {
        _lambda = lambda;
        _active = active;
    }

    public Expr EndLambda()
    {
        return _active ? new(_lambda.BuildHead().WithBlock(BuildBlock())) : Expr.Empty;
    }
}
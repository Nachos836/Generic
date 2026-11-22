using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Shared.CodeGeneration.Scoped.Rendering;
using static Microsoft.CodeAnalysis.CSharp.SyntaxFactory;

namespace Shared.CodeGeneration.Scoped;

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

    public Expr Body(Action<BodyScope> body, bool when = true)
    {
        if (_when is false || when is false) return Expr.Empty;
        return new(BuildHead().WithBlock(BodyScope.BuildBlock(body)));
    }

    private ParenthesizedLambdaExpressionSyntax BuildHead()
    {
        var lambda = ParenthesizedLambdaExpression()
            .WithParameterList(
                ParameterList(
                    SeparatedList(_parameters.Select(static name => Parameter(Names.CreateIdentifier(name))))));
        return _isStatic ? lambda.WithModifiers(TokenList(Token(SyntaxKind.StaticKeyword))) : lambda;
    }
}
using System.Linq;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace CustomAttributesEditorGenerator;

using Components;

[Generator(LanguageNames.CSharp)]
public sealed class ButtonDrawerGenerator : IIncrementalGenerator
{
    void IIncrementalGenerator.Initialize(IncrementalGeneratorInitializationContext context)
    {
        var candidateMethods = context.SyntaxProvider.CreateSyntaxProvider
        (
            predicate: static (node, token) =>
            {
                if (token.IsCancellationRequested) return false;

                return node is MethodDeclarationSyntax { AttributeLists.Count: > 0 };
            },
            transform: static (context, token) => context.SemanticModel.GetDeclaredSymbol((MethodDeclarationSyntax) context.Node, token)

        ).Where(static symbol => symbol != null)
            .WithTrackingName("CandidateMethods");

        var attributeTypeProvider = context.CompilationProvider
            .Select(static (compilation, _) => compilation.GetTypeByMetadataName(CodeGenerator.ButtonAttributeFullName));
        var methodsWithButton = candidateMethods
            .Combine(attributeTypeProvider)
            .Where(static income =>
            {
                var method = income.Left;
                var attributeSymbol = income.Right;
                if (method is null || attributeSymbol is null) return false;

                return method.GetAttributes()
                    .Any(data =>
                    {
                        var candidate = data.AttributeClass!;
                        var left = candidate.ContainingNamespace;
                        var right = attributeSymbol.ContainingNamespace;
                        if (SymbolEqualityComparer.Default.Equals(left, right))
                        {
                            return candidate.Name == attributeSymbol.Name;
                        }

                        return false;
                    });
            })
            .Select(static (pair, token) => token.IsCancellationRequested ? null : pair.Left)
            .WithTrackingName("MethodsWithButtonAttribute");

        var grouped = methodsWithButton
            .Where(static symbol => symbol is not null)
            .Collect()
            .SelectMany(static (methods, token) =>
            {
                if (token.IsCancellationRequested) return [];

                return methods.Cast<IMethodSymbol>()
                    .GroupBy(static symbol => symbol.ContainingType, SymbolEqualityComparer.Default);
            })
            .WithTrackingName("GroupMethodsByOwner");

        context.RegisterSourceOutput(grouped, CodeGenerator.GenerateDrawer);
    }
}

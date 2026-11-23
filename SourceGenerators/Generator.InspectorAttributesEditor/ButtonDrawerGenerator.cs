using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Shared.Generator;

namespace Generator.InspectorAttributesEditor;

using Components;

[Generator(LanguageNames.CSharp)]
public sealed class ButtonDrawerGenerator : IIncrementalGenerator
{
    private readonly SessionMemory _memory = new();

    void IIncrementalGenerator.Initialize(IncrementalGeneratorInitializationContext context)
    {
        var analyzedMethods = context.SyntaxProvider.ForAttributeWithMetadataName(
            fullyQualifiedMetadataName: CodeGenerator.ButtonAttributeFullName,
            predicate: static (node, _) => node is MethodDeclarationSyntax,
            transform: static (attributeContext, cancellationToken) => ButtonAnalysis.Analyze(attributeContext, cancellationToken))
                .WithTrackingName("AnalyzeButtonMethods");

        var diagnostics = analyzedMethods.Where(static result => result.Diagnostic is not null)
            .Select(static (result, _) => result.Diagnostic!);

        context.RegisterSourceOutput(diagnostics, static (productionContext, diagnostic) => productionContext.ReportDiagnostic(diagnostic));

        var validMethods = analyzedMethods.Where(static result => result.Model is not null)
            .Select(static (result, _) => result.Model!)
            .WithComparer(EqualityComparer<DrawerModel>.Default)
            .WithTrackingName("ButtonModels");

        var owners = validMethods.Collect()
            .SelectMany(static (models, cancellationToken) =>
            {
                var result = ImmutableArray.CreateBuilder<DrawerModel>();
                var grouping = models.GroupBy(static model => model.Identity, StringComparer.Ordinal)
                    .OrderBy(static group => group.Key, StringComparer.Ordinal);

                foreach (var group in grouping)
                {
                    cancellationToken.ThrowIfCancellationRequested();

                    var owner = group.First();

                    var buttons = group.SelectMany(static model => model.Buttons)
                        .OrderBy(static button => button.Name, StringComparer.Ordinal)
                        .ToImmutableArray();

                    result.Add(new DrawerModel
                    {
                        Identity = owner.Identity,
                        NamespaceName = owner.NamespaceName,
                        OwnerName = owner.OwnerName,
                        RequiresCustomUI = owner.RequiresCustomUI,
                        Buttons = buttons
                    });
                }

                return result.ToImmutable();
            })
            .WithComparer(EqualityComparer<DrawerModel>.Default)
            .WithTrackingName("DrawerModels");

        var sessions = owners.Collect().Combine(context.CompilationProvider).Combine(context.ParseOptionsProvider);

        context.RegisterSourceOutput(sessions, (productionContext, input) => CodeGenerator.GenerateDrawers(
            productionContext, input.Left.Left, input.Left.Right, input.Right, _memory));
    }
}

using System.Collections.Generic;
using System.Linq;
using System.Threading;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace Generator.InspectorAttributesEditor.Components;

internal sealed class ButtonAnalysis
{
    public required DrawerModel? Model { get; init; }
    public required Diagnostic? Diagnostic { get; init; }

    public static ButtonAnalysis Analyze(GeneratorAttributeSyntaxContext context, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();

        var method = (IMethodSymbol)context.TargetSymbol;
        var owner = method.ContainingType;
        var location = context.TargetNode.GetLocation();

        if (context.Attributes.Length != 1) return InvalidButton("exactly one ButtonAttribute is required.");

        if (method.MethodKind != MethodKind.Ordinary
        || method.ExplicitInterfaceImplementations.IsEmpty is false
        || method.IsStatic
        || method.IsGenericMethod
        || method.Parameters.Length != 0
        || method.ReturnsVoid is false
        || method.IsAsync
        || method.IsAbstract
        || method.IsExtern
        || method.IsPartialDefinition
        || method.PartialDefinitionPart is not null
        || method.PartialImplementationPart is not null)
            return InvalidButton("expected a non-partial, non-async instance method " +
                                 "without parameters and with a void return type.");

        if (owner.TypeKind != TypeKind.Class
        || owner.IsRecord
        || owner.IsStatic
        || owner.IsGenericType
        || owner.ContainingType is not null
        || owner.IsFileLocal)
            return InvalidOwner("expected a non-generic, non-file-local top-level class.");

        var declarations = owner.DeclaringSyntaxReferences
            .Select(reference => reference.GetSyntax(cancellationToken))
            .OfType<ClassDeclarationSyntax>()
            .ToArray();

        if (declarations.Length == 0
        || declarations.Any(static declaration => declaration.Modifiers.Any(SyntaxKind.PartialKeyword) is false))
            return new ButtonAnalysis
            {
                Model = null,
                Diagnostic = Diagnostic.Create
                (
                    descriptor: Shared.Generator.Diagnostics.MissingPartialDescriptor,
                    location: location,
                    messageArgs: owner.Name
                )
            };

        var compilation = context.SemanticModel.Compilation;
        var monoBehaviour = compilation.GetTypeByMetadataName
        (
            $"{nameof(UnityEngine)}." +
            $"{nameof(UnityEngine.MonoBehaviour)}"
        );

        var isUnityObject = false;
        for (var current = owner; current is not null; current = current.BaseType)
        {
            cancellationToken.ThrowIfCancellationRequested();

            if (SymbolEqualityComparer.IncludeNullability.Equals(current, monoBehaviour))
            {
                isUnityObject = true;
                break;
            }
        }

        if (isUnityObject is false) return InvalidOwner($"the class must derive from: {monoBehaviour!.MetadataName}.");

        var customUIAttribute = compilation.GetTypeByMetadataName
        (
            $"{nameof(InspectorAttributes)}." +
            $"{nameof(InspectorAttributes.ApplyCustomUIProcessingAttribute)}"
        );

        var requiresCustomUI = customUIAttribute is not null && owner.GetAttributes()
            .Any(attribute => SymbolEqualityComparer.Default.Equals(attribute.AttributeClass, customUIAttribute));

        // Use the attribute already matched by ForAttributeWithMetadataName.
        var buttonAttribute = context.Attributes[0];
        var arguments = buttonAttribute.ConstructorArguments;

        if (arguments.Length != 1 || arguments[0].Kind == TypedConstantKind.Error)
            return InvalidButton("the attribute must have one valid string constructor argument.");

        var label = arguments[0].Value as string ?? method.Name;
        var namespaceName = GetNamespaceName(owner.ContainingNamespace);

        var identity = namespaceName.Length == 0
            ? owner.MetadataName
            : namespaceName + "." + owner.MetadataName;

        return new ButtonAnalysis
        {
            Model = new DrawerModel
            {
                Identity = identity,
                NamespaceName = namespaceName,
                OwnerName = EscapeIdentifier(owner.Name),
                RequiresCustomUI = requiresCustomUI,
                Buttons =
                [
                    new ButtonModel
                    {
                        Name = EscapeIdentifier(method.Name),
                        Label = label
                    }
                ]
            },
            Diagnostic = null
        };

        ButtonAnalysis InvalidButton(string reason) => new()
        {
            Model = null,
            Diagnostic = Diagnostic.Create
            (
                descriptor: Diagnostics.InvalidButtonDescriptor,
                location: location,
                messageArgs: [method.Name, reason]
            )
        };

        ButtonAnalysis InvalidOwner(string reason) => new()
        {
            Model = null,
            Diagnostic = Diagnostic.Create
            (
                descriptor: Diagnostics.UnsupportedOwnerDescriptor,
                location: location,
                messageArgs: [owner.Name, reason]
            )
        };
    }

    private static string GetNamespaceName(INamespaceSymbol symbol)
    {
        var segments = new Stack<string>();

        for (var current = symbol; current.IsGlobalNamespace is false; current = current.ContainingNamespace)
        {
            segments.Push(EscapeIdentifier(current.Name));
        }

        return string.Join(".", segments);
    }

    private static string EscapeIdentifier(string name)
    {
        return SyntaxFacts.GetKeywordKind(name) != SyntaxKind.None
            || SyntaxFacts.GetContextualKeywordKind(name) != SyntaxKind.None
                ? "@" + name
                : name;
    }
}

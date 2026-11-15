using Microsoft.CodeAnalysis;

namespace CustomAttributesEditorGenerator.Components;

internal static class Diagnostics
{
    internal static DiagnosticDescriptor MissingPartialDescriptor { get; } = new
    (
        id: "UIGEN001",
        title: "Missing partial modifier",
        messageFormat: "Type '{0}' must be partial to generate inspector buttons.",
        category: "Usage",
        defaultSeverity: DiagnosticSeverity.Error,
        isEnabledByDefault: true
    );

    internal static DiagnosticDescriptor UnsupportedOwnerDescriptor { get; } = new
    (
        id: "UIGEN002",
        title: "Unsupported inspector owner",
        messageFormat: "Cannot generate inspector for '{0}': {1}",
        category: "Usage",
        defaultSeverity: DiagnosticSeverity.Error,
        isEnabledByDefault: true
    );

    internal static DiagnosticDescriptor InvalidButtonDescriptor { get; } = new
    (
        id: "UIGEN003",
        title: "Invalid inspector button",
        messageFormat: "Cannot generate button for '{0}': {1}",
        category: "Usage",
        defaultSeverity: DiagnosticSeverity.Error,
        isEnabledByDefault: true
    );
}

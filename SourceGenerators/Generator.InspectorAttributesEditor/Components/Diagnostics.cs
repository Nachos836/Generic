using Microsoft.CodeAnalysis;

namespace Generator.InspectorAttributesEditor.Components;

internal static class Diagnostics
{
    internal static DiagnosticDescriptor UnsupportedOwnerDescriptor { get; } = new
    (
        id: "UIGEN001",
        title: "Unsupported inspector owner",
        messageFormat: "Cannot generate inspector for '{0}': {1}",
        category: "Usage",
        defaultSeverity: DiagnosticSeverity.Error,
        isEnabledByDefault: true
    );

    internal static DiagnosticDescriptor InvalidButtonDescriptor { get; } = new
    (
        id: "UIGEN002",
        title: "Invalid inspector button",
        messageFormat: "Cannot generate button for '{0}': {1}",
        category: "Usage",
        defaultSeverity: DiagnosticSeverity.Error,
        isEnabledByDefault: true
    );
}

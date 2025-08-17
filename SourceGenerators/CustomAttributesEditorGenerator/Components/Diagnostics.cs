using Microsoft.CodeAnalysis;

namespace CustomAttributesEditorGenerator.Components;

internal static class Diagnostics
{
    internal static DiagnosticDescriptor MissingPartialDescriptor { get; } = new
    (
        id: "CA1812",
        title: "Missing partial modifier",
        messageFormat: "Type '{0}' is missing `partial` type modifier!",
        category: "Usage",
        defaultSeverity: DiagnosticSeverity.Error,
        isEnabledByDefault: true
    );
}

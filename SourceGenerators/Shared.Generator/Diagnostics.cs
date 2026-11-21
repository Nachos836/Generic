using Microsoft.CodeAnalysis;

namespace Shared.Generator;

internal static class Diagnostics
{
    internal static DiagnosticDescriptor MissingPartialDescriptor { get; } = new
    (
        id: "UCGEN001",
        title: "Missing partial modifier",
        messageFormat: "Type '{0}' must be partial to generate inspector buttons.",
        category: "Usage",
        defaultSeverity: DiagnosticSeverity.Error,
        isEnabledByDefault: true
    );
}

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

    internal static DiagnosticDescriptor PreviousVersionKeptDescriptor { get; } = new
    (
        id: "UCGEN002",
        title: "Generated code check failed, previous version kept",
        messageFormat: "Generated code for '{0}' does not compile, so its previous version stays in place: {1}",
        category: "CodeGeneration",
        defaultSeverity: DiagnosticSeverity.Warning,
        isEnabledByDefault: true
    );

    internal static DiagnosticDescriptor NothingEmittedDescriptor { get; } = new
    (
        id: "UCGEN003",
        title: "Generated code check failed, nothing emitted",
        messageFormat: "Generated code for '{0}' does not compile and has no previous version to keep: {1}",
        category: "CodeGeneration",
        defaultSeverity: DiagnosticSeverity.Error,
        isEnabledByDefault: true
    );
}

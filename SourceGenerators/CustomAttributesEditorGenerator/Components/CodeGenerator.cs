using System;
using System.Linq;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;

using static Microsoft.CodeAnalysis.CSharp.SyntaxFactory;
using static Microsoft.CodeAnalysis.CSharp.SyntaxKind;

namespace CustomAttributesEditorGenerator.Components;

internal static class CodeGenerator
{
    public const string AttributeFullName = $"{nameof(InspectorAttributes)}.{nameof(InspectorAttributes.ButtonAttribute)}";

    private static readonly Version Version = new(1, 0, 0);

    public static void GenerateDrawer(SourceProductionContext productionContext, IGrouping<ISymbol?, IMethodSymbol> methods)
    {
        if (methods.Key is not INamedTypeSymbol container) return;

        var hasPartialDeclaration = container.DeclaringSyntaxReferences
            .Select(static syntaxReference => syntaxReference.GetSyntax())
            .OfType<ClassDeclarationSyntax>()
            .Any(static declarationSyntax => declarationSyntax.Modifiers
                .Any(static token => token.IsKind(PartialKeyword)));

        if (hasPartialDeclaration is false)
        {
            var location = container.Locations.FirstOrDefault() ?? Location.None;
            productionContext.ReportDiagnostic(Diagnostic.Create
            (
                Diagnostics.MissingPartialDescriptor,
                location,
                messageArgs: container.Name
            ));

            return;
        }

        var ownerName = container.Name.Replace("global::", "")
            .Replace('<', '_')
            .Replace('>', '_');
        var buttonDefineKey = ownerName + "RequireButton";
        var drawerName = "Drawer";

        var inspectorGUI = Method.CreateInspectorGUIMethod();
        var buttonsDetectedPartialMethod = Method.CreateWhenButtonAttributeDetectedPartialMethod(buttonDefineKey);
        var buttonsDetectedMethod = Method.CreateWhenButtonAttributeDetectedMethod(ownerName, methods);

        var drawerClass = Class.CreateClass(drawerName,
            modifiers: [InternalKeyword, SealedKeyword, PartialKeyword],
            baseTypes: [nameof(UnityEditor.Editor)],
            attributes: [
                ("ExcludeFromCodeCoverage", []),
                ("DebuggerNonUserCode", []),
                ("GeneratedCode", Args: ["CustomAttributesEditorGenerator", Version.ToString()]),
                ("CustomEditor", Args: [ ParseTypeName(ownerName) ])
            ]);
        var shortDrawerClass = Class.CreateClass(drawerName,
            modifiers: [PartialKeyword],
            baseTypes: [],
            attributes: []);
        var ownerClass = Class.CreateClass(ownerName,
            modifiers: [PartialKeyword],
            baseTypes: [],
            attributes: [
                ("ExcludeFromCodeCoverage", []),
                ("DebuggerNonUserCode", []),
                ("GeneratedCode", Args: ["CustomAttributesEditorGenerator", Version.ToString()])
            ]);
        var shortOwnerClass = Class.CreateClass(ownerName,
            modifiers: [PartialKeyword],
            baseTypes: [],
            attributes: []);

        var @namespace = Namespace.CreateNamespace(container.ContainingNamespace.ToDisplayString());

        var compilation = CompilationUnit();
        var ownerCompilation = compilation
            .AddUsings(
                Using.System.CodeDom.Compiler.Value,
                Using.System.Diagnostics.Value,
                Using.System.Diagnostics.CodeAnalysis.Value,
                Using.Unity.Editor.Value,
                Using.Unity.Engine.UIElements.Value)
            .AddButtonHeader(buttonDefineKey)
            .AddGenericFooter();
        var shortOwnerCompilation = compilation
            .AddUsings(
                Using.Unity.Editor.Value,
                Using.Unity.Engine.UIElements.Value)
            .AddGenericHeader()
            .AddGenericFooter();

        var ownerSourceCode = ownerCompilation
            .AddNamespace(@namespace
                .AddClass(ownerClass
                    .AddClass(drawerClass
                        .AddMethod(inspectorGUI)
                        .AddMethod(buttonsDetectedPartialMethod))));

        var drawerSourceCode = shortOwnerCompilation
            .AddNamespace(@namespace
                .AddClass(shortOwnerClass
                    .AddClass(shortDrawerClass
                        .AddMethod(buttonsDetectedMethod))));

        productionContext.AddSource($"{ownerName}.g.cs", ownerSourceCode
            .NormalizeWhitespace()
                .ToFullString());
        productionContext.AddSource($"{ownerName}.buttons.g.cs", drawerSourceCode
            .NormalizeWhitespace()
                .ToFullString());
    }
}

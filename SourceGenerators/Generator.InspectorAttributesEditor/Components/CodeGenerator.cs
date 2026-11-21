using System;
using System.Globalization;
using System.Text;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;

using static Microsoft.CodeAnalysis.CSharp.SyntaxFactory;
using static Microsoft.CodeAnalysis.CSharp.SyntaxKind;

using Using = Shared.AST.Using;

namespace Generator.InspectorAttributesEditor.Components;

internal static class CodeGenerator
{
    public const string ButtonAttributeFullName = $"{nameof(InspectorAttributes)}." +
                                                  $"{nameof(InspectorAttributes.ButtonAttribute)}";

    private static Version Version => Shared.Generator.Version.Current;

    public static void GenerateDrawer(SourceProductionContext productionContext, DrawerModel model)
    {
        productionContext.CancellationToken.ThrowIfCancellationRequested();

        var ownerName = model.OwnerName;
        const string drawerName = "Drawer";

        // These symbols are local to the generated source file.
        const string buttonDefineKey = "INSPECTOR_BUTTONS_REQUIRED";
        const string customUIDefineKey = "INSPECTOR_CUSTOM_UI_REQUIRED";

        var inspectorGUI = Method.CreateInspectorGUIMethod();
        var buttonsDeclaration = Method.CreateWhenButtonAttributeDetectedPartialMethod(buttonDefineKey);
        var buttonsImplementation = Method.CreateWhenButtonAttributeDetectedMethod(ownerName, model.Buttons);

        var customUI = model.RequiresCustomUI
            ? Method.CreateWhenCustomAttributeDetectedPartialMethod(customUIDefineKey)
            : Method.CreateWhenCustomAttributeMissedPartialMethod(customUIDefineKey);

        var drawerClass = Class.CreateClass
        (
            drawerName,
            modifiers: [InternalKeyword, SealedKeyword, PartialKeyword],
            baseTypes: [nameof(UnityEditor.Editor)],
            attributes:
            [
                ("ExcludeFromCodeCoverage", []),
                ("DebuggerNonUserCode", []),
                ("GeneratedCode", Args: ["Generator.InspectorAttributesEditor", Version.ToString()]),
                ("CustomEditor", Args: [ParseTypeName(ownerName)])
            ]
        );

        var shortDrawerClass = Class.CreateClass
        (
            drawerName,
            modifiers: [PartialKeyword],
            baseTypes: [],
            attributes: []
        );

        var ownerClass = Class.CreateClass
        (
            ownerName,
            modifiers: [PartialKeyword],
            baseTypes: [],
            attributes:
            [
                ("ExcludeFromCodeCoverage", []),
                ("DebuggerNonUserCode", []),
                ("GeneratedCode", Args: ["Generator.InspectorAttributesEditor", Version.ToString()])
            ]
        );

        var shortOwnerClass = Class.CreateClass
        (
            ownerName,
            modifiers: [PartialKeyword],
            baseTypes: [],
            attributes: []
        );

        var ownerCompilation = CompilationUnit()
            .AddUsings(
                Using.System.CodeDom.Compiler.Name,
                Using.System.Diagnostics.Name,
                Using.System.Diagnostics.CodeAnalysis.Name,
                Using.Unity.Editor.Name,
                Using.Unity.Engine.UIElements.Name)
            .AddHeaderWithDefinitions(
                buttonDefineKey,
                model.RequiresCustomUI ? customUIDefineKey : null)
            .AddGenericFooter();

        var buttonsCompilation = CompilationUnit()
            .AddUsings(
                Using.Unity.Editor.Name,
                Using.Unity.Engine.UIElements.Name)
            .AddGenericHeader()
            .AddGenericFooter();

        var ownerDeclaration = ownerClass.AddClass(drawerClass
            .AddMethod(inspectorGUI)
            .AddMethod(buttonsDeclaration)
            .AddMethod(customUI));

        var buttonsDeclarationContainer = shortOwnerClass.AddClass(shortDrawerClass
            .AddMethod(buttonsImplementation));

        var ownerSource = AddOwner
        (
            ownerCompilation,
            model.NamespaceName,
            ownerDeclaration
        );

        var buttonsSource = AddOwner
        (
            buttonsCompilation,
            model.NamespaceName,
            buttonsDeclarationContainer
        );

        var hintPrefix = CreateHintPrefix(model.Identity);

        productionContext.CancellationToken.ThrowIfCancellationRequested();

        productionContext.AddSource
        (
            hintName: $"{hintPrefix}.g.cs",
            ownerSource.NormalizeWhitespace(indentation: "\t", eol: "\n", elasticTrivia: true)
                .ToFullString()
        );

        productionContext.AddSource
        (
            hintName: $"{hintPrefix}.buttons.g.cs",
            buttonsSource.NormalizeWhitespace(indentation: "\t", eol: "\n", elasticTrivia: true)
                .ToFullString()
        );
    }

    private static CompilationUnitSyntax AddOwner
    (
        CompilationUnitSyntax compilation,
        string namespaceName,
        ClassDeclarationSyntax owner
    ) {
        return namespaceName.Length == 0
            ? compilation.AddMembers(owner)
            : compilation.AddNamespace(Namespace.CreateNamespace(namespaceName).AddClass(owner));
    }

    private static string CreateHintPrefix(string identity)
    {
        const int readableNameLimit = 32;

        var nameStart = identity.LastIndexOf('.') + 1;
        var readableNameLength = Math.Min(identity.Length - nameStart, readableNameLimit);

        var builder = new StringBuilder("InspectorButtons_");
        for (var index = 0; index < readableNameLength; index++)
        {
            var character = identity[nameStart + index];
            var isAsciiLetterOrDigit = character is >= 'A' and <= 'Z'
                or >= 'a' and <= 'z'
                or >= '0' and <= '9';

            builder.Append(isAsciiLetterOrDigit ? character : '_');
        }

        builder.Append('_');

        using var sha256 = System.Security.Cryptography.SHA256.Create();
        var hash = sha256.ComputeHash(Encoding.UTF8.GetBytes(identity));

        foreach (var value in hash)
        {
            builder.Append(value.ToString("x1", CultureInfo.InvariantCulture));
        }

        return builder.ToString();
    }
}

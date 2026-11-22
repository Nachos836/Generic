#define SCOPED_DRAWER

using System;
using System.Globalization;
using System.Text;
using Microsoft.CodeAnalysis;

namespace Generator.InspectorAttributesEditor.Components;

internal static class CodeGenerator
{
    public const string ButtonAttributeFullName = $"{nameof(InspectorAttributes)}." +
                                                  $"{nameof(InspectorAttributes.ButtonAttribute)}";

    private static Version Version => Shared.Generator.Version.Current;

    public static void GenerateDrawer(SourceProductionContext productionContext, DrawerModel model)
    {
        productionContext.CancellationToken.ThrowIfCancellationRequested();

#if SCOPED_DRAWER
        var (ownerText, buttonsText) = ScopedDrawer.Emit(model, Version.ToString());
#else
        var (ownerText, buttonsText) = ChainedDrawer.Emit(model, Version.ToString());
#endif

        var hintPrefix = CreateHintPrefix(model.Identity);

        productionContext.CancellationToken.ThrowIfCancellationRequested();

        productionContext.AddSource(hintName: $"{hintPrefix}.g.cs", ownerText);
        productionContext.AddSource(hintName: $"{hintPrefix}.buttons.g.cs", buttonsText);
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

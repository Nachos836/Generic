using System;
using Shared.CodeGeneration.Scoped.Rendering;

namespace Shared.CodeGeneration.Scoped;

internal static class Code
{
    public static string File(Action<FileScope> body, bool when = true)
    {
        if (when is false) return string.Empty;

        var scope = new FileScope();
        body(scope);
        return Renderer.Render(scope.Complete());
    }
}
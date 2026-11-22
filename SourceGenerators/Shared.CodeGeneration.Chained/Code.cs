namespace Shared.CodeGeneration.Chained;

internal static class Code
{
    public static FileChain File(bool when = true) => new(when);
}
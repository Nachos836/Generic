namespace Shared.Generator;

internal static class Version
{
    public static System.Version Current { get; } = typeof(Version).Assembly.GetName().Version;
}

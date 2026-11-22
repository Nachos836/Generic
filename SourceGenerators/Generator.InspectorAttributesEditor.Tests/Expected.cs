namespace Generator.InspectorAttributesEditor.Tests;

internal static class Expected
{
    private const string FourSpaces = "    ";

    public static string InType(string members) => Wrap("class C", members);
    public static string InMethod(string signature) => InType($"void M({signature});");
    public static string InBody(string statements) => InType(Wrap("void M()", statements));
    public static string OfExpression(string expression) => InBody(expression + ";");
    public static string OfType(string type) => InType(type + " f;");

    public static string ConvertToTabs(string text)
    {
        return string.Join("\n", text.Replace("\r\n", "\n").Split('\n').Select(ConvertLineToTabs));
    }

    private static string Wrap(string header, string body)
    {
        if (body.Length == 0) return $"{header}\n{{\n}}";

        var lines = ConvertToTabs(body).Split('\n').Select(static line => line.Length == 0 ? line : "\t" + line);
        return $"{header}\n{{\n{string.Join("\n", lines)}\n}}";
    }

    private static string ConvertLineToTabs(string line)
    {
        var tabs = 0;
        while (line.StartsWith(FourSpaces, StringComparison.Ordinal))
        {
            line = line.Substring(FourSpaces.Length);
            tabs++;
        }

        return new string('\t', tabs) + line;
    }
}
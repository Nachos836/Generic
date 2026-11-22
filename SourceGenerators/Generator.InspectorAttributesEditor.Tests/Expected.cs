namespace Generator.InspectorAttributesEditor.Tests;

internal static class Expected
{
    private const string FourSpaces = "    ";

    public static string InType(string members) => Wrap("class C", members);
    public static string InMethod(string signature) => InType($"void M({signature});");
    public static string InBody(string statements) => InType(Wrap("void M()", statements));
    public static string OfExpression(string expression) => InBody(expression + ";");
    public static string OfType(string type) => InType(type + " f;");

    public static string Tabs(string text)
    {
        return string.Join("\n", text.Replace("\r\n", "\n").Split('\n').Select(ToTabs));
    }

    private static string Wrap(string header, string body)
    {
        var indented = Indent(body);
        return indented.Length == 0 ? $"{header}\n{{\n}}" : $"{header}\n{{\n{indented}\n}}";
    }

    private static string Indent(string text)
    {
        if (text.Length == 0) return string.Empty;
        return string.Join("\n", Tabs(text).Split('\n').Select(static line => line.Length == 0 ? line : "\t" + line));
    }

    private static string ToTabs(string line)
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
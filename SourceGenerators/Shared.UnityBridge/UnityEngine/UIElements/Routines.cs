namespace UnityEngine.UIElements;

internal class VisualElement
{
    public void Add(VisualElement child) { }
}

internal sealed class Button : VisualElement
{
    public Button(System.Action clickEvent) { }

    // ReSharper disable once InconsistentNaming
    public string text { get; set; } = string.Empty;
}

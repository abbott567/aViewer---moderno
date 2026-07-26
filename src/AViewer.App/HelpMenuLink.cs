namespace AViewer.App;

public sealed class HelpMenuLink
{
    public string? Label { get; init; }

    public string? ResourceKey { get; init; }

    public string? Url { get; init; }

    public bool IsSeparator { get; init; }
}

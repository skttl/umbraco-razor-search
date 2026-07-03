namespace Umbraco.Community.RazorSearch.Configuration;

public sealed class RazorSearchOptions
{
    private RazorSearchSnapshotExtractionOptions _snapshotExtraction = new();

    public string[] ExcludedContentTypeAliases { get; set; } = [];

    public string? ExcludeFromSearchPropertyAlias { get; set; }

    public string HighlightPattern { get; set; } = "<mark>{0}</mark>";

    public string RenderRequestHeaderName { get; set; } = Constants.RenderRequestHeaderName;

    public string? RenderRequestToken { get; set; }

    public string DefaultRenderer { get; set; } = Rendering.RazorSearchRendererNames.Http;

    public RazorSearchHttpRendererOptions HttpRenderer { get; set; } = new();

    public RazorSearchRenderQueueOptions RenderQueue { get; set; } = new();

    public RazorSearchSnapshotExtractionOptions SnapshotExtraction
    {
        get => _snapshotExtraction;
        set
        {
            _snapshotExtraction = value ?? new RazorSearchSnapshotExtractionOptions();
            _snapshotExtraction.ApplyToGlobalSettings();
        }
    }
}

public sealed class RazorSearchHttpRendererOptions
{
    public string? BaseAddress { get; set; }

    public Dictionary<string, string> Headers { get; set; } = new(StringComparer.OrdinalIgnoreCase)
    {
        ["X-RazorSearch"] = "true",
    };

    public Dictionary<string, string> Cookies { get; set; } = new(StringComparer.OrdinalIgnoreCase);

    public TimeSpan Timeout { get; set; } = TimeSpan.FromSeconds(30);

    public bool AllowAutoRedirect { get; set; } = true;
}

public sealed class RazorSearchRenderQueueOptions
{
    public int Capacity { get; set; } = 256;

    public bool DeduplicateActiveJobs { get; set; } = true;
}

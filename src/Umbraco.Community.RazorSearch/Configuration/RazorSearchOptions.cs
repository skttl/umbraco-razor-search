using System.ComponentModel;
using System.ComponentModel.DataAnnotations;

namespace Umbraco.Community.RazorSearch.Configuration;

/// <summary>Rendered snapshot and public website search settings.</summary>
public sealed class RazorSearchOptions
{
    /// <summary>Content type aliases omitted from rendering and search.</summary>
    public string[] ExcludedContentTypeAliases { get; set; } = [];
    /// <summary>Boolean content property used to exclude a document from search.</summary>
    public string? ExcludeFromSearchPropertyAlias { get; set; }
    [DefaultValue("<mark>{0}</mark>")]
    public string HighlightPattern { get; set; } = "<mark>{0}</mark>";
    [DefaultValue(Constants.RenderRequestHeaderName)]
    public string RenderRequestHeaderName { get; set; } = Constants.RenderRequestHeaderName;
    /// <summary>Shared secret for render requests. Required when rendering another app instance.</summary>
    public string? RenderRequestToken { get; set; }
    /// <summary>Registered renderer name. Custom renderer names are supported.</summary>
    [DefaultValue("http")]
    public string DefaultRenderer { get; set; } = Rendering.RazorSearchRendererNames.Http;
    public RazorSearchHttpRendererOptions HttpRenderer { get; set; } = new();
    public RazorSearchRenderQueueOptions RenderQueue { get; set; } = new();
    public RazorSearchSnapshotExtractionOptions SnapshotExtraction { get; set; } = new();
}

public sealed class RazorSearchHttpRendererOptions
{
    /// <summary>Public base URL used when a published route is relative.</summary>
    public string? BaseAddress { get; set; }
    /// <summary>Internal HTTP(S) origin of the dedicated backoffice server. Preserves the public host, scheme, path and culture. Requires a shared RenderRequestToken.</summary>
    public string? RenderBaseAddress { get; set; }
    public Dictionary<string, string> Headers { get; set; } = new(StringComparer.OrdinalIgnoreCase)
    {
        ["X-RazorSearch"] = "true",
    };
    public Dictionary<string, string> Cookies { get; set; } = new(StringComparer.OrdinalIgnoreCase);
    [DefaultValue(typeof(TimeSpan), "00:00:30")]
    public TimeSpan Timeout { get; set; } = TimeSpan.FromSeconds(30);
    /// <summary>Follow at most five redirects within the original public origin. Cross-origin redirects always fail.</summary>
    [DefaultValue(false)]
    public bool AllowAutoRedirect { get; set; }
}

public sealed class RazorSearchRenderQueueOptions
{
    [Range(1, 100000), DefaultValue(256)]
    public int Capacity { get; set; } = 256;
    [Range(1, 10), DefaultValue(3)]
    public int MaxAttempts { get; set; } = 3;
    [DefaultValue(typeof(TimeSpan), "00:00:02")]
    public TimeSpan RetryDelay { get; set; } = TimeSpan.FromSeconds(2);
    [Range(0, 100000), DefaultValue(1000)]
    public int CompletedJobRetention { get; set; } = 1000;
}

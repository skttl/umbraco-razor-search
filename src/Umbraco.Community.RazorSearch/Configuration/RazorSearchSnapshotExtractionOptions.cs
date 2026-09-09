using Umbraco.Community.RazorSearch.Services;

namespace Umbraco.Community.RazorSearch.Configuration;

/// <summary>Missing source arrays use defaults; an explicit empty array disables that field.</summary>
public sealed class RazorSearchSnapshotExtractionOptions
{
    /// <summary>Ordered sources: the first nonempty value becomes the title. Defaults to the title element.</summary>
    public RazorSearchSnapshotSourceDefinition[] TitleSources { get; set; } = DefaultTitleSources();
    /// <summary>Ordered sources: the first nonempty value becomes the summary. Defaults to the description meta element.</summary>
    public RazorSearchSnapshotSourceDefinition[] SummarySources { get; set; } = DefaultSummarySources();
    /// <summary>Combined heading sources. Defaults to h1 through h6.</summary>
    public RazorSearchSnapshotSourceDefinition[] HeadingSources { get; set; } = DefaultHeadingSources();
    /// <summary>Combined body sources. Defaults to body. An explicit list replaces this default.</summary>
    public RazorSearchSnapshotSourceDefinition[] BodySources { get; set; } = DefaultBodySources();
    /// <summary>CSS selectors removed before extraction, in addition to script, style, noscript and template.</summary>
    public string[] RemoveSelectors { get; set; } = [];

    internal RazorSearchSnapshotExtractionSettings CreateSettings() => new(
        ParseSources(TitleSources), ParseSources(SummarySources), ParseSources(HeadingSources),
        ParseSources(BodySources), RemoveSelectors.ToArray());

    private static RazorSearchSnapshotTextSource[] ParseSources(RazorSearchSnapshotSourceDefinition[] sources)
        => sources.Select(source => RazorSearchSnapshotTextSource.TryParse(source, out var parsed) && parsed is not null
            ? parsed : throw new InvalidOperationException("Invalid RazorSearch extraction source. Validate configuration before rendering.")).ToArray();

    internal static RazorSearchSnapshotSourceDefinition[] DefaultTitleSources() => [RazorSearchSnapshotSourceDefinition.SelectorSource("title")];
    internal static RazorSearchSnapshotSourceDefinition[] DefaultSummarySources() => [RazorSearchSnapshotSourceDefinition.SelectorSource("meta[name='description']", "content")];
    internal static RazorSearchSnapshotSourceDefinition[] DefaultHeadingSources() => [RazorSearchSnapshotSourceDefinition.SelectorSource("h1, h2, h3, h4, h5, h6")];
    internal static RazorSearchSnapshotSourceDefinition[] DefaultBodySources() => [RazorSearchSnapshotSourceDefinition.SelectorSource("body")];
}

internal sealed record RazorSearchSnapshotExtractionSettings(
    RazorSearchSnapshotTextSource[] TitleSources,
    RazorSearchSnapshotTextSource[] SummarySources,
    RazorSearchSnapshotTextSource[] HeadingSources,
    RazorSearchSnapshotTextSource[] BodySources,
    string[] RemoveSelectors)
{
    public static RazorSearchSnapshotExtractionSettings Default => new RazorSearchSnapshotExtractionOptions().CreateSettings();
}

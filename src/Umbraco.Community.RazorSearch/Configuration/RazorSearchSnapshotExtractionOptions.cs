using Umbraco.Community.RazorSearch.Services;

namespace Umbraco.Community.RazorSearch.Configuration;

public sealed class RazorSearchSnapshotExtractionOptions
{
    private RazorSearchSnapshotSourceDefinition[] _titleSources = DefaultTitleSources();
    private RazorSearchSnapshotSourceDefinition[] _summarySources = DefaultSummarySources();
    private RazorSearchSnapshotSourceDefinition[] _headingSources = DefaultHeadingSources();
    private RazorSearchSnapshotSourceDefinition[] _bodySources = DefaultBodySources();
    private string[] _removeSelectors = [];

    public RazorSearchSnapshotExtractionOptions()
    {
        ApplyToGlobalSettings();
    }

    public RazorSearchSnapshotSourceDefinition[] TitleSources
    {
        get => _titleSources;
        set
        {
            _titleSources = NormalizeSources(value, DefaultTitleSources);
            ApplyToGlobalSettings();
        }
    }

    public RazorSearchSnapshotSourceDefinition[] SummarySources
    {
        get => _summarySources;
        set
        {
            _summarySources = NormalizeSources(value, DefaultSummarySources);
            ApplyToGlobalSettings();
        }
    }

    public RazorSearchSnapshotSourceDefinition[] HeadingSources
    {
        get => _headingSources;
        set
        {
            _headingSources = NormalizeSources(value, DefaultHeadingSources);
            ApplyToGlobalSettings();
        }
    }

    public RazorSearchSnapshotSourceDefinition[] BodySources
    {
        get => _bodySources;
        set
        {
            _bodySources = NormalizeSources(value, DefaultBodySources);
            ApplyToGlobalSettings();
        }
    }

    public string[] RemoveSelectors
    {
        get => _removeSelectors;
        set
        {
            _removeSelectors = NormalizeSelectors(value);
            ApplyToGlobalSettings();
        }
    }

    internal void ApplyToGlobalSettings()
        => RazorSearchSnapshotExtractionSettingsStore.Set(new RazorSearchSnapshotExtractionSettings(
            NormalizeRuntimeSources(_titleSources, DefaultTitleRuntimeSources),
            NormalizeRuntimeSources(_summarySources, DefaultSummaryRuntimeSources),
            NormalizeRuntimeSources(_headingSources, DefaultHeadingRuntimeSources),
            NormalizeRuntimeSources(_bodySources, DefaultBodyRuntimeSources),
            NormalizeSelectors(_removeSelectors)));

    private static RazorSearchSnapshotSourceDefinition[] NormalizeSources(
        RazorSearchSnapshotSourceDefinition[]? definitions,
        Func<RazorSearchSnapshotSourceDefinition[]> fallbackFactory)
    {
        RazorSearchSnapshotSourceDefinition[] normalized = (definitions ?? [])
            .Where(x => x is not null)
            .Select(x => x.Normalize())
            .Where(x => RazorSearchSnapshotTextSource.TryParse(x, out _))
            .ToArray();

        return normalized.Length == 0
            ? fallbackFactory()
            : normalized;
    }

    private static RazorSearchSnapshotTextSource[] NormalizeRuntimeSources(
        RazorSearchSnapshotSourceDefinition[]? definitions,
        RazorSearchSnapshotTextSource[] fallback)
    {
        RazorSearchSnapshotTextSource[] sources = (definitions ?? [])
            .Where(x => x is not null)
            .Select(x => x.Normalize())
            .Select(TryParseRuntimeSource)
            .Where(x => x is not null)
            .Cast<RazorSearchSnapshotTextSource>()
            .ToArray();

        return sources.Length == 0
            ? fallback
            : sources;
    }

    private static RazorSearchSnapshotTextSource? TryParseRuntimeSource(RazorSearchSnapshotSourceDefinition definition)
        => RazorSearchSnapshotTextSource.TryParse(definition, out RazorSearchSnapshotTextSource? source)
            ? source
            : null;

    private static string[] NormalizeSelectors(string[]? values)
    {
        return (values ?? [])
            .Where(x => string.IsNullOrWhiteSpace(x) is false)
            .Select(x => x.Trim())
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToArray();
    }

    internal static RazorSearchSnapshotSourceDefinition[] DefaultTitleSources()
        => [RazorSearchSnapshotSourceDefinition.SelectorSource("title")];

    internal static RazorSearchSnapshotSourceDefinition[] DefaultSummarySources()
        => [RazorSearchSnapshotSourceDefinition.SelectorSource("meta[name='description']", "content")];

    internal static RazorSearchSnapshotSourceDefinition[] DefaultHeadingSources()
        =>
        [
            RazorSearchSnapshotSourceDefinition.SelectorSource("h1"),
            RazorSearchSnapshotSourceDefinition.SelectorSource("h2"),
            RazorSearchSnapshotSourceDefinition.SelectorSource("h3"),
            RazorSearchSnapshotSourceDefinition.SelectorSource("h4"),
            RazorSearchSnapshotSourceDefinition.SelectorSource("h5"),
            RazorSearchSnapshotSourceDefinition.SelectorSource("h6"),
        ];

    internal static RazorSearchSnapshotSourceDefinition[] DefaultBodySources()
        => [RazorSearchSnapshotSourceDefinition.SelectorSource("body")];

    private static RazorSearchSnapshotTextSource[] DefaultTitleRuntimeSources { get; } = CreateRuntimeDefaults(DefaultTitleSources());

    private static RazorSearchSnapshotTextSource[] DefaultSummaryRuntimeSources { get; } = CreateRuntimeDefaults(DefaultSummarySources());

    private static RazorSearchSnapshotTextSource[] DefaultHeadingRuntimeSources { get; } = CreateRuntimeDefaults(DefaultHeadingSources());

    private static RazorSearchSnapshotTextSource[] DefaultBodyRuntimeSources { get; } = CreateRuntimeDefaults(DefaultBodySources());

    internal static RazorSearchSnapshotTextSource[] CreateRuntimeDefaults(RazorSearchSnapshotSourceDefinition[] definitions)
        => definitions
            .Select(TryParseRuntimeSource)
            .Where(x => x is not null)
            .Cast<RazorSearchSnapshotTextSource>()
            .ToArray();
}

internal sealed record RazorSearchSnapshotExtractionSettings(
    RazorSearchSnapshotTextSource[] TitleSources,
    RazorSearchSnapshotTextSource[] SummarySources,
    RazorSearchSnapshotTextSource[] HeadingSources,
    RazorSearchSnapshotTextSource[] BodySources,
    string[] RemoveSelectors)
{
    public static RazorSearchSnapshotExtractionSettings Default { get; } = new(
        RazorSearchSnapshotExtractionOptions.CreateRuntimeDefaults(RazorSearchSnapshotExtractionOptions.DefaultTitleSources()),
        RazorSearchSnapshotExtractionOptions.CreateRuntimeDefaults(RazorSearchSnapshotExtractionOptions.DefaultSummarySources()),
        RazorSearchSnapshotExtractionOptions.CreateRuntimeDefaults(RazorSearchSnapshotExtractionOptions.DefaultHeadingSources()),
        RazorSearchSnapshotExtractionOptions.CreateRuntimeDefaults(RazorSearchSnapshotExtractionOptions.DefaultBodySources()),
        []);
}

internal static class RazorSearchSnapshotExtractionSettingsStore
{
    private static RazorSearchSnapshotExtractionSettings _current = RazorSearchSnapshotExtractionSettings.Default;

    public static RazorSearchSnapshotExtractionSettings Current => _current;

    public static void Set(RazorSearchSnapshotExtractionSettings settings)
        => _current = settings;
}

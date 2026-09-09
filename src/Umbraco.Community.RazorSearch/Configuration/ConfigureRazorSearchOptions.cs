using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Options;

namespace Umbraco.Community.RazorSearch.Configuration;

/// <summary>Binds arrays independently so explicit arrays replace defaults, including empty JSON arrays.</summary>
internal sealed class ConfigureRazorSearchOptions(IConfiguration configuration) : IConfigureOptions<RazorSearchOptions>
{
    public void Configure(RazorSearchOptions options)
    {
        IConfigurationSection section = configuration.GetSection(Constants.ConfigurationSection);
        section.Bind(options, binder => binder.ErrorOnUnknownConfiguration = true);
        if (options.SnapshotExtraction is null) return; // The options validator reports a clear configuration error.
        IConfigurationSection extraction = section.GetSection(nameof(options.SnapshotExtraction));
        options.SnapshotExtraction.TitleSources = Sources(extraction, nameof(options.SnapshotExtraction.TitleSources), options.SnapshotExtraction.TitleSources);
        options.SnapshotExtraction.SummarySources = Sources(extraction, nameof(options.SnapshotExtraction.SummarySources), options.SnapshotExtraction.SummarySources);
        options.SnapshotExtraction.HeadingSources = Sources(extraction, nameof(options.SnapshotExtraction.HeadingSources), options.SnapshotExtraction.HeadingSources);
        options.SnapshotExtraction.BodySources = Sources(extraction, nameof(options.SnapshotExtraction.BodySources), options.SnapshotExtraction.BodySources);
    }

    private static RazorSearchSnapshotSourceDefinition[] Sources(IConfigurationSection extraction, string name, RazorSearchSnapshotSourceDefinition[] fallback)
    {
        // GetChildren retains an explicitly configured empty/null JSON array, unlike Exists().
        IConfigurationSection? configured = extraction.GetChildren().FirstOrDefault(x => x.Key.Equals(name, StringComparison.OrdinalIgnoreCase));
        if (configured is not null && configured.Value is null && !configured.GetChildren().Any())
            throw new InvalidOperationException($"{Constants.ConfigurationSection}:SnapshotExtraction:{name} cannot be null; use [] to disable it.");
        return configured is null ? fallback : configured.Get<RazorSearchSnapshotSourceDefinition[]>() ?? [];
    }
}

using Umbraco.Community.RazorSearch.Persistence.Models;

namespace Umbraco.Community.RazorSearch.Indexing;

internal sealed record RazorSearchIndexedVariant(
    string? Culture,
    string[] Titles,
    string[] Summaries,
    string[] Headings,
    string[] Bodies);

internal static class RazorSearchSnapshotIndexProjection
{
    public static IReadOnlyCollection<RazorSearchIndexedVariant> ProjectSuccessfulVariants(
        IEnumerable<RazorSearchSnapshot> snapshots,
        IReadOnlySet<string>? requestedCultures = null)
        => snapshots
            .Where(x => string.Equals(x.RenderStatus, RazorSearchSnapshotStatuses.Success, StringComparison.OrdinalIgnoreCase))
            .Where(x => ShouldIncludeCulture(x.Culture, requestedCultures))
            .GroupBy(x => NormalizeVariant(x.Culture), StringComparer.OrdinalIgnoreCase)
            .Select(snapshotGroup => new RazorSearchIndexedVariant(
                DenormalizeVariant(snapshotGroup.Key),
                CollectDistinct(snapshotGroup.Select(x => x.TitleText)),
                CollectDistinct(snapshotGroup.Select(x => x.SummaryText)),
                CollectDistinct(snapshotGroup.Select(x => x.HeadingText)),
                CollectDistinct(snapshotGroup.Select(x => x.BodyText))))
            .ToArray();

    private static string[] CollectDistinct(IEnumerable<string?> values) => values
        .Where(x => string.IsNullOrWhiteSpace(x) is false)
        .Select(x => x!.Trim())
        .Distinct(StringComparer.OrdinalIgnoreCase)
        .ToArray();

    private static bool ShouldIncludeCulture(string? snapshotCulture, IReadOnlySet<string>? requestedCultures)
    {
        if (requestedCultures is null || requestedCultures.Count == 0)
        {
            return true;
        }

        return requestedCultures.Contains(NormalizeVariant(snapshotCulture));
    }

    private static string NormalizeVariant(string? value) => value ?? string.Empty;

    private static string? DenormalizeVariant(string value) => string.IsNullOrWhiteSpace(value) ? null : value;
}

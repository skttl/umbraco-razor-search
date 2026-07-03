using Microsoft.Extensions.DependencyInjection;
using Umbraco.Cms.Core.Models;
using Umbraco.Cms.Search.Core.Models.Indexing;
using Umbraco.Cms.Search.Core.Services.ContentIndexing;
using Umbraco.Community.RazorSearch.Persistence.Models;
using Umbraco.Community.RazorSearch.Persistence.Stores;

namespace Umbraco.Community.RazorSearch.Indexing;

public sealed class RazorSearchSnapshotContentIndexer(
    IServiceScopeFactory serviceScopeFactory,
    Searching.IRazorSearchContentFilter contentFilter)
    : IContentIndexer
{
    public async Task<IEnumerable<IndexField>> GetIndexFieldsAsync(
        IContentBase content,
        string?[] cultures,
        bool published,
        CancellationToken cancellationToken)
    {
        if (published is false)
        {
            return [];
        }

        if (contentFilter.IsExcludedContentType(content.ContentType.Alias))
        {
            return [];
        }

        await using AsyncServiceScope scope = serviceScopeFactory.CreateAsyncScope();
        IRazorSearchSnapshotStore snapshotStore = scope.ServiceProvider.GetRequiredService<IRazorSearchSnapshotStore>();
        IReadOnlyCollection<RazorSearchSnapshot> snapshots = await snapshotStore.GetByContentKeyAsync(content.Key, cancellationToken);
        RazorSearchSnapshot[] successfulSnapshots = snapshots
            .Where(x => string.Equals(x.RenderStatus, RazorSearchSnapshotStatuses.Success, StringComparison.OrdinalIgnoreCase))
            .ToArray();

        if (successfulSnapshots.Length == 0)
        {
            return [];
        }

        HashSet<string>? requestedCultures = cultures.Length == 0
            ? null
            : cultures
                .Where(x => string.IsNullOrWhiteSpace(x) is false)
                .Select(x => x!)
                .ToHashSet(StringComparer.OrdinalIgnoreCase);

        var indexFields = new List<IndexField>();

        foreach (IGrouping<(string Culture, string Segment), RazorSearchSnapshot> snapshotGroup in successfulSnapshots
                     .Where(x => ShouldIncludeCulture(x.Culture, requestedCultures))
                     .GroupBy(x => (NormalizeVariant(x.Culture), NormalizeVariant(x.Segment))))
        {
            string culture = snapshotGroup.Key.Culture;
            string segment = snapshotGroup.Key.Segment;
            string? variantCulture = DenormalizeVariant(culture);
            string? variantSegment = DenormalizeVariant(segment);

            if (contentFilter.IsExcluded(content, variantCulture, variantSegment, published))
            {
                continue;
            }

            string[] titles = CollectDistinct(snapshotGroup.Select(x => x.TitleText));
            string[] summaries = CollectDistinct(snapshotGroup.Select(x => x.SummaryText));
            string[] headings = CollectDistinct(snapshotGroup.Select(x => x.HeadingText));
            string[] bodies = CollectDistinct(snapshotGroup.Select(x => x.BodyText));

            AppendIndexField(indexFields, Constants.TitleFieldName, variantCulture, variantSegment, textsR1: titles);
            AppendIndexField(indexFields, Constants.SummaryFieldName, variantCulture, variantSegment, textsR2: summaries);
            AppendIndexField(indexFields, Constants.HeadingFieldName, variantCulture, variantSegment, textsR2: headings);
            AppendIndexField(indexFields, Constants.ContentFieldName, variantCulture, variantSegment, texts: bodies);
        }

        return indexFields;
    }

    private static void AppendIndexField(
        ICollection<IndexField> fields,
        string fieldName,
        string? culture,
        string? segment,
        IEnumerable<string>? textsR1 = null,
        IEnumerable<string>? textsR2 = null,
        IEnumerable<string>? texts = null)
    {
        string[] normalizedTextsR1 = CollectDistinct(textsR1 ?? []);
        string[] normalizedTextsR2 = CollectDistinct(textsR2 ?? []);
        string[] normalizedTexts = CollectDistinct(texts ?? []);

        if (normalizedTextsR1.Length == 0 && normalizedTextsR2.Length == 0 && normalizedTexts.Length == 0)
        {
            return;
        }

        fields.Add(new IndexField(
            fieldName,
            new IndexValue
            {
                TextsR1 = normalizedTextsR1,
                TextsR2 = normalizedTextsR2,
                Texts = normalizedTexts,
            },
            culture!,
            segment!));
    }

    private static string[] CollectDistinct(IEnumerable<string?> values) => values
        .Where(x => string.IsNullOrWhiteSpace(x) is false)
        .Select(x => x!.Trim())
        .Distinct(StringComparer.OrdinalIgnoreCase)
        .ToArray();

    private static bool ShouldIncludeCulture(string? snapshotCulture, HashSet<string>? requestedCultures)
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

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

        RazorSearchIndexedVariant[] successfulVariants = RazorSearchSnapshotIndexProjection
            .ProjectSuccessfulVariants(snapshots, requestedCultures)
            .ToArray();

        if (successfulVariants.Length == 0)
        {
            return [];
        }

        var indexFields = new List<IndexField>();

        foreach (RazorSearchIndexedVariant variant in successfulVariants)
        {
            if (contentFilter.IsExcluded(content, variant.Culture, variant.Segment, published))
            {
                continue;
            }

            AppendIndexField(indexFields, Constants.TitleFieldName, variant.Culture, variant.Segment, textsR1: variant.Titles);
            AppendIndexField(indexFields, Constants.SummaryFieldName, variant.Culture, variant.Segment, textsR2: variant.Summaries);
            AppendIndexField(indexFields, Constants.HeadingFieldName, variant.Culture, variant.Segment, textsR2: variant.Headings);
            AppendIndexField(indexFields, Constants.ContentFieldName, variant.Culture, variant.Segment, texts: variant.Bodies);
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

}

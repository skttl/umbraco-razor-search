using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Umbraco.Cms.Core;
using Umbraco.Cms.Core.Events;
using Umbraco.Cms.Core.Models;
using Umbraco.Cms.Core.Services;
using Umbraco.Cms.Search.Core.Extensions;
using Umbraco.Cms.Search.Core.Models.Indexing;
using Umbraco.Cms.Search.Core.Notifications;
using Umbraco.Cms.Search.Core.Services.ContentIndexing;
using Umbraco.Community.RazorSearch.Persistence.Models;
using Umbraco.Community.RazorSearch.Persistence.Stores;
using Umbraco.Community.RazorSearch.Searching;
using Umbraco.Extensions;

namespace Umbraco.Community.RazorSearch.Indexing;

internal sealed class RazorSearchContentIndexChangeStrategy(
    IServiceScopeFactory serviceScopeFactory,
    ILogger<RazorSearchContentIndexChangeStrategy> logger)
    : IContentChangeStrategy
{
    private static readonly HashSet<string> RequiredSystemFieldNames =
    [
        Umbraco.Cms.Search.Core.Constants.FieldNames.Id,
        Umbraco.Cms.Search.Core.Constants.FieldNames.ParentId,
        Umbraco.Cms.Search.Core.Constants.FieldNames.PathIds,
        Umbraco.Cms.Search.Core.Constants.FieldNames.ContentTypeId,
    ];

    public async Task HandleAsync(
        IEnumerable<ContentIndexInfo> indexInfos,
        IEnumerable<ContentChange> changes,
        CancellationToken cancellationToken)
    {
        using IServiceScope scope = serviceScopeFactory.CreateScope();
        ScopedServices services = ActivatorUtilities.CreateInstance<ScopedServices>(scope.ServiceProvider);

        ContentIndexInfo[] documentIndexes = indexInfos
            .Where(x => x.ContainedObjectTypes.Contains(UmbracoObjectTypes.Document))
            .ToArray();

        if (documentIndexes.Length == 0)
        {
            return;
        }

        foreach (ContentChange change in changes.Where(x => x.ObjectType == UmbracoObjectTypes.Document && x.ContentState == ContentState.Published))
        {
            IContent? content = change.ChangeImpact is ChangeImpact.Remove
                ? null
                : services.ContentService.GetById(change.Id);

            if (
                change.ChangeImpact is ChangeImpact.Remove
                || content is null
                || content.Trashed
                || content.Published is false
            )
            {
                await DeleteAsync(documentIndexes, [change.Id]);
                IContent? removedContent = services.ContentService.GetById(change.Id);
                if (removedContent is not null)
                {
                    await ReindexDescendantsAsync(documentIndexes, removedContent, services, cancellationToken);
                }
                continue;
            }

            await ReindexAsync(documentIndexes, content, services, cancellationToken);

            if (change.ChangeImpact is ChangeImpact.RefreshWithDescendants)
            {
                await ReindexDescendantsAsync(documentIndexes, content, services, cancellationToken);
            }
        }
    }

    public async Task RebuildAsync(ContentIndexInfo indexInfo, CancellationToken cancellationToken)
    {
        using IServiceScope scope = serviceScopeFactory.CreateScope();
        ScopedServices services = ActivatorUtilities.CreateInstance<ScopedServices>(scope.ServiceProvider);

        await indexInfo.Indexer.ResetAsync(indexInfo.IndexAlias);

        foreach (IContent rootContent in services.ContentService.GetRootContent())
        {
            if (cancellationToken.IsCancellationRequested)
            {
                logger.LogInformation(
                    "Cancelled RazorSearch index rebuild for {IndexAlias}.",
                    indexInfo.IndexAlias);
                return;
            }

            await ReindexAsync([indexInfo], rootContent, services, cancellationToken);
            await ReindexDescendantsAsync([indexInfo], rootContent, services, cancellationToken);
        }
    }

    private async Task ReindexAsync(
        IReadOnlyCollection<ContentIndexInfo> indexInfos,
        IContent content,
        ScopedServices services,
        CancellationToken cancellationToken)
    {
        Variation[] variations = RoutablePublishedVariations(content, services.ContentService);
        if (variations.Length == 0)
        {
            await DeleteAsync(indexInfos, [content.Key]);
            return;
        }

        IndexField[] fields = await BuildFieldsAsync(content, variations, services, cancellationToken);
        if (fields.Length == 0)
        {
            await DeleteAsync(indexInfos, [content.Key]);
            return;
        }

        ContentProtection? contentProtection = await services.ContentProtectionProvider.GetContentProtectionAsync(
            content);

        foreach (ContentIndexInfo indexInfo in indexInfos)
        {
            var notification = new ContentIndexingNotification(
                indexInfo.IndexAlias,
                content.Key,
                UmbracoObjectTypes.Document,
                variations,
                fields);

            if (await services.EventAggregator.PublishCancelableAsync(notification))
            {
                continue;
            }

            await indexInfo.Indexer.AddOrUpdateAsync(
                indexInfo.IndexAlias,
                content.Key,
                UmbracoObjectTypes.Document,
                variations,
                notification.Fields,
                contentProtection);
        }
    }

    private async Task ReindexDescendantsAsync(
        IReadOnlyCollection<ContentIndexInfo> indexInfos,
        IContent content,
        ScopedServices services,
        CancellationToken cancellationToken)
    {
        foreach (IContent descendant in GetDescendants(content, services.ContentService))
        {
            if (cancellationToken.IsCancellationRequested)
            {
                return;
            }

            await ReindexAsync(indexInfos, descendant, services, cancellationToken);
        }
    }

    private async Task<IndexField[]> BuildFieldsAsync(
        IContent content,
        IReadOnlyCollection<Variation> variations,
        ScopedServices services,
        CancellationToken cancellationToken)
    {
        string?[] cultures = variations
            .Select(x => x.Culture)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToArray();

        IndexField[] systemFields = (await services.SystemFieldsContentIndexer.GetIndexFieldsAsync(
                content,
                cultures,
                published: true,
                cancellationToken))
            .Where(x => RequiredSystemFieldNames.Contains(x.FieldName))
            .ToArray();

        IReadOnlyCollection<RazorSearchSnapshot> snapshots = await services.SnapshotStore.GetByContentKeyAsync(
            content.Key,
            cancellationToken);

        HashSet<string>? requestedCultures = cultures.Length == 0
            ? null
            : cultures
                .Where(x => string.IsNullOrWhiteSpace(x) is false)
                .Select(x => x!)
                .ToHashSet(StringComparer.OrdinalIgnoreCase);

        RazorSearchIndexedVariant[] indexedVariants = RazorSearchSnapshotIndexProjection
            .ProjectSuccessfulVariants(snapshots, requestedCultures)
            .Where(variant => variations.Any(x =>
                string.Equals(x.Culture, variant.Culture, StringComparison.OrdinalIgnoreCase)
                && string.Equals(x.Segment, null, StringComparison.OrdinalIgnoreCase)))
            // Providers associate fields with variations by exact culture equality. Persistence
            // normalizes identity casing, so restore the published culture's canonical spelling.
            .Select(variant => variant with
            {
                Culture = variations.First(x => string.Equals(x.Culture, variant.Culture, StringComparison.OrdinalIgnoreCase)).Culture,
            })
            .ToArray();

        if (indexedVariants.Length == 0)
        {
            return [];
        }

        var fields = new List<IndexField>(systemFields.Length + (indexedVariants.Length * 5));
        fields.AddRange(systemFields);

        foreach (RazorSearchIndexedVariant variant in indexedVariants)
        {
            AppendIndexField(
                fields,
                Constants.TitleFieldName,
                variant.Culture,
                null,
                textsR1: variant.Titles);
            AppendIndexField(
                fields,
                Constants.HeadingFieldName,
                variant.Culture,
                null,
                textsR2: variant.Headings);
            AppendIndexField(
                fields,
                Constants.ContentFieldName,
                variant.Culture,
                null,
                texts: variant.Bodies);

            fields.Add(
                new IndexField(
                    Constants.InternalIndex.ContentTypeAliasFieldName,
                    new IndexValue { Keywords = [content.ContentType.Alias] },
                    variant.Culture,
                    null));

            if (services.ContentFilter.IsExcluded(content, variant.Culture, published: true))
            {
                fields.Add(
                    new IndexField(
                        Constants.InternalIndex.ExcludedFlagFieldName,
                        new IndexValue { Integers = [1] },
                        variant.Culture,
                        null));
            }
        }

        return fields.ToArray();
    }

    private async Task DeleteAsync(
        IReadOnlyCollection<ContentIndexInfo> indexInfos,
        IReadOnlyCollection<Guid> ids)
    {
        if (ids.Count == 0)
        {
            return;
        }

        foreach (ContentIndexInfo indexInfo in indexInfos)
        {
            await indexInfo.Indexer.DeleteAsync(indexInfo.IndexAlias, ids);
        }
    }

    private IEnumerable<IContent> GetDescendants(IContent content, IContentService contentService)
    {
        long pageIndex = 0;
        const int pageSize = 128;

        while (true)
        {
            IContent[] page = contentService.GetPagedDescendants(
                    content.Id,
                    pageIndex,
                    pageSize,
                    out long totalRecords,
                    filter: null,
                    ordering: Ordering.ByDefault())
                .ToArray();

            if (page.Length == 0)
            {
                yield break;
            }

            foreach (IContent descendant in page)
            {
                yield return descendant;
            }

            pageIndex++;
            if (pageIndex * pageSize >= totalRecords)
            {
                yield break;
            }
        }
    }

    private Variation[] RoutablePublishedVariations(IContent content, IContentService contentService)
    {
        if (content.Published is false)
        {
            return [];
        }

        bool variesByCulture = content.ContentType.VariesByCulture();
        string?[] cultures = variesByCulture
            ? content.PublishedCultures.Cast<string?>().ToArray()
            : [null];

        foreach (int ancestorId in GetAncestorIds(content))
        {
            IContent? ancestor = contentService.GetById(ancestorId);
            if (ancestor is null || ancestor.Published is false)
            {
                cultures = [];
            }
            else if (variesByCulture && ancestor.ContentType.VariesByCulture())
            {
                cultures = cultures.Intersect(ancestor.PublishedCultures).ToArray();
            }

            if (cultures.Length == 0)
            {
                break;
            }
        }

        return cultures.Select(x => new Variation(x, null)).ToArray();
    }

    private static IEnumerable<int> GetAncestorIds(IContent content)
    {
        string[] pathParts = content.Path.Split(',', StringSplitOptions.RemoveEmptyEntries);
        foreach (string part in pathParts.Skip(1).SkipLast(1))
        {
            if (int.TryParse(part, out int ancestorId))
            {
                yield return ancestorId;
            }
        }
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

        if (
            normalizedTextsR1.Length == 0
            && normalizedTextsR2.Length == 0
            && normalizedTexts.Length == 0
        )
        {
            return;
        }

        fields.Add(
            new IndexField(
                fieldName,
                new IndexValue
                {
                    TextsR1 = normalizedTextsR1,
                    TextsR2 = normalizedTextsR2,
                    Texts = normalizedTexts,
                },
                culture,
                segment));
    }

    private static string[] CollectDistinct(IEnumerable<string?> values) =>
        values
            .Where(x => string.IsNullOrWhiteSpace(x) is false)
            .Select(x => x!.Trim())
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToArray();

    private sealed record ScopedServices(
        IContentService ContentService,
        IContentProtectionProvider ContentProtectionProvider,
        IEventAggregator EventAggregator,
        IRazorSearchSnapshotStore SnapshotStore,
        ISystemFieldsContentIndexer SystemFieldsContentIndexer,
        IRazorSearchContentFilter ContentFilter);
}

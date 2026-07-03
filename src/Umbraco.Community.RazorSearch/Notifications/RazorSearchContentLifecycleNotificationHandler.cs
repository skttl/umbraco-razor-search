using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Umbraco.Cms.Core;
using Umbraco.Cms.Core.Events;
using Umbraco.Cms.Core.Models;
using Umbraco.Cms.Core.Models.PublishedContent;
using Umbraco.Cms.Core.Notifications;
using Umbraco.Cms.Core.Services;
using Umbraco.Cms.Core.Web;
using Umbraco.Cms.Search.Core.Models.Indexing;
using Umbraco.Cms.Search.Core.Services.ContentIndexing;
using Umbraco.Community.RazorSearch.Models;
using Umbraco.Community.RazorSearch.Persistence.Models;
using Umbraco.Community.RazorSearch.Persistence.Stores;
using Umbraco.Community.RazorSearch.Searching;
using Umbraco.Community.RazorSearch.Services;
using Umbraco.Extensions;

namespace Umbraco.Community.RazorSearch.Notifications;

internal sealed class RazorSearchContentLifecycleNotificationHandler(
    IServiceScopeFactory serviceScopeFactory,
    ILogger<RazorSearchContentLifecycleNotificationHandler> logger)
    : INotificationAsyncHandler<ContentPublishedNotification>,
        INotificationAsyncHandler<ContentUnpublishedNotification>,
        INotificationAsyncHandler<ContentDeletedNotification>,
        INotificationAsyncHandler<ContentMovedNotification>,
        INotificationAsyncHandler<ContentMovedToRecycleBinNotification>
{
    public async Task HandleAsync(ContentPublishedNotification notification, CancellationToken cancellationToken)
    {
        await using AsyncServiceScope scope = serviceScopeFactory.CreateAsyncScope();
        IRazorSearchRenderQueue renderQueue = scope.ServiceProvider.GetRequiredService<IRazorSearchRenderQueue>();
        IRazorSearchSnapshotStore snapshotStore = scope.ServiceProvider.GetRequiredService<IRazorSearchSnapshotStore>();
        IUmbracoContextFactory umbracoContextFactory = scope.ServiceProvider.GetRequiredService<IUmbracoContextFactory>();
        IRazorSearchContentFilter contentFilter = scope.ServiceProvider.GetRequiredService<IRazorSearchContentFilter>();

        IContent[] publishedEntities = notification.PublishedEntities
            .DistinctBy(x => x.Key)
            .ToArray();

        int deletedSnapshotCount = 0;

        foreach (IContent content in publishedEntities)
        {
            deletedSnapshotCount += await DeleteSnapshotsForUnpublishedCulturesAsync(notification, content, snapshotStore, cancellationToken);
            deletedSnapshotCount += await DeleteSnapshotsForExcludedPublishedVariantsAsync(
                content,
                umbracoContextFactory,
                snapshotStore,
                contentFilter,
                cancellationToken);
        }

        await EnqueuePublishedContentAsync(
            renderQueue,
            umbracoContextFactory,
            publishedEntities,
            contentFilter,
            force: false,
            cancellationToken);

        logger.LogDebug(
            "Queued RazorSearch synchronization for {PublishedCount} published content item(s) after deleting {DeletedSnapshotCount} excluded or unpublished snapshot(s).",
            publishedEntities.Length,
            deletedSnapshotCount);
    }

    public async Task HandleAsync(ContentUnpublishedNotification notification, CancellationToken cancellationToken)
    {
        await using AsyncServiceScope scope = serviceScopeFactory.CreateAsyncScope();
        IRazorSearchSnapshotStore snapshotStore = scope.ServiceProvider.GetRequiredService<IRazorSearchSnapshotStore>();

        int deletedSnapshotCount = 0;

        foreach (IContent content in notification.UnpublishedEntities.DistinctBy(x => x.Key))
        {
            if (content.Published is false)
            {
                deletedSnapshotCount += await snapshotStore.DeleteByContentKeyAsync(content.Key, cancellationToken);
                continue;
            }

            deletedSnapshotCount += await DeleteSnapshotsForUnpublishedCulturesAsync(
                notification,
                content,
                snapshotStore,
                cancellationToken);
        }

        logger.LogDebug(
            "Deleted {DeletedSnapshotCount} RazorSearch snapshot(s) after content unpublish.",
            deletedSnapshotCount);
    }

    public async Task HandleAsync(ContentDeletedNotification notification, CancellationToken cancellationToken)
    {
        await using AsyncServiceScope scope = serviceScopeFactory.CreateAsyncScope();
        IRazorSearchSnapshotStore snapshotStore = scope.ServiceProvider.GetRequiredService<IRazorSearchSnapshotStore>();
        IDistributedContentIndexRefresher distributedContentIndexRefresher = scope.ServiceProvider.GetRequiredService<IDistributedContentIndexRefresher>();

        IContent[] deletedEntities = notification.DeletedEntities
            .DistinctBy(x => x.Key)
            .ToArray();

        int deletedSnapshotCount = await DeleteSnapshotsByContentKeysAsync(
            snapshotStore,
            deletedEntities.Select(x => x.Key),
            cancellationToken);

        distributedContentIndexRefresher.RefreshContent(deletedEntities, ContentState.Published);

        logger.LogDebug(
            "Deleted {DeletedSnapshotCount} RazorSearch snapshot(s) after content delete.",
            deletedSnapshotCount);
    }

    public async Task HandleAsync(ContentMovedNotification notification, CancellationToken cancellationToken)
    {
        await using AsyncServiceScope scope = serviceScopeFactory.CreateAsyncScope();
        IRazorSearchRenderQueue renderQueue = scope.ServiceProvider.GetRequiredService<IRazorSearchRenderQueue>();
        IRazorSearchSnapshotStore snapshotStore = scope.ServiceProvider.GetRequiredService<IRazorSearchSnapshotStore>();
        IContentService contentService = scope.ServiceProvider.GetRequiredService<IContentService>();
        IUmbracoContextFactory umbracoContextFactory = scope.ServiceProvider.GetRequiredService<IUmbracoContextFactory>();

        IContent[] movedRoots = notification.MoveInfoCollection
            .Select(x => x.Entity)
            .DistinctBy(x => x.Key)
            .ToArray();

        IReadOnlyCollection<IContent> affectedContent = ExpandContentTree(contentService, movedRoots);
        int deletedSnapshotCount = await DeleteSnapshotsByContentKeysAsync(
            snapshotStore,
            affectedContent.Select(x => x.Key),
            cancellationToken);

        await EnqueuePublishedContentAsync(
            renderQueue,
            umbracoContextFactory,
            affectedContent,
            scope.ServiceProvider.GetRequiredService<IRazorSearchContentFilter>(),
            force: true,
            cancellationToken);

        logger.LogDebug(
            "Deleted {DeletedSnapshotCount} RazorSearch snapshot(s) and queued synchronization for {AffectedCount} moved content item(s).",
            deletedSnapshotCount,
            affectedContent.Count);
    }

    public async Task HandleAsync(ContentMovedToRecycleBinNotification notification, CancellationToken cancellationToken)
    {
        await using AsyncServiceScope scope = serviceScopeFactory.CreateAsyncScope();
        IRazorSearchSnapshotStore snapshotStore = scope.ServiceProvider.GetRequiredService<IRazorSearchSnapshotStore>();
        IContentService contentService = scope.ServiceProvider.GetRequiredService<IContentService>();

        IContent[] movedRoots = notification.MoveInfoCollection
            .Select(x => x.Entity)
            .DistinctBy(x => x.Key)
            .ToArray();

        IReadOnlyCollection<IContent> affectedContent = ExpandContentTree(contentService, movedRoots);
        int deletedSnapshotCount = await DeleteSnapshotsByContentKeysAsync(
            snapshotStore,
            affectedContent.Select(x => x.Key),
            cancellationToken);

        logger.LogDebug(
            "Deleted {DeletedSnapshotCount} RazorSearch snapshot(s) after moving {AffectedCount} content item(s) to the recycle bin.",
            deletedSnapshotCount,
            affectedContent.Count);
    }

    private static IReadOnlyCollection<IContent> ExpandContentTree(IContentService contentService, IEnumerable<IContent> roots)
    {
        Dictionary<Guid, IContent> contentByKey = roots.ToDictionary(x => x.Key, x => x);

        foreach (IContent root in roots)
        {
            foreach (IContent descendant in GetDescendants(contentService, root))
            {
                contentByKey.TryAdd(descendant.Key, descendant);
            }
        }

        return contentByKey.Values.ToArray();
    }

    private static IEnumerable<IContent> GetDescendants(IContentService contentService, IContent rootContent)
    {
        long pageIndex = 0;
        const int pageSize = 128;

        while (true)
        {
            IEnumerable<IContent> descendants = contentService.GetPagedDescendants(
                rootContent.Id,
                pageIndex,
                pageSize,
                out long totalRecords,
                filter: null,
                ordering: Ordering.ByDefault());

            IContent[] page = descendants.ToArray();
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

    private static async Task EnqueuePublishedContentAsync(
        IRazorSearchRenderQueue renderQueue,
        IUmbracoContextFactory umbracoContextFactory,
        IEnumerable<IContent> contentItems,
        IRazorSearchContentFilter contentFilter,
        bool force,
        CancellationToken cancellationToken)
    {
        using UmbracoContextReference contextReference = umbracoContextFactory.EnsureUmbracoContext();

        foreach (IContent contentItem in contentItems.DistinctBy(x => x.Key))
        {
            IPublishedContent? publishedContent = contextReference.UmbracoContext.Content?.GetById(contentItem.Key);
            if (publishedContent is null)
            {
                continue;
            }

            if (contentFilter.IsExcluded(publishedContent))
            {
                continue;
            }

            IEnumerable<string?> cultures = publishedContent.Cultures.Count > 0
                ? publishedContent.Cultures.Keys.Cast<string?>()
                : [null];

            foreach (string? culture in cultures.Distinct(StringComparer.OrdinalIgnoreCase))
            {
                if (contentFilter.IsExcluded(publishedContent, culture))
                {
                    continue;
                }

                string route = publishedContent.Url(culture, UrlMode.Absolute);
                if (string.IsNullOrWhiteSpace(route) || route.StartsWith('#'))
                {
                    continue;
                }

                await renderQueue.EnqueueAsync(
                    new RazorSearchRenderRequest
                    {
                        ContentKey = contentItem.Key,
                        Culture = culture,
                        Route = route,
                        Force = force,
                    },
                    cancellationToken);
            }
        }
    }

    private static async Task<int> DeleteSnapshotsForUnpublishedCulturesAsync(
        ContentPublishedNotification notification,
        IContent content,
        IRazorSearchSnapshotStore snapshotStore,
        CancellationToken cancellationToken)
    {
        IReadOnlyCollection<RazorSearchSnapshot> snapshots = await snapshotStore.GetByContentKeyAsync(content.Key, cancellationToken);
        int deletedSnapshotCount = 0;

        foreach (RazorSearchSnapshot snapshot in snapshots)
        {
            if (string.IsNullOrWhiteSpace(snapshot.Culture)
                || notification.HasUnpublishedCulture(content, snapshot.Culture) is false)
            {
                continue;
            }

            deletedSnapshotCount += await snapshotStore.DeleteAsync(snapshot.Id, cancellationToken) ? 1 : 0;
        }

        return deletedSnapshotCount;
    }

    private static async Task<int> DeleteSnapshotsForExcludedPublishedVariantsAsync(
        IContent content,
        IUmbracoContextFactory umbracoContextFactory,
        IRazorSearchSnapshotStore snapshotStore,
        IRazorSearchContentFilter contentFilter,
        CancellationToken cancellationToken)
    {
        using UmbracoContextReference contextReference = umbracoContextFactory.EnsureUmbracoContext();
        IPublishedContent? publishedContent = contextReference.UmbracoContext.Content?.GetById(content.Key);
        if (publishedContent is null)
        {
            return 0;
        }

        if (contentFilter.IsExcluded(publishedContent))
        {
            return await snapshotStore.DeleteByContentKeyAsync(content.Key, cancellationToken);
        }

        IReadOnlyCollection<RazorSearchSnapshot> snapshots = await snapshotStore.GetByContentKeyAsync(content.Key, cancellationToken);
        int deletedSnapshotCount = 0;

        foreach (RazorSearchSnapshot snapshot in snapshots)
        {
            if (contentFilter.IsExcluded(publishedContent, snapshot.Culture, snapshot.Segment) is false)
            {
                continue;
            }

            deletedSnapshotCount += await snapshotStore.DeleteAsync(snapshot.Id, cancellationToken) ? 1 : 0;
        }

        return deletedSnapshotCount;
    }

    private static async Task<int> DeleteSnapshotsForUnpublishedCulturesAsync(
        ContentUnpublishedNotification notification,
        IContent content,
        IRazorSearchSnapshotStore snapshotStore,
        CancellationToken cancellationToken)
    {
        IReadOnlyCollection<RazorSearchSnapshot> snapshots = await snapshotStore.GetByContentKeyAsync(content.Key, cancellationToken);
        int deletedSnapshotCount = 0;

        foreach (RazorSearchSnapshot snapshot in snapshots)
        {
            if (string.IsNullOrWhiteSpace(snapshot.Culture)
                || notification.HasUnpublishedCulture(content, snapshot.Culture) is false)
            {
                continue;
            }

            deletedSnapshotCount += await snapshotStore.DeleteAsync(snapshot.Id, cancellationToken) ? 1 : 0;
        }

        return deletedSnapshotCount;
    }

    private static async Task<int> DeleteSnapshotsByContentKeysAsync(
        IRazorSearchSnapshotStore snapshotStore,
        IEnumerable<Guid> contentKeys,
        CancellationToken cancellationToken)
    {
        int deletedSnapshotCount = 0;

        foreach (Guid contentKey in contentKeys.Distinct())
        {
            deletedSnapshotCount += await snapshotStore.DeleteByContentKeyAsync(contentKey, cancellationToken);
        }

        return deletedSnapshotCount;
    }
}

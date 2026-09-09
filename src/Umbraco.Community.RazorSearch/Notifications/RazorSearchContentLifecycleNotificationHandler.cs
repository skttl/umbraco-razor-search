using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Umbraco.Cms.Core;
using Umbraco.Cms.Core.Events;
using Umbraco.Cms.Core.Models;
using Umbraco.Cms.Core.Models.PublishedContent;
using Umbraco.Cms.Core.Notifications;
using Umbraco.Cms.Core.Services;
using Umbraco.Cms.Core.Sync;
using Umbraco.Cms.Core.Web;
using Umbraco.Community.RazorSearch.Models;
using Umbraco.Community.RazorSearch.Persistence.Stores;
using Umbraco.Community.RazorSearch.Searching;
using Umbraco.Community.RazorSearch.Services;
using Umbraco.Extensions;

namespace Umbraco.Community.RazorSearch.Notifications;

internal sealed class RazorSearchContentLifecycleNotificationHandler(
    IServiceScopeFactory serviceScopeFactory,
    IServerRoleAccessor serverRoleAccessor,
    ILogger<RazorSearchContentLifecycleNotificationHandler> logger)
    : INotificationAsyncHandler<ContentPublishedNotification>,
      INotificationAsyncHandler<ContentUnpublishedNotification>,
      INotificationAsyncHandler<ContentDeletedNotification>,
      INotificationAsyncHandler<ContentMovedNotification>,
      INotificationAsyncHandler<ContentMovedToRecycleBinNotification>
{
    public Task HandleAsync(ContentPublishedNotification notification, CancellationToken cancellationToken) =>
        SynchronizeAsync(notification.PublishedEntities, false, cancellationToken);
    public Task HandleAsync(ContentUnpublishedNotification notification, CancellationToken cancellationToken) =>
        SynchronizeAsync(notification.UnpublishedEntities, false, cancellationToken);
    public Task HandleAsync(ContentDeletedNotification notification, CancellationToken cancellationToken) =>
        SynchronizeAsync(notification.DeletedEntities, true, cancellationToken);
    public Task HandleAsync(ContentMovedNotification notification, CancellationToken cancellationToken) =>
        SynchronizeAsync(notification.MoveInfoCollection.Select(x => x.Entity), false, cancellationToken);
    public Task HandleAsync(ContentMovedToRecycleBinNotification notification, CancellationToken cancellationToken) =>
        SynchronizeAsync(notification.MoveInfoCollection.Select(x => x.Entity), true, cancellationToken);

    private async Task SynchronizeAsync(IEnumerable<IContent> roots, bool remove, CancellationToken cancellationToken)
    {
        if (serverRoleAccessor.CurrentServerRole is not (ServerRole.Single or ServerRole.SchedulingPublisher)) return;
        await using var scope = serviceScopeFactory.CreateAsyncScope();
        var services = scope.ServiceProvider;
        var contentService = services.GetRequiredService<IContentService>();
        var store = services.GetRequiredService<IRazorSearchSnapshotStore>();
        var queue = services.GetRequiredService<IRazorSearchRenderQueue>();
        var backgroundQueue = services.GetRequiredService<IBackgroundRazorSearchRenderQueue>();
        var coordinator = services.GetRequiredService<RazorSearchWorkCoordinator>();
        var contextFactory = services.GetRequiredService<IUmbracoContextFactory>();
        var filter = services.GetRequiredService<IRazorSearchContentFilter>();
        // Descendant routes can change when any ancestor is renamed or moved.
        foreach (IContent item in Expand(contentService, roots).DistinctBy(x => x.Key))
        {
            using (await coordinator.EnterAsync(item.Key, cancellationToken)) backgroundQueue.Invalidate(item.Key);
            using var context = contextFactory.EnsureUmbracoContext();
            IPublishedContent? published = context.UmbracoContext.Content?.GetById(item.Key);
            if (remove || published is null || item.Trashed || !item.Published || filter.IsExcludedContentType(published.ContentType.Alias))
            { await store.DeleteByContentKeyAsync(item.Key, cancellationToken); continue; }
            var routes = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            IEnumerable<string?> cultures = RazorSearchPublishedCultures.Get(published);
            foreach (string? culture in cultures)
            {
                string route = published.Url(culture, UrlMode.Absolute);
                if (!filter.IsExcluded(published, culture) && !string.IsNullOrWhiteSpace(route) && !route.StartsWith('#'))
                    routes[culture ?? string.Empty] = route;
            }
            foreach (var snapshot in await store.GetByContentKeyAsync(item.Key, cancellationToken))
                if (!routes.ContainsKey(snapshot.Culture ?? string.Empty)) await store.DeleteAsync(snapshot.Id, cancellationToken);
            foreach (var route in routes)
                await queue.EnqueueAsync(new RazorSearchRenderRequest { ContentKey = item.Key,
                    Culture = route.Key.Length == 0 ? null : route.Key, Route = route.Value }, cancellationToken);
        }
        logger.LogDebug("Synchronized RazorSearch snapshots and queued current published routes after a content lifecycle change.");
    }

    private static IEnumerable<IContent> Expand(IContentService service, IEnumerable<IContent> roots)
    {
        foreach (IContent root in roots)
        {
            yield return root;
            long pageIndex = 0;
            const int pageSize = 128;
            while (true)
            {
                IContent[] page = service.GetPagedDescendants(root.Id, pageIndex, pageSize, out long total,
                    filter: null, ordering: Ordering.By("id", Direction.Ascending)).ToArray();
                foreach (IContent descendant in page) yield return descendant;
                if (page.Length == 0 || ++pageIndex * pageSize >= total) break;
            }
        }
    }
}

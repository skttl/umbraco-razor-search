using System.Collections.Concurrent;
using System.Threading.Channels;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Umbraco.Cms.Core;
using Umbraco.Cms.Core.Actions;
using Umbraco.Cms.Core.Models;
using Umbraco.Cms.Core.Models.PublishedContent;
using Umbraco.Cms.Core.Services;
using Umbraco.Cms.Core.Services.AuthorizationStatus;
using Umbraco.Cms.Core.Sync;
using Umbraco.Cms.Core.Web;
using Umbraco.Community.RazorSearch.Models;
using Umbraco.Community.RazorSearch.Searching;
using Umbraco.Extensions;

namespace Umbraco.Community.RazorSearch.Services;

internal sealed class RazorSearchRebuildQueue(IServiceScopeFactory scopeFactory, IServerRoleAccessor serverRole,
    IRazorSearchQueueActivityNotifier notifier, ILogger<RazorSearchRebuildQueue> logger) : BackgroundService
{
    private readonly Channel<Operation> _operations = Channel.CreateBounded<Operation>(32);
    private readonly ConcurrentDictionary<Guid, RazorSearchRebuildOperationStatus> _statuses = new();

    public Guid Enqueue(Guid? rootKey, bool descendants, Guid userKey)
    {
        if (serverRole.CurrentServerRole is not (ServerRole.Single or ServerRole.SchedulingPublisher))
            throw new InvalidOperationException("Rebuilds must run on the dedicated backoffice server.");
        var operation = new Operation(Guid.NewGuid(), rootKey, descendants, userKey);
        _statuses[operation.Id] = new() { Id = operation.Id, RootContentKey = rootKey, State = "queued", UpdatedAtUtc = DateTimeOffset.UtcNow };
        if (!_operations.Writer.TryWrite(operation))
        { _statuses.TryRemove(operation.Id, out _); throw new InvalidOperationException("The rebuild queue is full. Wait for a running rebuild to finish."); }
        notifier.Publish();
        return operation.Id;
    }

    public IReadOnlyCollection<RazorSearchRebuildOperationStatus> GetStatuses() => _statuses.Values.OrderByDescending(x => x.UpdatedAtUtc).ToArray();

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        await foreach (Operation operation in _operations.Reader.ReadAllAsync(stoppingToken))
        {
            try
            {
                Update(operation.Id, s => s with { State = "discovering" });
                await ProduceAsync(operation, stoppingToken);
                Update(operation.Id, s => s with { State = "queuedAll" });
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested) { break; }
            catch (Exception exception)
            {
                Update(operation.Id, s => s with { State = "failed", ErrorMessage = exception.Message });
                logger.LogError(exception, "RazorSearch rebuild {OperationId} failed during discovery.", operation.Id);
            }
            foreach (var old in _statuses.Values.Where(x => x.State is "queuedAll" or "failed").OrderByDescending(x => x.UpdatedAtUtc).Skip(100))
                _statuses.TryRemove(old.Id, out _);
        }
    }

    private async Task ProduceAsync(Operation operation, CancellationToken cancellationToken)
    {
        using var scope = scopeFactory.CreateScope();
        var services = scope.ServiceProvider;
        var contentService = services.GetRequiredService<IContentService>();
        var userService = services.GetRequiredService<IUserService>();
        var permissions = services.GetRequiredService<IContentPermissionService>();
        var contextFactory = services.GetRequiredService<IUmbracoContextFactory>();
        var queue = services.GetRequiredService<IRazorSearchRenderQueue>();
        var filter = services.GetRequiredService<IRazorSearchContentFilter>();
        IEnumerable<IContent> roots = operation.RootKey is { } key
            ? [contentService.GetById(key) ?? throw new InvalidOperationException("The rebuild root no longer exists.")]
            : contentService.GetRootContent();
        foreach (IContent item in Enumerate(contentService, roots, operation.Descendants))
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (serverRole.CurrentServerRole is not (ServerRole.Single or ServerRole.SchedulingPublisher))
                throw new InvalidOperationException("This server no longer owns RazorSearch rebuilds.");
            Update(operation.Id, s => s with { DiscoveredDocumentCount = s.DiscoveredDocumentCount + 1 });
            // Resolve the actor again, so disabling a user or changing permissions takes effect during a long rebuild.
            var actor = await userService.GetAsync(operation.UserKey);
            if (actor is null || !actor.IsApproved || actor.IsLockedOut
                || await permissions.AuthorizeAccessAsync(actor, [item.Key], new HashSet<string> { ActionPublish.ActionLetter }) != ContentAuthorizationStatus.Success)
            { Update(operation.Id, s => s with { SkippedDocumentCount = s.SkippedDocumentCount + 1 }); continue; }
            using var context = contextFactory.EnsureUmbracoContext();
            IPublishedContent? published = context.UmbracoContext.Content?.GetById(item.Key);
            if (published is null || filter.IsExcludedContentType(published.ContentType.Alias))
            { Update(operation.Id, s => s with { SkippedDocumentCount = s.SkippedDocumentCount + 1 }); continue; }
            IEnumerable<string?> cultures = RazorSearchPublishedCultures.Get(published);
            foreach (string? culture in cultures)
            {
                if (culture is not null && await permissions.AuthorizeCultureAccessAsync(actor, new HashSet<string> { culture }) != ContentAuthorizationStatus.Success) continue;
                string route = published.Url(culture, UrlMode.Absolute);
                if (filter.IsExcluded(published, culture) || string.IsNullOrWhiteSpace(route) || route.StartsWith('#')) continue;
                await queue.EnqueueAsync(new() { ContentKey = item.Key, Culture = culture, Route = route }, cancellationToken);
                Update(operation.Id, s => s with { QueuedRouteCount = s.QueuedRouteCount + 1 });
            }
        }
    }

    internal static IEnumerable<IContent> Enumerate(IContentService service, IEnumerable<IContent> roots, bool descendants)
    {
        const int pageSize = 128;
        foreach (var root in roots)
        {
            yield return root;
            if (!descendants) continue;
            long pageIndex = 0;
            while (true)
            {
                IContent[] page = service.GetPagedDescendants(root.Id, pageIndex, pageSize, out long total,
                    filter: null, ordering: Ordering.By("id", Direction.Ascending)).ToArray();
                foreach (IContent item in page) yield return item;
                if (page.Length == 0 || ++pageIndex * pageSize >= total) break;
            }
        }
    }
    private void Update(Guid id, Func<RazorSearchRebuildOperationStatus, RazorSearchRebuildOperationStatus> change)
    {
        _statuses.AddOrUpdate(id, _ => throw new InvalidOperationException("Unknown rebuild."), (_, status) => change(status) with { UpdatedAtUtc = DateTimeOffset.UtcNow });
        notifier.Publish();
    }
    private sealed record Operation(Guid Id, Guid? RootKey, bool Descendants, Guid UserKey);
}

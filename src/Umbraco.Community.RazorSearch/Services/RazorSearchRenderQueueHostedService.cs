using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using Umbraco.Cms.Core.Models.PublishedContent;
using Umbraco.Cms.Core.Sync;
using Umbraco.Cms.Core.Web;
using Umbraco.Community.RazorSearch.Configuration;
using Umbraco.Community.RazorSearch.Models;
using Umbraco.Community.RazorSearch.Persistence.Models;
using Umbraco.Community.RazorSearch.Persistence.Stores;
using Umbraco.Community.RazorSearch.Rendering;
using Umbraco.Community.RazorSearch.Searching;
using Umbraco.Extensions;

namespace Umbraco.Community.RazorSearch.Services;

internal sealed class RazorSearchRenderQueueHostedService(
    IBackgroundRazorSearchRenderQueue queue,
    IRazorSearchRendererResolver rendererResolver,
    IServiceScopeFactory serviceScopeFactory,
    IUmbracoContextFactory umbracoContextFactory,
    RazorSearchWorkCoordinator coordinator,
    IRazorSearchContentFilter contentFilter,
    IServerRoleAccessor serverRoleAccessor,
    IOptionsMonitor<RazorSearchOptions> options,
    ILogger<RazorSearchRenderQueueHostedService> logger) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            if (serverRoleAccessor.CurrentServerRole is not (ServerRole.Single or ServerRole.SchedulingPublisher))
            {
                await Task.Delay(TimeSpan.FromSeconds(1), stoppingToken);
                continue;
            }
            RazorSearchRenderJob job;
            try { job = await queue.DequeueAsync(stoppingToken); }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested) { break; }
            try { await ProcessAsync(job, stoppingToken); }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            { queue.MarkCancelled(job, "Rendering was cancelled during shutdown."); break; }
            catch (Exception exception)
            {
                queue.MarkFailed(job, exception.Message);
                logger.LogError(exception, "RazorSearch job {JobId} failed while processing or storing its result.", job.Id);
            }
        }
    }

    private async Task ProcessAsync(RazorSearchRenderJob originalJob, CancellationToken cancellationToken)
    {
        RazorSearchOptions settings = options.CurrentValue;
        for (int attempt = 1; attempt <= settings.RenderQueue.MaxAttempts; attempt++)
        {
            RazorSearchRenderJob job;
            using (var context = umbracoContextFactory.EnsureUmbracoContext())
            {
                IPublishedContent? content = context.UmbracoContext.Content?.GetById(originalJob.ContentKey);
                if (!queue.IsCurrent(originalJob) || !IsEligible(content, originalJob.Culture))
                { queue.MarkCancelled(originalJob, "Content is no longer eligible or a newer job is pending."); return; }
                string route = content!.Url(originalJob.Culture, UrlMode.Absolute);
                if (string.IsNullOrWhiteSpace(route) || route.StartsWith('#'))
                { queue.MarkCancelled(originalJob, "Content has no published route."); return; }
                job = originalJob with { Route = route, AttemptCount = attempt };
            }
            queue.MarkRunning(job);
            RazorSearchRenderResult result;
            bool transient;
            try
            {
                result = await rendererResolver.GetRenderer(job.Renderer).RenderAsync(job, cancellationToken);
                transient = result.StatusCode is 408 or 429 or >= 500 and <= 599;
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested) { throw; }
            catch (Exception exception)
            {
                transient = exception is HttpRequestException or TimeoutException or OperationCanceledException;
                result = new() { JobId = job.Id, Success = false, Content = string.Empty,
                    ErrorMessage = exception.Message, CompletedAtUtc = DateTimeOffset.UtcNow };
            }

            using (await coordinator.EnterAsync(job.ContentKey, cancellationToken))
            {
                using var context = umbracoContextFactory.EnsureUmbracoContext();
                IPublishedContent? content = context.UmbracoContext.Content?.GetById(job.ContentKey);
                if (!queue.IsCurrent(job) || !IsEligible(content, job.Culture)
                    || !string.Equals(content!.Url(job.Culture, UrlMode.Absolute), job.Route, StringComparison.Ordinal))
                { queue.MarkCancelled(job, "Content changed during rendering; the result was discarded."); return; }
                using IServiceScope scope = serviceScopeFactory.CreateScope();
                IRazorSearchSnapshotStore store = scope.ServiceProvider.GetRequiredService<IRazorSearchSnapshotStore>();
                RazorSearchSnapshot snapshot;
                if (result.Success)
                {
                    var extracted = RazorSearchSnapshotTextSanitizer.ExtractSnapshotContent(result.Content, job.Route, result.FinalUrl,
                        new RazorSearchSnapshotExtractionContext(content, job.Culture), settings.SnapshotExtraction);
                    snapshot = new() { ContentKey = job.ContentKey, Culture = job.Culture, Route = job.Route, Renderer = job.Renderer,
                        Snapshot = extracted.CombinedText, SnapshotHtml = extracted.SnapshotHtml, Checksum = extracted.ContentHash,
                        FinalUrl = extracted.FinalUrl, TitleText = extracted.TitleText, SummaryText = extracted.SummaryText,
                        HeadingText = extracted.HeadingText, BodyText = extracted.BodyText, ContentHash = extracted.ContentHash,
                        RenderStatus = RazorSearchSnapshotStatuses.Success, RenderedAtUtc = result.CompletedAtUtc,
                        LastAttemptAtUtc = result.CompletedAtUtc, UpdatedAtUtc = result.CompletedAtUtc };
                }
                else
                {
                    RazorSearchSnapshot? existing = (await store.GetByContentKeyAsync(job.ContentKey, cancellationToken))
                        .SingleOrDefault(x => string.Equals(x.Culture ?? string.Empty, job.Culture ?? string.Empty, StringComparison.OrdinalIgnoreCase));
                    snapshot = PreserveSnapshotOnFailure(existing, job, result);
                }
                await store.UpsertAsync(snapshot, cancellationToken);
            }
            if (result.Success) { queue.MarkCompleted(job, result); return; }
            if (!transient || attempt == settings.RenderQueue.MaxAttempts)
            { queue.MarkFailed(job, result.ErrorMessage ?? "Rendering failed."); return; }
            await Task.Delay(settings.RenderQueue.RetryDelay, cancellationToken);
        }
    }

    internal static RazorSearchSnapshot PreserveSnapshotOnFailure(RazorSearchSnapshot? existing, RazorSearchRenderJob job, RazorSearchRenderResult result)
    {
        existing ??= new() { ContentKey = job.ContentKey, Culture = job.Culture, Route = job.Route,
            Renderer = job.Renderer, Snapshot = string.Empty, RenderStatus = RazorSearchSnapshotStatuses.Failed };
        return existing with { LastRenderError = result.ErrorMessage ?? "The renderer returned an unsuccessful result.",
            LastAttemptAtUtc = result.CompletedAtUtc, UpdatedAtUtc = result.CompletedAtUtc };
    }

    private bool IsEligible(IPublishedContent? content, string? culture) => content is not null
        && RazorSearchPublishedCultures.Get(content).Contains(culture, StringComparer.OrdinalIgnoreCase)
        && !contentFilter.IsExcluded(content, culture);
}

using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.DependencyInjection;
using Umbraco.Cms.Core.Web;
using Umbraco.Community.RazorSearch.Models;
using Umbraco.Community.RazorSearch.Persistence.Models;
using Umbraco.Community.RazorSearch.Persistence.Stores;
using Umbraco.Community.RazorSearch.Rendering;

namespace Umbraco.Community.RazorSearch.Services;

internal sealed class RazorSearchRenderQueueHostedService(
    IBackgroundRazorSearchRenderQueue queue,
    IRazorSearchRendererResolver rendererResolver,
    IServiceScopeFactory serviceScopeFactory,
    IUmbracoContextFactory umbracoContextFactory,
    ILogger<RazorSearchRenderQueueHostedService> logger) : BackgroundService
{
    private readonly IBackgroundRazorSearchRenderQueue _queue = queue;
    private readonly IRazorSearchRendererResolver _rendererResolver = rendererResolver;
    private readonly IServiceScopeFactory _serviceScopeFactory = serviceScopeFactory;
    private readonly IUmbracoContextFactory _umbracoContextFactory = umbracoContextFactory;
    private readonly ILogger<RazorSearchRenderQueueHostedService> _logger = logger;

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (stoppingToken.IsCancellationRequested is false)
        {
            RazorSearchRenderJob job;

            try
            {
                job = await _queue.DequeueAsync(stoppingToken);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }

            _queue.MarkRunning(job);

            try
            {
                using IServiceScope scope = _serviceScopeFactory.CreateScope();
                IRazorSearchSnapshotStore snapshotStore = scope.ServiceProvider.GetRequiredService<IRazorSearchSnapshotStore>();
                IRazorSearchRenderer renderer = _rendererResolver.GetRenderer(job.Renderer);
                RazorSearchRenderResult result = await renderer.RenderAsync(job, stoppingToken);

                if (result.Success)
                {
                    using var contextReference = _umbracoContextFactory.EnsureUmbracoContext();
                    RazorSearchSnapshotContent snapshotContent =
                        RazorSearchSnapshotTextSanitizer.ExtractSnapshotContent(
                            result.Content,
                            job.Route,
                            result.FinalUrl,
                            new RazorSearchSnapshotExtractionContext(
                                contextReference.UmbracoContext.Content?.GetById(job.ContentKey),
                                job.Culture,
                                job.Segment));

                    RazorSearchSnapshot snapshot = new()
                    {
                        ContentKey = job.ContentKey,
                        Route = job.Route,
                        Culture = job.Culture,
                        Segment = job.Segment,
                        Renderer = job.Renderer,
                        Snapshot = snapshotContent.CombinedText,
                        Checksum = snapshotContent.ContentHash,
                        FinalUrl = snapshotContent.FinalUrl,
                        TitleText = snapshotContent.TitleText,
                        SummaryText = snapshotContent.SummaryText,
                        HeadingText = snapshotContent.HeadingText,
                        BodyText = snapshotContent.BodyText,
                        ContentHash = snapshotContent.ContentHash,
                        RenderStatus = RazorSearchSnapshotStatuses.Success,
                        LastRenderError = null,
                        RenderedAtUtc = result.CompletedAtUtc,
                        UpdatedAtUtc = result.CompletedAtUtc,
                    };

                    await snapshotStore.UpsertAsync(snapshot, stoppingToken);
                    _queue.MarkCompleted(job, result);

                    _logger.LogDebug(
                        "RazorSearch render job {JobId} for content {ContentKey} completed successfully.",
                        job.Id,
                        job.ContentKey);
                }
                else
                {
                    string errorMessage = result.ErrorMessage ?? "The renderer returned an unsuccessful result.";
                    RazorSearchSnapshot? existingSnapshot = (await snapshotStore.GetByContentKeyAsync(job.ContentKey, stoppingToken))
                        .FirstOrDefault(x =>
                            string.Equals(x.Culture, job.Culture, StringComparison.OrdinalIgnoreCase)
                            && string.Equals(x.Route, job.Route, StringComparison.OrdinalIgnoreCase));

                    RazorSearchSnapshot failedSnapshot = new()
                    {
                        Id = existingSnapshot?.Id ?? Guid.Empty,
                        ContentKey = job.ContentKey,
                        Route = job.Route,
                        Culture = job.Culture,
                        Segment = job.Segment,
                        Renderer = job.Renderer,
                        Snapshot = existingSnapshot?.Snapshot ?? string.Empty,
                        Checksum = existingSnapshot?.Checksum,
                        FinalUrl = existingSnapshot?.FinalUrl,
                        TitleText = existingSnapshot?.TitleText,
                        SummaryText = existingSnapshot?.SummaryText,
                        HeadingText = existingSnapshot?.HeadingText,
                        BodyText = existingSnapshot?.BodyText,
                        ContentHash = existingSnapshot?.ContentHash,
                        RenderStatus = RazorSearchSnapshotStatuses.Failed,
                        LastRenderError = errorMessage,
                        RenderedAtUtc = result.CompletedAtUtc,
                        CreatedAtUtc = existingSnapshot?.CreatedAtUtc ?? result.CompletedAtUtc,
                        UpdatedAtUtc = result.CompletedAtUtc,
                    };

                    await snapshotStore.UpsertAsync(failedSnapshot, stoppingToken);
                    _queue.MarkFailed(job, errorMessage);
                    _logger.LogWarning(
                        "RazorSearch render job {JobId} for content {ContentKey} failed: {ErrorMessage}",
                        job.Id,
                        job.ContentKey,
                        errorMessage);
                }
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                _queue.MarkCancelled(job, "Rendering was cancelled during shutdown.");
                break;
            }
            catch (Exception exception)
            {
                _queue.MarkFailed(job, exception.Message);
                _logger.LogError(
                    exception,
                    "RazorSearch render job {JobId} for content {ContentKey} failed unexpectedly.",
                    job.Id,
                    job.ContentKey);
            }
        }
    }
}

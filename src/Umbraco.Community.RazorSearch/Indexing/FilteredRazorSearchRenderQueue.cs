using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Umbraco.Cms.Core.Web;
using Umbraco.Community.RazorSearch.Configuration;
using Umbraco.Community.RazorSearch.Models;
using Umbraco.Community.RazorSearch.Searching;
using Umbraco.Community.RazorSearch.Services;

namespace Umbraco.Community.RazorSearch.Indexing;

internal sealed class FilteredRazorSearchRenderQueue(
    InMemoryRazorSearchRenderQueue innerQueue,
    IUmbracoContextFactory umbracoContextFactory,
    IRazorSearchContentFilter contentFilter,
    IOptionsMonitor<RazorSearchOptions> optionsMonitor,
    ILogger<FilteredRazorSearchRenderQueue> logger) : IRazorSearchRenderQueue
{
    public ValueTask<RazorSearchRenderEnqueueResult> EnqueueAsync(
        RazorSearchRenderRequest request,
        CancellationToken cancellationToken = default)
    {
        using var contextReference = umbracoContextFactory.EnsureUmbracoContext();
        if (contextReference.UmbracoContext.Content?.GetById(request.ContentKey) is { } publishedContent
            && contentFilter.IsExcluded(publishedContent, request.Culture))
        {
            DateTimeOffset now = DateTimeOffset.UtcNow;
            string renderer = string.IsNullOrWhiteSpace(request.Renderer)
                ? optionsMonitor.CurrentValue.DefaultRenderer
                : request.Renderer;
            Guid jobId = Guid.NewGuid();

            logger.LogDebug(
                "Skipped RazorSearch render enqueue for content {ContentKey} and culture {Culture} because the content is excluded from search.",
                request.ContentKey,
                request.Culture ?? "<invariant>");

            return ValueTask.FromResult(new RazorSearchRenderEnqueueResult
            {
                Job = new RazorSearchRenderJob
                {
                    Id = jobId,
                    ContentKey = request.ContentKey,
                    Route = request.Route,
                    Culture = request.Culture,
                    Renderer = renderer,
                    EnqueuedAtUtc = now,
                },
                Status = new RazorSearchRenderJobStatus
                {
                    JobId = jobId,
                    BatchId = 0,
                    ContentKey = request.ContentKey,
                    Route = request.Route,
                    Culture = request.Culture,
                    Renderer = renderer,
                    State = RazorSearchRenderJobState.Cancelled,
                    EnqueuedAtUtc = now,
                    UpdatedAtUtc = now,
                    CompletedAtUtc = now,
                    IsDuplicate = true,
                    ErrorMessage = "RazorSearch skipped this route because the content is excluded from search.",
                },
            });
        }

        return innerQueue.EnqueueAsync(request, cancellationToken);
    }

    public bool TryGetStatus(Guid jobId, out RazorSearchRenderJobStatus? status) => innerQueue.TryGetStatus(jobId, out status);

    public IReadOnlyCollection<RazorSearchRenderJobStatus> GetStatuses(Guid contentKey) => innerQueue.GetStatuses(contentKey);

    public IReadOnlyCollection<RazorSearchRenderJobStatus> GetAllStatuses() => innerQueue.GetAllStatuses();
}

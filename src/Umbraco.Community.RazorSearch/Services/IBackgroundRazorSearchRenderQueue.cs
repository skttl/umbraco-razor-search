using Umbraco.Community.RazorSearch.Models;

namespace Umbraco.Community.RazorSearch.Services;

internal interface IBackgroundRazorSearchRenderQueue
{
    ValueTask<RazorSearchRenderJob> DequeueAsync(CancellationToken cancellationToken);

    bool IsCurrent(RazorSearchRenderJob job);

    void Invalidate(Guid contentKey, string? culture = null, bool allCultures = true);

    void MarkRunning(RazorSearchRenderJob job);

    void MarkCompleted(RazorSearchRenderJob job, RazorSearchRenderResult result);

    void MarkFailed(RazorSearchRenderJob job, string errorMessage);

    void MarkCancelled(RazorSearchRenderJob job, string? errorMessage = null);
}

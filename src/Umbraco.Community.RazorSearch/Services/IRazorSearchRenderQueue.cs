using Umbraco.Community.RazorSearch.Models;

namespace Umbraco.Community.RazorSearch.Services;

public interface IRazorSearchRenderQueue
{
    ValueTask<RazorSearchRenderEnqueueResult> EnqueueAsync(
        RazorSearchRenderRequest request,
        CancellationToken cancellationToken = default);

    bool TryGetStatus(Guid jobId, out RazorSearchRenderJobStatus? status);

    IReadOnlyCollection<RazorSearchRenderJobStatus> GetStatuses(Guid contentKey);

    IReadOnlyCollection<RazorSearchRenderJobStatus> GetAllStatuses();
}

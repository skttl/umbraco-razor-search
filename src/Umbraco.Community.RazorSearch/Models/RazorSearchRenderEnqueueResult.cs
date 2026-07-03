namespace Umbraco.Community.RazorSearch.Models;

public sealed record RazorSearchRenderEnqueueResult
{
    public required RazorSearchRenderJob Job { get; init; }

    public required RazorSearchRenderJobStatus Status { get; init; }

    public bool WasQueued => Status.IsDuplicate is false;
}

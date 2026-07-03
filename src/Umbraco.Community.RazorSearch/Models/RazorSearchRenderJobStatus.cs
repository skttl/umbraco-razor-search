namespace Umbraco.Community.RazorSearch.Models;

public sealed record RazorSearchRenderJobStatus
{
    public required Guid JobId { get; init; }

    public long BatchId { get; init; }

    public required Guid ContentKey { get; init; }

    public required string Route { get; init; }

    public required string Renderer { get; init; }

    public required RazorSearchRenderJobState State { get; init; }

    public string? Culture { get; init; }

    public string? Segment { get; init; }

    public required DateTimeOffset EnqueuedAtUtc { get; init; }

    public required DateTimeOffset UpdatedAtUtc { get; init; }

    public DateTimeOffset? StartedAtUtc { get; init; }

    public DateTimeOffset? CompletedAtUtc { get; init; }

    public int AttemptCount { get; init; } = 1;

    public bool IsDuplicate { get; init; }

    public string? ErrorMessage { get; init; }
}

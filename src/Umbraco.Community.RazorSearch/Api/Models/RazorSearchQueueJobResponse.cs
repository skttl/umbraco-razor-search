namespace Umbraco.Community.RazorSearch.Api.Models;

public sealed class RazorSearchQueueJobResponse
{
    public Guid DocumentId { get; init; }

    public string? DocumentName { get; init; }

    public string State { get; init; } = "queued";

    public string Route { get; init; } = string.Empty;

    public string Renderer { get; init; } = string.Empty;

    public string? Culture { get; init; }

    public string? Segment { get; init; }

    public DateTimeOffset EnqueuedAt { get; init; }

    public DateTimeOffset UpdatedAt { get; init; }

    public DateTimeOffset? StartedAt { get; init; }

    public DateTimeOffset? CompletedAt { get; init; }

    public string? ErrorMessage { get; init; }
}

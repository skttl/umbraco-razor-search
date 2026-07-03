namespace Umbraco.Community.RazorSearch.Models;

public sealed record RazorSearchRenderJob
{
    public required Guid Id { get; init; }

    public required Guid ContentKey { get; init; }

    public required string Route { get; init; }

    public required string Renderer { get; init; }

    public string? Culture { get; init; }

    public string? Segment { get; init; }

    public DateTimeOffset EnqueuedAtUtc { get; init; }

    public int AttemptCount { get; init; } = 1;
}

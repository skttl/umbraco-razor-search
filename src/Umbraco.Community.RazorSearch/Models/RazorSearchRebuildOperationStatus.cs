namespace Umbraco.Community.RazorSearch.Models;

/// <summary>Progress of an in-memory rebuild producer. QueuedAll means discovery finished; render jobs can still be pending.</summary>
public sealed record RazorSearchRebuildOperationStatus
{
    public Guid Id { get; init; }
    public Guid? RootContentKey { get; init; }
    public required string State { get; init; }
    public int DiscoveredDocumentCount { get; init; }
    public int QueuedRouteCount { get; init; }
    public int SkippedDocumentCount { get; init; }
    public string? ErrorMessage { get; init; }
    public DateTimeOffset UpdatedAtUtc { get; init; }
}

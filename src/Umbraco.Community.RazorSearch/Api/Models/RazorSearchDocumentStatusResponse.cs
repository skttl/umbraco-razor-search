namespace Umbraco.Community.RazorSearch.Api.Models;

public sealed record RazorSearchDocumentStatusResponse
{
    public Guid DocumentId { get; init; }

    public string? DocumentName { get; init; }

    public string State { get; init; } = "unknown";

    public bool IncludeDescendants { get; init; }

    public int PendingDocumentCount { get; init; }

    public int CompletedDocumentCount { get; init; }

    public int FailedDocumentCount { get; init; }

    public DateTimeOffset UpdatedAt { get; init; }

    public string? Message { get; init; }

    public IReadOnlyCollection<RazorSearchQueueJobResponse> Jobs { get; init; } = [];

    public IReadOnlyCollection<RazorSearchDocumentSnapshotResponse> Snapshots { get; init; } = [];

    public IReadOnlyCollection<RazorSearchDocumentIndexEntryResponse> ExpectedIndexEntries { get; init; } = [];
}

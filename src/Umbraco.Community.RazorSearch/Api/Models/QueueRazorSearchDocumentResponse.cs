namespace Umbraco.Community.RazorSearch.Api.Models;

public sealed record QueueRazorSearchDocumentResponse
{
    public Guid OperationId { get; init; }

    public Guid DocumentId { get; init; }

    public string Scope { get; init; } = "document";

    public bool IncludeDescendants { get; init; }

    public string State { get; init; } = "queued";

    public int DiscoveredDocumentCount { get; init; }

    public int ProcessedDocumentCount { get; init; }

    public int QueuedRouteCount { get; init; }

    public int DuplicateRouteCount { get; init; }

    public int SkippedDocumentCount { get; init; }

    public DateTimeOffset QueuedAt { get; init; }

    public string Message { get; init; } = string.Empty;

    public RazorSearchDocumentStatusResponse Status { get; init; } = new();
}

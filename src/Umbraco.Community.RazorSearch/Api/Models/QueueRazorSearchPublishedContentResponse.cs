namespace Umbraco.Community.RazorSearch.Api.Models;

public sealed class QueueRazorSearchPublishedContentResponse
{
    public string Scope { get; init; } = "allPublishedContent";

    public string State { get; init; } = "queued";

    public int MaxDocumentCount { get; init; }

    public int DiscoveredDocumentCount { get; init; }

    public int ProcessedDocumentCount { get; init; }

    public int QueuedRouteCount { get; init; }

    public int DuplicateRouteCount { get; init; }

    public int SkippedDocumentCount { get; init; }

    public bool WasTruncated { get; init; }

    public DateTimeOffset QueuedAt { get; init; }

    public string Message { get; init; } = string.Empty;
}

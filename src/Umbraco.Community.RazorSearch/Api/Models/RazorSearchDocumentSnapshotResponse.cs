namespace Umbraco.Community.RazorSearch.Api.Models;

public sealed record RazorSearchDocumentSnapshotResponse
{
    public string Route { get; init; } = string.Empty;

    public string? FinalUrl { get; init; }

    public string Renderer { get; init; } = string.Empty;

    public string? Culture { get; init; }

    public string State { get; init; } = "unknown";

    public string Snapshot { get; init; } = string.Empty;

    public string? SnapshotHtml { get; init; }

    public string? TitleText { get; init; }

    public string? SummaryText { get; init; }

    public string? HeadingText { get; init; }

    public string? BodyText { get; init; }

    public string? ErrorMessage { get; init; }

    public DateTimeOffset? LastAttemptAt { get; init; }

    public DateTimeOffset? RenderedAt { get; init; }

    public DateTimeOffset UpdatedAt { get; init; }
}

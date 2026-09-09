namespace Umbraco.Community.RazorSearch.Persistence.Models;

public sealed record RazorSearchSnapshot
{
    public Guid Id { get; init; }

    public required Guid ContentKey { get; init; }

    public required string Route { get; init; }

    public string? Culture { get; init; }

    public required string Renderer { get; init; }

    public required string Snapshot { get; init; }

    public string? SnapshotHtml { get; init; }

    public string? Checksum { get; init; }

    public string? FinalUrl { get; init; }

    public string? TitleText { get; init; }

    public string? SummaryText { get; init; }

    public string? HeadingText { get; init; }

    public string? BodyText { get; init; }

    public string? ContentHash { get; init; }

    public string RenderStatus { get; init; } = RazorSearchSnapshotStatuses.Success;

    public string? LastRenderError { get; init; }

    public DateTimeOffset? LastAttemptAtUtc { get; init; }

    public DateTimeOffset? RenderedAtUtc { get; init; }

    public DateTimeOffset CreatedAtUtc { get; init; }

    public DateTimeOffset UpdatedAtUtc { get; init; }
}

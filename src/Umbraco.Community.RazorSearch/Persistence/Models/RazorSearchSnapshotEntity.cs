namespace Umbraco.Community.RazorSearch.Persistence.Models;

internal sealed class RazorSearchSnapshotEntity
{
    public Guid Id { get; set; }

    public Guid ContentKey { get; set; }

    public string Route { get; set; } = string.Empty;

    public string Culture { get; set; } = string.Empty;

    public string Segment { get; set; } = string.Empty;

    public string Renderer { get; set; } = string.Empty;

    public string Snapshot { get; set; } = string.Empty;

    public string? Checksum { get; set; }

    public string? FinalUrl { get; set; }

    public string? TitleText { get; set; }

    public string? SummaryText { get; set; }

    public string? HeadingText { get; set; }

    public string? BodyText { get; set; }

    public string? ContentHash { get; set; }

    public string RenderStatus { get; set; } = RazorSearchSnapshotStatuses.Success;

    public string? LastRenderError { get; set; }

    public DateTimeOffset? RenderedAtUtc { get; set; }

    public DateTimeOffset CreatedAtUtc { get; set; }

    public DateTimeOffset UpdatedAtUtc { get; set; }
}

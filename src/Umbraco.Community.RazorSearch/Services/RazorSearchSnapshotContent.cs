namespace Umbraco.Community.RazorSearch.Services;

internal sealed record RazorSearchSnapshotContent
{
    public required string TitleText { get; init; }

    public required string SummaryText { get; init; }

    public required string HeadingText { get; init; }

    public required string BodyText { get; init; }

    public required string CombinedText { get; init; }

    public required string SnapshotHtml { get; init; }

    public required string FinalUrl { get; init; }

    public required string ContentHash { get; init; }
}

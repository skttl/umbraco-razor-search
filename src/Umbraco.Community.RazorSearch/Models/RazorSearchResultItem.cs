using Umbraco.Cms.Core.Models.PublishedContent;

namespace Umbraco.Community.RazorSearch.Models;

public sealed class RazorSearchResultItem : IRazorSearchResultItem
{
    public required Guid ContentKey { get; init; }

    public required IPublishedContent? Content { get; init; }

    public required string? Url { get; init; }

    public required string Title { get; init; }

    public required string SummaryHtml { get; init; }
}

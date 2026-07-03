using Umbraco.Cms.Core.Models.PublishedContent;

namespace Umbraco.Community.RazorSearch.Models;

public interface IRazorSearchResultItem
{
    Guid ContentKey { get; }

    IPublishedContent? Content { get; }

    string? Url { get; }

    string Title { get; }

    string SummaryHtml { get; }
}

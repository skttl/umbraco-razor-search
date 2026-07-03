using Umbraco.Cms.Core.Models;
using Umbraco.Cms.Core.Models.PublishedContent;

namespace Umbraco.Community.RazorSearch.Searching;

public interface IRazorSearchContentFilter
{
    bool IsExcludedContentType(string? contentTypeAlias);

    bool IsExcluded(IPublishedContent content, string? culture = null, string? segment = null);

    bool IsExcluded(IContentBase content, string? culture = null, string? segment = null, bool published = true);
}

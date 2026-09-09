using Umbraco.Cms.Core.Models;
using Umbraco.Cms.Core.Models.PublishedContent;

namespace Umbraco.Community.RazorSearch.Services;

internal static class RazorSearchPublishedCultures
{
    public static IEnumerable<string?> Get(IPublishedContent content) =>
        content.ContentType.Variations.HasFlag(ContentVariation.Culture)
            ? content.Cultures.Keys.Where(x => !string.IsNullOrWhiteSpace(x)).Cast<string?>()
            : [null];
}

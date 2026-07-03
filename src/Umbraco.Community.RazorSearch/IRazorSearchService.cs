namespace Umbraco.Community.RazorSearch;

public interface IRazorSearchService
{
    Task<Models.IRazorSearchResult> SearchAsync(Models.RazorSearch search, CancellationToken cancellationToken = default);
}

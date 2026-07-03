namespace Umbraco.Community.RazorSearch.Models;

public interface IRazorSearchResult
{
    long Total { get; }

    int Skip { get; }

    int Take { get; }

    IReadOnlyCollection<IRazorSearchResultItem> Items { get; }
}

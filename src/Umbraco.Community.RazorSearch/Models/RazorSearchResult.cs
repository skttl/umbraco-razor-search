namespace Umbraco.Community.RazorSearch.Models;

public sealed class RazorSearchResult : IRazorSearchResult
{
    public required long Total { get; init; }

    public required int Skip { get; init; }

    public required int Take { get; init; }

    public required IReadOnlyCollection<IRazorSearchResultItem> Items { get; init; }
}

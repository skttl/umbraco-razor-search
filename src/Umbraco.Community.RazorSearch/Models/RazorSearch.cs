namespace Umbraco.Community.RazorSearch.Models;

public sealed class RazorSearch
{
    private readonly HashSet<Guid> _rootKeys = [];
    private readonly HashSet<string> _includedContentTypeAliases = new(StringComparer.OrdinalIgnoreCase);
    private readonly HashSet<string> _excludedContentTypeAliases = new(StringComparer.OrdinalIgnoreCase);

    public RazorSearch(string text) => Text = text;

    public string Text { get; }

    public string? Culture { get; private set; }

    public string? Segment { get; set; }

    public int Skip { get; private set; }

    public int Take { get; private set; } = 10;

    public int? PageNumber { get; private set; }

    public int? PageSize { get; private set; }

    public IReadOnlyCollection<Guid> RootKeys => _rootKeys;

    public IReadOnlyCollection<string> IncludedContentTypeAliases => _includedContentTypeAliases;

    public IReadOnlyCollection<string> ExcludedContentTypeAliases => _excludedContentTypeAliases;

    public RazorSearch UnderRoot(Guid rootKey)
    {
        _rootKeys.Add(rootKey);
        return this;
    }

    public RazorSearch UnderRoots(IEnumerable<Guid> rootKeys)
    {
        foreach (Guid rootKey in rootKeys)
        {
            _rootKeys.Add(rootKey);
        }

        return this;
    }

    public RazorSearch IncludeContentType(string contentTypeAlias)
    {
        _includedContentTypeAliases.Add(contentTypeAlias);
        return this;
    }

    public RazorSearch IncludeContentTypes(params string[] contentTypeAliases)
    {
        foreach (string contentTypeAlias in contentTypeAliases)
        {
            _includedContentTypeAliases.Add(contentTypeAlias);
        }

        return this;
    }

    public RazorSearch IncludeContentTypes(IEnumerable<string> contentTypeAliases)
    {
        foreach (string contentTypeAlias in contentTypeAliases)
        {
            _includedContentTypeAliases.Add(contentTypeAlias);
        }

        return this;
    }

    public RazorSearch ExcludeContentType(string contentTypeAlias)
    {
        _excludedContentTypeAliases.Add(contentTypeAlias);
        return this;
    }

    public RazorSearch ExcludeContentTypes(params string[] contentTypeAliases)
    {
        foreach (string contentTypeAlias in contentTypeAliases)
        {
            _excludedContentTypeAliases.Add(contentTypeAlias);
        }

        return this;
    }

    public RazorSearch ExcludeContentTypes(IEnumerable<string> contentTypeAliases)
    {
        foreach (string contentTypeAlias in contentTypeAliases)
        {
            _excludedContentTypeAliases.Add(contentTypeAlias);
        }

        return this;
    }

    public RazorSearch InCulture(string culture)
    {
        Culture = culture;
        return this;
    }

    public RazorSearch Page(int pageNumber, int pageSize)
    {
        PageNumber = pageNumber;
        PageSize = pageSize;
        Skip = Math.Max(0, pageNumber - 1) * pageSize;
        Take = pageSize;
        return this;
    }

    public RazorSearch SkipTake(int skip, int take)
    {
        PageNumber = null;
        PageSize = null;
        Skip = skip;
        Take = take;
        return this;
    }
}

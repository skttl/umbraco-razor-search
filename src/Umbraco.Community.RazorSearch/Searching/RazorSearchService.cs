using Umbraco.Cms.Core.Services;
using Microsoft.Extensions.Options;
using Umbraco.Cms.Core;
using Umbraco.Cms.Core.Models;
using Umbraco.Cms.Core.Models.PublishedContent;
using Umbraco.Cms.Core.PublishedCache;
using Umbraco.Cms.Core.Routing;
using Umbraco.Cms.Core.Web;
using Umbraco.Cms.Search.Core.Extensions;
using Umbraco.Cms.Search.Core.Models.Searching;
using Umbraco.Cms.Search.Core.Models.Searching.Filtering;
using Umbraco.Cms.Search.Core.Models.Searching.Sorting;
using Umbraco.Cms.Search.Core.Services;
using Umbraco.Community.RazorSearch.Configuration;
using Umbraco.Community.RazorSearch.Models;
using Umbraco.Community.RazorSearch.Persistence.Models;
using Umbraco.Community.RazorSearch.Persistence.Stores;

namespace Umbraco.Community.RazorSearch.Searching;

public sealed class RazorSearchService(
    IRazorSearchSnapshotStore snapshotStore,
    ISearcherResolver searcherResolver,
    IUmbracoContextFactory umbracoContextFactory,
    IOptions<RazorSearchOptions> razorSearchOptions,
    ILanguageService languageService,
    IPublishedUrlProvider publishedUrlProvider
) : IRazorSearchService
{
    public async Task<IRazorSearchResult> SearchAsync(
        Models.RazorSearch search,
        CancellationToken cancellationToken = default
    )
    {
        RazorSearchRequestValidator.Validate(search);
        string? culture = RazorSearchRequestValidator.ResolveCulture(search.Culture,
            (await languageService.GetAllAsync()).Select(language => language.IsoCode));

        string[] terms = Tokenize(search.Text);

        using var contextReference = umbracoContextFactory.EnsureUmbracoContext();
        IPublishedContentCache? contentCache = contextReference.UmbracoContext.Content;
        if (contentCache is null)
        {
            return EmptyResult(search);
        }

        ISearcher searcher = searcherResolver.GetRequiredSearcher(
            Constants.InternalIndex.Alias
        );

        Filter[] filters = CreateMetadataFilters(search);

        SearchResult searchResult = await searcher.SearchAsync(
            Constants.InternalIndex.Alias,
            search.Text,
            filters,
            [],
            [new ScoreSorter(Direction.Descending)],
            culture,
            null,
            CreateAccessContext(),
            search.Skip,
            search.Take,
            0
        );

        Document[] documents = searchResult.Documents.ToArray();
        if (documents.Length == 0)
        {
            // Some providers report zero total when Skip falls beyond the final hit.
            // Read the first hit with identical constraints to retain the actual filtered total.
            if (search.Skip > 0 && searchResult.Total == 0)
            {
                SearchResult firstPage = await searcher.SearchAsync(
                    Constants.InternalIndex.Alias, search.Text, filters, [],
                    [new ScoreSorter(Direction.Descending)], culture, null, CreateAccessContext(), 0, 1, 0);
                return EmptyResult(search, firstPage.Total);
            }
            return EmptyResult(search, searchResult.Total);
        }

        SearchCandidate[] candidates = await BuildCandidatesAsync(
            contentCache,
            culture,
            documents,
            cancellationToken
        );

        IRazorSearchResultItem[] items = candidates
            .Select(x => new RazorSearchResultItem
            {
                ContentKey = x.ContentKey,
                Content = x.Content,
                Url = publishedUrlProvider.GetUrl(x.Content, UrlMode.Absolute,
                    string.IsNullOrEmpty(x.Snapshot.Culture) ? null : culture),
                Title = ResolveTitle(x),
                SummaryHtml = RazorSearchSummaryBuilder.Build(
                    ResolveSummarySource(x.Snapshot),
                    terms,
                    razorSearchOptions.Value.HighlightPattern
                ),
            })
            .Cast<IRazorSearchResultItem>()
            .ToArray();

        IRazorSearchResult result = new RazorSearchResult
        {
            Total = searchResult.Total,
            Skip = search.Skip,
            Take = search.Take,
            Items = items,
        };

        return result;
    }

    private static IRazorSearchResult EmptyResult(Models.RazorSearch search, long total = 0) =>
        new RazorSearchResult
        {
            Total = total,
            Skip = search.Skip,
            Take = search.Take,
            Items = [],
        };

    private async Task<SearchCandidate[]> BuildCandidatesAsync(
        IPublishedContentCache contentCache,
        string? culture,
        IReadOnlyCollection<Document> documents,
        CancellationToken cancellationToken
    )
    {
        Guid[] documentIds = documents
            .Where(x => x.ObjectType == UmbracoObjectTypes.Document)
            .Select(x => x.Id)
            .Distinct()
            .ToArray();

        if (documentIds.Length == 0)
        {
            return [];
        }

        IReadOnlyCollection<RazorSearchSnapshot> snapshots = await snapshotStore.GetByContentKeysAsync(
            documentIds,
            cancellationToken
        );
        Dictionary<Guid, RazorSearchSnapshot[]> snapshotsByContentKey = snapshots
            .GroupBy(x => x.ContentKey)
            .ToDictionary(x => x.Key, x => x.ToArray());

        return documentIds
            .Select(documentId =>
                snapshotsByContentKey.TryGetValue(documentId, out RazorSearchSnapshot[]? contentSnapshots)
                    ? CreateCandidate(contentSnapshots, contentCache, culture, documentId)
                    : null)
            .Where(x => x is not null)
            .Select(x => x!)
            .ToArray();
    }

    private static SearchCandidate? CreateCandidate(
        IReadOnlyCollection<RazorSearchSnapshot> snapshots,
        IPublishedContentCache contentCache,
        string? culture,
        Guid contentKey
    )
    {
        IPublishedContent? content = contentCache.GetById(contentKey);
        if (content is null)
        {
            return null;
        }

        RazorSearchSnapshot? snapshot = SelectSnapshot(snapshots, culture);
        if (snapshot is null)
        {
            return null;
        }

        return new SearchCandidate(contentKey, content, snapshot);
    }

    private Filter[] CreateMetadataFilters(Models.RazorSearch search)
    {
        var filters = new List<Filter>();

        string[] rootKeyValues = search.RootKeys.Select(x => x.AsKeyword()).ToArray();
        if (rootKeyValues.Length > 0)
        {
            filters.Add(
                new KeywordFilter(
                    Umbraco.Cms.Search.Core.Constants.FieldNames.PathIds,
                    rootKeyValues,
                    false
                )
            );
        }

        string[] includedAliases = search.IncludedContentTypeAliases
            .Where(x => string.IsNullOrWhiteSpace(x) is false)
            .Select(x => x.Trim())
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToArray();
        if (includedAliases.Length > 0)
        {
            filters.Add(
                new KeywordFilter(
                    Constants.InternalIndex.ContentTypeAliasFieldName,
                    includedAliases,
                    false
                )
            );
        }

        string[] excludedAliases = GetEffectiveExcludedAliases(search).ToArray();
        if (excludedAliases.Length > 0)
        {
            filters.Add(
                new KeywordFilter(
                    Constants.InternalIndex.ContentTypeAliasFieldName,
                    excludedAliases,
                    true
                )
            );
        }

        AddExcludeFromSearchFilters(filters);

        return filters.ToArray();
    }

    private HashSet<string> GetEffectiveExcludedAliases(Models.RazorSearch search)
    {
        HashSet<string> excludedAliases = search.ExcludedContentTypeAliases.ToHashSet(
            StringComparer.OrdinalIgnoreCase
        );

        foreach (string alias in razorSearchOptions.Value.ExcludedContentTypeAliases)
        {
            if (string.IsNullOrWhiteSpace(alias))
            {
                continue;
            }

            excludedAliases.Add(alias.Trim());
        }

        return excludedAliases;
    }

    private void AddExcludeFromSearchFilters(ICollection<Filter> filters)
    {
        string? propertyAlias = razorSearchOptions.Value.ExcludeFromSearchPropertyAlias?.Trim();
        if (string.IsNullOrWhiteSpace(propertyAlias))
        {
            return;
        }

        filters.Add(new IntegerExactFilter(Constants.InternalIndex.ExcludedFlagFieldName, [1], true));
    }

    private static string ResolveTitle(SearchCandidate candidate)
        => string.IsNullOrWhiteSpace(candidate.Snapshot.TitleText) is false
            ? candidate.Snapshot.TitleText
            : candidate.Snapshot.Culture is null
                ? candidate.Content.Name
                : candidate.Content.Name(candidate.Snapshot.Culture);

    private static string ResolveSummarySource(RazorSearchSnapshot snapshot) =>
        string.IsNullOrWhiteSpace(snapshot.SummaryText) is false
            ? snapshot.SummaryText
            : snapshot.BodyText ?? snapshot.Snapshot;

    internal static RazorSearchSnapshot? SelectSnapshot(
        IReadOnlyCollection<RazorSearchSnapshot> snapshots,
        string? culture)
        => snapshots
            .Where(x => x.RenderStatus == RazorSearchSnapshotStatuses.Success)
            .Where(x => string.IsNullOrEmpty(x.Culture) || string.Equals(x.Culture, culture, StringComparison.OrdinalIgnoreCase))
            .OrderByDescending(x => string.Equals(x.Culture, culture, StringComparison.OrdinalIgnoreCase))
            .ThenByDescending(x => x.UpdatedAtUtc)
            .FirstOrDefault();

    private static string[] Tokenize(string text) =>
        text.Split(
                [' ', '\t', '\r', '\n'],
                StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries
            )
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToArray();

    private static AccessContext CreateAccessContext() => new(Guid.Empty, []);

    private sealed record SearchCandidate(
        Guid ContentKey,
        IPublishedContent Content,
        RazorSearchSnapshot Snapshot
    );
}

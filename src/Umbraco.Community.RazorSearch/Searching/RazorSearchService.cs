using Microsoft.Extensions.Options;
using Umbraco.Cms.Core;
using Umbraco.Cms.Core.Models;
using Umbraco.Cms.Core.Models.PublishedContent;
using Umbraco.Cms.Core.PublishedCache;
using Umbraco.Cms.Core.Services;
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
    IContentTypeService contentTypeService,
    IUmbracoContextFactory umbracoContextFactory,
    IOptions<RazorSearchOptions> razorSearchOptions
) : IRazorSearchService
{
    private const int SearchBatchSize = 128;

    public async Task<IRazorSearchResult> SearchAsync(
        Models.RazorSearch search,
        CancellationToken cancellationToken = default
    )
    {
        RazorSearchRequestValidator.Validate(search);

        string[] terms = Tokenize(search.Text);

        using var contextReference = umbracoContextFactory.EnsureUmbracoContext();
        IPublishedContentCache? contentCache = contextReference.UmbracoContext.Content;
        if (contentCache is null)
        {
            return EmptyResult(search);
        }

        HashSet<Guid> rootKeys = search.RootKeys.ToHashSet();
        HashSet<string> includedAliases = search.IncludedContentTypeAliases.ToHashSet(
            StringComparer.OrdinalIgnoreCase
        );
        HashSet<string> excludedAliases = GetEffectiveExcludedAliases(search);
        Filter[] filters = CreateMetadataFilters(
            contentCache,
            rootKeys,
            includedAliases,
            excludedAliases,
            out bool canMatch
        );
        if (canMatch is false)
        {
            return EmptyResult(search);
        }

        ISearcher searcher = searcherResolver.GetRequiredSearcher(
            Umbraco.Cms.Search.Core.Constants.IndexAliases.PublishedContent
        );

        SearchCandidate[] candidates = await SearchCandidatesAsync(
            searcher,
            contentCache,
            search,
            filters,
            cancellationToken
        );

        if (candidates.Length == 0)
        {
            return EmptyResult(search);
        }

        IRazorSearchResultItem[] items = candidates
            .Skip(search.Skip)
            .Take(search.Take)
            .Select(x => new RazorSearchResultItem
            {
                ContentKey = x.ContentKey,
                Content = x.Content,
                Url = ResolveUrl(search, x),
                Title = ResolveTitle(search.Culture, x),
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
            Total = candidates.LongLength,
            Skip = search.Skip,
            Take = search.Take,
            Items = items,
        };

        return result;
    }

    private static IRazorSearchResult EmptyResult(Models.RazorSearch search) =>
        new RazorSearchResult
        {
            Total = 0,
            Skip = search.Skip,
            Take = search.Take,
            Items = [],
        };

    private async Task<SearchCandidate[]> SearchCandidatesAsync(
        ISearcher searcher,
        IPublishedContentCache contentCache,
        Models.RazorSearch search,
        IReadOnlyCollection<Filter> filters,
        CancellationToken cancellationToken
    )
    {
        var candidates = new List<SearchCandidate>();
        var seenContentKeys = new HashSet<Guid>();

        int skip = 0;
        long total = long.MaxValue;

        while (skip < total)
        {
            SearchResult searchResult = await searcher.SearchAsync(
                Umbraco.Cms.Search.Core.Constants.IndexAliases.PublishedContent,
                search.Text,
                filters,
                [],
                [new ScoreSorter(Direction.Descending)],
                search.Culture ?? string.Empty,
                search.Segment ?? string.Empty,
                CreateAccessContext(),
                skip,
                SearchBatchSize,
                0
            );

            total = searchResult.Total;
            Document[] documents = searchResult.Documents.ToArray();
            if (documents.Length == 0)
            {
                break;
            }

            skip += documents.Length;

            Guid[] documentIds = documents
                .Where(x => x.ObjectType == UmbracoObjectTypes.Document)
                .Select(x => x.Id)
                .Distinct()
                .ToArray();

            if (documentIds.Length == 0)
            {
                continue;
            }

            IReadOnlyCollection<RazorSearchSnapshot> snapshots =
                await snapshotStore.GetByContentKeysAsync(documentIds, cancellationToken);
            Dictionary<Guid, RazorSearchSnapshot[]> snapshotsByContentKey = snapshots
                .GroupBy(x => x.ContentKey)
                .ToDictionary(x => x.Key, x => x.ToArray());

            foreach (Guid documentId in documentIds)
            {
                if (seenContentKeys.Add(documentId) is false)
                {
                    continue;
                }

                if (
                    snapshotsByContentKey.TryGetValue(
                        documentId,
                        out RazorSearchSnapshot[]? contentSnapshots
                    )
                    is false
                )
                {
                    continue;
                }

                SearchCandidate? candidate = CreateCandidate(
                    contentSnapshots,
                    contentCache,
                    search,
                    documentId
                );
                if (candidate is null)
                {
                    continue;
                }

                candidates.Add(candidate);
            }
        }

        return candidates.ToArray();
    }

    private static SearchCandidate? CreateCandidate(
        IReadOnlyCollection<RazorSearchSnapshot> snapshots,
        IPublishedContentCache contentCache,
        Models.RazorSearch search,
        Guid contentKey
    )
    {
        IPublishedContent? content = contentCache.GetById(contentKey);
        if (content is null)
        {
            return null;
        }

        RazorSearchSnapshot? snapshot = SelectSnapshot(snapshots, search);
        if (snapshot is null)
        {
            return null;
        }

        return new SearchCandidate(contentKey, content, snapshot);
    }

    private static bool MatchesCulture(Models.RazorSearch search, RazorSearchSnapshot snapshot)
    {
        if (string.IsNullOrWhiteSpace(search.Culture))
        {
            return true;
        }

        return string.IsNullOrWhiteSpace(snapshot.Culture)
            || string.Equals(snapshot.Culture, search.Culture, StringComparison.OrdinalIgnoreCase);
    }

    private static bool MatchesSegment(Models.RazorSearch search, RazorSearchSnapshot snapshot)
    {
        if (string.IsNullOrWhiteSpace(search.Segment))
        {
            return true;
        }

        return string.IsNullOrWhiteSpace(snapshot.Segment)
            || string.Equals(snapshot.Segment, search.Segment, StringComparison.OrdinalIgnoreCase);
    }

    private Filter[] CreateMetadataFilters(
        IPublishedContentCache contentCache,
        IReadOnlySet<Guid> rootKeys,
        IReadOnlySet<string> includedAliases,
        IReadOnlySet<string> excludedAliases,
        out bool canMatch
    )
    {
        var filters = new List<Filter>();
        canMatch = true;

        int[] rootIds = ResolveRootIds(contentCache, rootKeys);
        if (rootKeys.Count > 0)
        {
            if (rootIds.Length == 0)
            {
                canMatch = false;
                return [];
            }

            filters.Add(
                new IntegerExactFilter(
                    Umbraco.Cms.Search.Core.Constants.FieldNames.PathIds,
                    rootIds,
                    false
                )
            );
        }

        int[] includedContentTypeIds = ResolveContentTypeIds(includedAliases);
        if (includedAliases.Count > 0)
        {
            if (includedContentTypeIds.Length == 0)
            {
                canMatch = false;
                return [];
            }

            filters.Add(
                new IntegerExactFilter(
                    Umbraco.Cms.Search.Core.Constants.FieldNames.ContentTypeId,
                    includedContentTypeIds,
                    false
                )
            );
        }

        int[] excludedContentTypeIds = ResolveContentTypeIds(excludedAliases);
        if (excludedContentTypeIds.Length > 0)
        {
            filters.Add(
                new IntegerExactFilter(
                    Umbraco.Cms.Search.Core.Constants.FieldNames.ContentTypeId,
                    excludedContentTypeIds,
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

        filters.Add(new KeywordFilter(propertyAlias, ["true", "1", "yes", "on"], true));
        filters.Add(new TextFilter(propertyAlias, ["true", "1", "yes", "on"], true));
        filters.Add(new IntegerExactFilter(propertyAlias, [1], true));
    }

    private static int[] ResolveRootIds(IPublishedContentCache contentCache, IReadOnlySet<Guid> rootKeys) =>
        rootKeys
            .Select(contentCache.GetById)
            .Where(x => x is not null)
            .Select(x => x!.Id)
            .Distinct()
            .ToArray();

    private int[] ResolveContentTypeIds(IReadOnlySet<string> contentTypeAliases)
    {
        if (contentTypeAliases.Count == 0)
        {
            return [];
        }

        return contentTypeService
            .GetAllContentTypeIds(contentTypeAliases.ToArray())
            .Distinct()
            .ToArray();
    }

    private static string ResolveTitle(string? culture, SearchCandidate candidate) =>
        ResolveTitle(culture, candidate.Snapshot, candidate.Content);

    private static string ResolveTitle(
        string? culture,
        RazorSearchSnapshot snapshot,
        IPublishedContent content
    ) =>
        string.IsNullOrWhiteSpace(snapshot.TitleText) is false ? snapshot.TitleText
        : culture is null ? content.Name
        : content.Name(culture);

    private static string ResolveUrl(Models.RazorSearch search, SearchCandidate candidate)
    {
        if (string.IsNullOrWhiteSpace(candidate.Snapshot.FinalUrl) is false)
        {
            return candidate.Snapshot.FinalUrl;
        }

        string? url = candidate.Content.Url(search.Culture, UrlMode.Absolute);
        return string.IsNullOrWhiteSpace(url) ? candidate.Snapshot.Route : url;
    }

    private static string ResolveSummarySource(RazorSearchSnapshot snapshot) =>
        string.IsNullOrWhiteSpace(snapshot.SummaryText) is false
            ? snapshot.SummaryText
            : snapshot.BodyText ?? snapshot.Snapshot;

    private static RazorSearchSnapshot? SelectSnapshot(
        IReadOnlyCollection<RazorSearchSnapshot> snapshots,
        Models.RazorSearch search
    )
    {
        return snapshots
            .Where(x =>
                string.Equals(
                    x.RenderStatus,
                    RazorSearchSnapshotStatuses.Success,
                    StringComparison.OrdinalIgnoreCase
                )
            )
            .Where(x => MatchesCulture(search, x))
            .Where(x => MatchesSegment(search, x))
            .OrderByDescending(x => MatchesExactVariant(search.Culture, x.Culture))
            .ThenByDescending(x => MatchesExactVariant(search.Segment, x.Segment))
            .ThenByDescending(x => x.UpdatedAtUtc)
            .FirstOrDefault();
    }

    private static string[] Tokenize(string text) =>
        text.Split(
                [' ', '\t', '\r', '\n'],
                StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries
            )
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToArray();

    private static bool MatchesExactVariant(string? requestedValue, string? snapshotValue)
    {
        if (string.IsNullOrWhiteSpace(requestedValue))
        {
            return string.IsNullOrWhiteSpace(snapshotValue);
        }

        return string.Equals(requestedValue, snapshotValue, StringComparison.OrdinalIgnoreCase);
    }

    private static AccessContext CreateAccessContext() => new(Guid.Empty, []);

    private sealed record SearchCandidate(
        Guid ContentKey,
        IPublishedContent Content,
        RazorSearchSnapshot Snapshot
    );
}

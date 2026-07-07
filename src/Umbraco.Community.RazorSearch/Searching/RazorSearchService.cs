using System.Globalization;
using Microsoft.Extensions.Options;
using Umbraco.Cms.Core;
using Umbraco.Cms.Core.Models;
using Umbraco.Cms.Core.Models.PublishedContent;
using Umbraco.Cms.Core.PublishedCache;
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
    IOptions<RazorSearchOptions> razorSearchOptions
) : IRazorSearchService
{
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
            NormalizeCultureForSearchProvider(search.Culture),
            NormalizeVariant(search.Segment),
            CreateAccessContext(),
            search.Skip,
            search.Take,
            0
        );

        Document[] documents = searchResult.Documents.ToArray();
        if (documents.Length == 0)
        {
            return EmptyResult(search);
        }

        SearchCandidate[] candidates = await BuildCandidatesAsync(
            searcher,
            contentCache,
            search,
            documents,
            cancellationToken
        );

        IRazorSearchResultItem[] items = candidates
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
            Total = searchResult.Total,
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

    private async Task<SearchCandidate[]> BuildCandidatesAsync(
        ISearcher _,
        IPublishedContentCache contentCache,
        Models.RazorSearch search,
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
                    ? CreateCandidate(contentSnapshots, contentCache, search, documentId)
                    : null)
            .Where(x => x is not null)
            .Select(x => x!)
            .ToArray();
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
        string? requestedCulture = NormalizeCulture(search.Culture);
        if (requestedCulture is null)
        {
            return true;
        }

        string? snapshotCulture = NormalizeCulture(snapshot.Culture);
        return snapshotCulture is null || CulturesMatch(requestedCulture, snapshotCulture);
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

    private static string ResolveTitle(string? culture, SearchCandidate candidate) =>
        ResolveTitle(culture, candidate.Snapshot, candidate.Content);

    private static string ResolveTitle(
        string? culture,
        RazorSearchSnapshot snapshot,
        IPublishedContent content
    ) =>
        string.IsNullOrWhiteSpace(snapshot.TitleText) is false ? snapshot.TitleText
        : ResolvePublishedName(content, culture, snapshot.Culture);

    private static string ResolveUrl(Models.RazorSearch search, SearchCandidate candidate)
    {
        if (string.IsNullOrWhiteSpace(candidate.Snapshot.FinalUrl) is false)
        {
            return candidate.Snapshot.FinalUrl;
        }

        string? url = candidate.Content.Url(
            NormalizeCulture(candidate.Snapshot.Culture) ?? NormalizeCulture(search.Culture),
            UrlMode.Absolute);
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
            .OrderByDescending(x => GetCultureMatchRank(search.Culture, x.Culture))
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

    private static string? NormalizeVariant(string? value) =>
        string.IsNullOrWhiteSpace(value) ? null : value;

    private static string? NormalizeCultureForSearchProvider(string? culture)
    {
        string? normalizedCulture = NormalizeCulture(culture);
        if (normalizedCulture is null)
        {
            return null;
        }

        return IsNeutralCulture(normalizedCulture)
            ? null
            : normalizedCulture;
    }

    private static string ResolvePublishedName(
        IPublishedContent content,
        string? requestedCulture,
        string? snapshotCulture)
    {
        string? resolvedCulture = NormalizeCulture(snapshotCulture) ?? NormalizeCulture(requestedCulture);
        return resolvedCulture is null
            ? content.Name
            : content.Name(resolvedCulture);
    }

    private static int GetCultureMatchRank(string? requestedCulture, string? snapshotCulture)
    {
        string? normalizedRequestedCulture = NormalizeCulture(requestedCulture);
        string? normalizedSnapshotCulture = NormalizeCulture(snapshotCulture);

        if (normalizedRequestedCulture is null)
        {
            return normalizedSnapshotCulture is null ? 2 : 1;
        }

        if (normalizedSnapshotCulture is null)
        {
            return 1;
        }

        if (string.Equals(normalizedRequestedCulture, normalizedSnapshotCulture, StringComparison.OrdinalIgnoreCase))
        {
            return 3;
        }

        return CulturesMatch(normalizedRequestedCulture, normalizedSnapshotCulture) ? 2 : 0;
    }

    private static bool CulturesMatch(string requestedCulture, string snapshotCulture)
    {
        if (string.Equals(requestedCulture, snapshotCulture, StringComparison.OrdinalIgnoreCase))
        {
            return true;
        }

        string requestedLanguage = GetLanguagePart(requestedCulture);
        string snapshotLanguage = GetLanguagePart(snapshotCulture);

        return string.Equals(requestedLanguage, snapshotLanguage, StringComparison.OrdinalIgnoreCase)
            && (IsNeutralCulture(requestedCulture) || IsNeutralCulture(snapshotCulture));
    }

    private static string GetLanguagePart(string culture)
    {
        int separatorIndex = culture.IndexOf('-');
        return separatorIndex < 0 ? culture : culture[..separatorIndex];
    }

    private static bool IsNeutralCulture(string culture) => culture.Contains('-') is false;

    private static string? NormalizeCulture(string? culture)
    {
        string? normalizedCulture = NormalizeVariant(culture);
        if (normalizedCulture is null)
        {
            return null;
        }

        normalizedCulture = normalizedCulture.Replace('_', '-');

        try
        {
            return CultureInfo.GetCultureInfo(normalizedCulture).Name;
        }
        catch (CultureNotFoundException)
        {
            return normalizedCulture;
        }
    }

    private sealed record SearchCandidate(
        Guid ContentKey,
        IPublishedContent Content,
        RazorSearchSnapshot Snapshot
    );
}

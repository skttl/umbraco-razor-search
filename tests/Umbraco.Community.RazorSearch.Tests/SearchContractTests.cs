using Microsoft.Extensions.Options;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using Umbraco.Cms.Core;
using Umbraco.Cms.Core.Events;
using Umbraco.Cms.Core.Models;
using Umbraco.Cms.Core.Models.PublishedContent;
using Umbraco.Cms.Core.PublishedCache;
using Umbraco.Cms.Core.Routing;
using Umbraco.Cms.Core.Services;
using Umbraco.Cms.Core.Web;
using Umbraco.Cms.Search.Core.Models.Searching;
using Umbraco.Cms.Search.Core.Models.Searching.Faceting;
using Umbraco.Cms.Search.Core.Models.Searching.Filtering;
using Umbraco.Cms.Search.Core.Models.Searching.Sorting;
using Umbraco.Cms.Search.Core.Services;
using Umbraco.Cms.Search.Core.Models.Indexing;
using Umbraco.Cms.Search.Core.Services.ContentIndexing;
using Umbraco.Community.RazorSearch.Configuration;
using Umbraco.Community.RazorSearch.Indexing;
using Umbraco.Community.RazorSearch.Persistence.Models;
using Umbraco.Community.RazorSearch.Persistence.Stores;
using Umbraco.Community.RazorSearch.Searching;
using SearchRequest = Umbraco.Community.RazorSearch.Models.RazorSearch;

namespace Umbraco.Community.RazorSearch.Tests;

public class SearchContractTests
{
    [Theory]
    [InlineData(null, null)]
    [InlineData("da-DK", "da-DK")]
    [InlineData("DA-dk", "da-DK")]
    [InlineData("en-US", "en-US")]
    public void CultureUsesConfiguredExactForm(string? input, string? expected)
        => Assert.Equal(expected, RazorSearchRequestValidator.ResolveCulture(input, ["da-DK", "en-US"]));

    [Theory]
    [InlineData("da")]
    [InlineData("da_DK")]
    [InlineData("sv-SE")]
    [InlineData("")]
    [InlineData(" ")]
    public void UnconfiguredCultureFailsRatherThanBroadeningSearch(string input)
        => Assert.Throws<ArgumentException>(() => RazorSearchRequestValidator.ResolveCulture(input, ["da-DK", "en-US"]));

    [Fact]
    public void ExplicitlyConfiguredNeutralCultureIsSupported()
        => Assert.Equal("da", RazorSearchRequestValidator.ResolveCulture("DA", ["da"]));

    [Theory]
    [InlineData(null, "invariant")]
    [InlineData("da-DK", "danish")]
    [InlineData("en-US", "english")]
    public void SnapshotSelectionDoesNotMixLanguages(string? culture, string expected)
    {
        var snapshots = new[] { Snapshot(null, "invariant"), Snapshot("da-DK", "danish"), Snapshot("en-US", "english") };
        Assert.Equal(expected, RazorSearchService.SelectSnapshot(snapshots, culture)?.TitleText);
        Assert.Null(RazorSearchService.SelectSnapshot([Snapshot("en-US", "english")], culture == "en-US" ? null : culture));
    }

    [Fact]
    public void RequestedCultureAlsoSelectsInvariantDocument()
        => Assert.Equal("invariant", RazorSearchService.SelectSnapshot([Snapshot(null, "invariant")], "da-DK")?.TitleText);

    [Fact]
    public void FailedAttemptDoesNotHideLastSuccessfulSnapshot()
    {
        var snapshot = Snapshot("da-DK", "usable") with { LastRenderError = "HTTP 503", LastAttemptAtUtc = DateTimeOffset.UtcNow };
        var variant = Assert.Single(RazorSearchSnapshotIndexProjection.ProjectSuccessfulVariants([snapshot]));
        Assert.Equal("usable", Assert.Single(variant.Titles));
        Assert.Equal(snapshot, RazorSearchService.SelectSnapshot([snapshot], "da-DK"));
    }

    [Fact]
    public void FailedSnapshotIsNeverProjected()
        => Assert.Empty(RazorSearchSnapshotIndexProjection.ProjectSuccessfulVariants([Snapshot(null, "failure") with { RenderStatus = RazorSearchSnapshotStatuses.Failed }]));

    [Fact]
    public async Task SavingDraftDoesNotDeletePublishedIndexEntry()
    {
        var content = new Mock<IContentService>(MockBehavior.Strict);
        var indexer = new Mock<IIndexer>(MockBehavior.Strict);
        using var provider = new ServiceCollection()
            .AddSingleton(content.Object)
            .AddSingleton(Mock.Of<IContentProtectionProvider>())
            .AddSingleton(Mock.Of<IEventAggregator>())
            .AddSingleton(Mock.Of<IRazorSearchSnapshotStore>())
            .AddSingleton(Mock.Of<ISystemFieldsContentIndexer>())
            .AddSingleton(Mock.Of<IRazorSearchContentFilter>())
            .BuildServiceProvider();
        var strategy = new RazorSearchContentIndexChangeStrategy(provider.GetRequiredService<IServiceScopeFactory>(),
            NullLogger<RazorSearchContentIndexChangeStrategy>.Instance);

        await strategy.HandleAsync([new ContentIndexInfo("test", [UmbracoObjectTypes.Document], indexer.Object)],
            [ContentChange.Document(Guid.NewGuid(), ChangeImpact.Refresh, ContentState.Draft)], default);

        content.VerifyNoOtherCalls();
        indexer.VerifyNoOtherCalls();
    }

    [Fact]
    public async Task OmittedCultureReachesProviderAsNull()
    {
        var fixture = new SearchFixture(new SearchResult(0, [], []));
        var result = await fixture.Service.SearchAsync(new SearchRequest("needle"));
        Assert.Null(fixture.Culture);
        Assert.Equal(10, fixture.Take);
        Assert.Empty(result.Items);
        Assert.Equal(0, result.Total);
    }

    [Fact]
    public async Task ProviderReceivesCultureFiltersAndPaginationAndEmptyPageRetainsTotal()
    {
        var fixture = new SearchFixture(new SearchResult(37, [], []));
        var root = Guid.NewGuid();
        var result = await fixture.Service.SearchAsync(new SearchRequest("needle").InCulture("DA-dk").UnderRoot(root)
            .IncludeContentType("article").ExcludeContentType("landing").SkipTake(40, 10));

        Assert.Equal(37, result.Total);
        Assert.Equal(40, result.Skip);
        Assert.Equal(10, result.Take);
        Assert.Empty(result.Items);
        Assert.Equal("da-DK", fixture.Culture);
        Assert.Null(fixture.Segment);
        Assert.Equal(40, fixture.Skip);
        Assert.Equal(10, fixture.Take);
        Assert.Equal(Guid.Empty, fixture.Access?.PrincipalId);
        Assert.Contains(fixture.Filters, x => x is KeywordFilter { FieldName: Umbraco.Cms.Search.Core.Constants.FieldNames.PathIds, Negate: false });
        Assert.Contains(fixture.Filters, x => x is KeywordFilter { FieldName: Constants.InternalIndex.ContentTypeAliasFieldName, Negate: false } filter && filter.Values.Contains("article"));
        Assert.Contains(fixture.Filters, x => x is KeywordFilter { FieldName: Constants.InternalIndex.ContentTypeAliasFieldName, Negate: true } filter && filter.Values.Contains("landing"));
        Assert.Contains(fixture.Filters, x => x is IntegerExactFilter { FieldName: Constants.InternalIndex.ExcludedFlagFieldName, Negate: true });
    }

    [Fact]
    public async Task ResultOrderAndPublicUrlsFollowProviderAndMatchingSnapshots()
    {
        var first = Snapshot("da-DK", "first");
        var second = Snapshot(null, "second");
        var fixture = new SearchFixture(new SearchResult(2,
            [new Document(first.ContentKey, UmbracoObjectTypes.Document), new Document(second.ContentKey, UmbracoObjectTypes.Document)], []));
        fixture.Snapshots.Setup(x => x.GetByContentKeysAsync(It.IsAny<IEnumerable<Guid>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync([second, first with { FinalUrl = "http://internal:5000/render" }, first with { Culture = "en-US", TitleText = "wrong language" }]);
        fixture.Content.Setup(x => x.GetById(It.IsAny<Guid>())).Returns(Mock.Of<IPublishedContent>());

        var result = await fixture.Service.SearchAsync(new SearchRequest("needle").InCulture("da-DK"));

        Assert.Equal([first.ContentKey, second.ContentKey], result.Items.Select(x => x.ContentKey));
        Assert.Equal(["first", "second"], result.Items.Select(x => x.Title));
        Assert.All(result.Items, item => Assert.Equal("https://public.example/page", item.Url));
        Assert.Equal(2, result.Total);
    }

    [Theory]
    [InlineData(null, null, null)]
    [InlineData("DA-dk", "da-dk", "da-DK")]
    [InlineData("DA-dk", null, null)]
    public async Task MovedContentUsesCurrentPublicUrlAndPreservesSuccessfulTextAfterRenderFailure(string? requestedCulture, string? snapshotCulture, string? urlCulture)
    {
        var snapshot = Snapshot(snapshotCulture, "previous successful title") with
        {
            Route = "https://public.example/old-route",
            FinalUrl = "http://internal:5000/old-route",
            LastRenderError = "HTTP 503",
            LastAttemptAtUtc = DateTimeOffset.UtcNow,
        };
        var fixture = new SearchFixture(new SearchResult(1, [new Document(snapshot.ContentKey, UmbracoObjectTypes.Document)], []));
        fixture.Snapshots.Setup(x => x.GetByContentKeysAsync(It.IsAny<IEnumerable<Guid>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync([snapshot]);
        var content = Mock.Of<IPublishedContent>();
        fixture.Content.Setup(x => x.GetById(snapshot.ContentKey)).Returns(content);
        fixture.Urls.Setup(x => x.GetUrl(content, UrlMode.Absolute, urlCulture, null))
            .Returns("https://public.example/new-route");
        var query = new SearchRequest("needle");
        if (requestedCulture is not null) query.InCulture(requestedCulture);

        var item = Assert.Single((await fixture.Service.SearchAsync(query)).Items);

        Assert.Equal("https://public.example/new-route", item.Url);
        Assert.Equal("previous successful title", item.Title);
        Assert.Contains("body", item.SummaryHtml);
        fixture.Urls.Verify(x => x.GetUrl(content, UrlMode.Absolute, urlCulture, null), Times.Once);
    }

    [Theory]
    [InlineData(0, 10)]
    [InlineData(1, 0)]
    [InlineData(-1, 10)]
    public void InvalidPagesAreRejected(int page, int size)
        => Assert.Throws<ArgumentOutOfRangeException>(() => new SearchRequest("x").Page(page, size));

    private static RazorSearchSnapshot Snapshot(string? culture, string title) => new()
    {
        ContentKey = Guid.NewGuid(), Culture = culture, Route = "https://public.example/page", Renderer = "http",
        Snapshot = "needle body", TitleText = title, BodyText = "needle body", RenderStatus = RazorSearchSnapshotStatuses.Success,
    };

    private sealed class SearchFixture
    {
        public Mock<IRazorSearchSnapshotStore> Snapshots { get; } = new();
        public Mock<IPublishedContentCache> Content { get; } = new();
        public Mock<IPublishedUrlProvider> Urls { get; } = new();
        public RazorSearchService Service { get; }
        public string? Culture { get; private set; }
        public string? Segment { get; private set; }
        public int Skip { get; private set; }
        public int Take { get; private set; }
        public AccessContext? Access { get; private set; }
        public Filter[] Filters { get; private set; } = [];

        public SearchFixture(SearchResult result)
        {
            var searcher = new Mock<ISearcher>();
            searcher.Setup(x => x.SearchAsync(It.IsAny<string>(), It.IsAny<string?>(), It.IsAny<IEnumerable<Filter>?>(),
                    It.IsAny<IEnumerable<Facet>?>(), It.IsAny<IEnumerable<Sorter>?>(), It.IsAny<string?>(), It.IsAny<string?>(),
                    It.IsAny<AccessContext?>(), It.IsAny<int>(), It.IsAny<int>(), It.IsAny<int>()))
                .Callback<string, string?, IEnumerable<Filter>?, IEnumerable<Facet>?, IEnumerable<Sorter>?, string?, string?, AccessContext?, int, int, int>(
                    (_, _, filters, _, _, culture, segment, access, skip, take, _) =>
                    { Filters = filters!.ToArray(); Culture = culture; Segment = segment; Access = access; Skip = skip; Take = take; })
                .ReturnsAsync(result);
            var resolver = Mock.Of<ISearcherResolver>(x => x.GetSearcher(Constants.InternalIndex.Alias) == searcher.Object);
            var context = Mock.Of<IUmbracoContext>(x => x.Content == Content.Object);
            var factory = new Mock<IUmbracoContextFactory>();
            factory.Setup(x => x.EnsureUmbracoContext()).Returns(new UmbracoContextReference(context, false, Mock.Of<IUmbracoContextAccessor>()));
            var languages = new Mock<ILanguageService>();
            languages.Setup(x => x.GetAllAsync()).ReturnsAsync([Mock.Of<ILanguage>(x => x.IsoCode == "da-DK"), Mock.Of<ILanguage>(x => x.IsoCode == "en-US")]);
            Urls.Setup(x => x.GetUrl(It.IsAny<IPublishedContent>(), UrlMode.Absolute, It.IsAny<string?>(), null))
                .Returns("https://public.example/page");
            Service = new RazorSearchService(Snapshots.Object, resolver, factory.Object, Options.Create(new RazorSearchOptions { ExcludeFromSearchPropertyAlias = "excludeFromSearch" }), languages.Object, Urls.Object);
        }
    }
}

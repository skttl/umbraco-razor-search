using Examine;
using Examine.Lucene;
using Examine.Lucene.Directories;
using Examine.Lucene.Providers;
using Lucene.Net.Store;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Moq;
using Umbraco.Cms.Core;
using Umbraco.Cms.Core.Events;
using Umbraco.Cms.Core.Models;
using Umbraco.Cms.Core.Models.PublishedContent;
using Umbraco.Cms.Core.PublishedCache;
using Umbraco.Cms.Core.Routing;
using Umbraco.Cms.Core.Services;
using Umbraco.Cms.Core.Web;
using Umbraco.Cms.Search.Core.Extensions;
using Umbraco.Cms.Search.Core.Models.Indexing;
using Umbraco.Cms.Search.Core.Services;
using Umbraco.Cms.Search.Core.Services.ContentIndexing;
using Umbraco.Cms.Search.Provider.Examine.DependencyInjection;
using Umbraco.Cms.Search.Provider.Examine.Services;
using Umbraco.Community.RazorSearch.Configuration;
using Umbraco.Community.RazorSearch.Examine.Composing;
using Umbraco.Community.RazorSearch.Indexing;
using Umbraco.Community.RazorSearch.Persistence.Models;
using Umbraco.Community.RazorSearch.Persistence.Stores;
using Umbraco.Community.RazorSearch.Searching;
using SearchRequest = Umbraco.Community.RazorSearch.Models.RazorSearch;

namespace Umbraco.Community.RazorSearch.Tests;

public class ExamineProviderIntegrationTests
{
    [Fact]
    public async Task RealProviderMatchesExactCultureAndInvariantWithCanonicalFieldCulture()
    {
        using var fixture = new Fixture();
        Guid invariant = await fixture.IndexAsync(null, "common invariant", "invariantword");
        Guid variant = Guid.NewGuid();
        await fixture.IndexAsync("da-DK", "common dansk", "danishword", key: variant);
        await fixture.IndexAsync("en-US", "common english", "englishword", key: variant);

        var invariantOnly = await fixture.Search.SearchAsync(new SearchRequest("common"));
        Assert.Equal(1, invariantOnly.Total);
        Assert.Equal(invariant, Assert.Single(invariantOnly.Items).ContentKey);
        foreach (string culture in new[] { "da-DK", "en-US" })
        {
            var result = await fixture.Search.SearchAsync(new SearchRequest("common").InCulture(culture));
            Assert.Equal(2, result.Total);
            Assert.Equal(new[] { invariant, variant }.Order(), result.Items.Select(x => x.ContentKey).Order());
            Assert.Equal(culture == "da-DK" ? "common dansk" : "common english", result.Items.Single(x => x.ContentKey == variant).Title);
            Assert.All(result.Items, item => Assert.Equal("https://public.example/" + item.ContentKey, item.Url));
        }
        Assert.Equal(variant, Assert.Single((await fixture.Search.SearchAsync(new SearchRequest("danishword").InCulture("DA-dk"))).Items).ContentKey);
        Assert.Empty((await fixture.Search.SearchAsync(new SearchRequest("englishword").InCulture("da-DK"))).Items);
        Assert.Empty((await fixture.Search.SearchAsync(new SearchRequest("danishword"))).Items);
    }

    [Fact]
    public async Task RealProviderFiltersProtectionRootsAliasesAndExclusionsBeforePaging()
    {
        using var fixture = new Fixture();
        Guid root = Guid.NewGuid();
        Guid first = await fixture.IndexAsync(null, "needle", "firstbody", root: root);
        Guid second = await fixture.IndexAsync(null, "Second title", "needle", root: root);
        await fixture.IndexAsync(null, "needle private", "needle", root: root, protection: new ContentProtection([Guid.NewGuid()]));
        await fixture.IndexAsync(null, "needle wrongroot", "needle", root: Guid.NewGuid());
        await fixture.IndexAsync(null, "needle wrongtype", "needle", root: root, alias: "landing");
        await fixture.IndexAsync(null, "needle excludedflag", "needle", root: root, excluded: true);
        await fixture.IndexAsync(null, "needle excludedtype", "needle", root: root, alias: "news");

        SearchRequest Query(int skip) => new SearchRequest("needle").UnderRoot(root)
            .IncludeContentTypes("article", "news").ExcludeContentType("news").SkipTake(skip, 1);
        var pageOne = await fixture.Search.SearchAsync(Query(0));
        var pageTwo = await fixture.Search.SearchAsync(Query(1));
        var pastEnd = await fixture.Search.SearchAsync(Query(2));

        Assert.Equal(2, pageOne.Total);
        Assert.Equal(2, pageTwo.Total);
        Assert.Equal(2, pastEnd.Total);
        Assert.Equal(first, Assert.Single(pageOne.Items).ContentKey);
        Assert.Equal(second, Assert.Single(pageTwo.Items).ContentKey);
        Assert.Empty(pastEnd.Items);
    }

    private sealed class Fixture : IDisposable
    {
        private readonly string _directory = Path.Combine(Path.GetTempPath(), "razorsearch-provider-tests", Guid.NewGuid().ToString("N"));
        private readonly ServiceProvider _services;
        private readonly LuceneIndex _index;
        private readonly IIndexer _indexer;
        private readonly RazorSearchContentIndexChangeStrategy _strategy;
        private readonly Mock<IContentService> _content = new();
        private readonly Mock<IRazorSearchSnapshotStore> _snapshots = new();
        private readonly Mock<ISystemFieldsContentIndexer> _systemFields = new();
        private readonly Mock<IContentProtectionProvider> _protection = new();
        private readonly Mock<IRazorSearchContentFilter> _filter = new();
        private readonly Mock<IPublishedContentCache> _published = new();
        private readonly Dictionary<Guid, List<RazorSearchSnapshot>> _stored = [];
        public RazorSearchService Search { get; }

        public Fixture()
        {
            var services = new ServiceCollection();
            services.AddLogging();
            var builder = Mock.Of<IUmbracoBuilder>(x => x.Services == services && x.Config == new ConfigurationBuilder().Build());
            builder.AddExamineSearchProvider();
            new RazorSearchExamineComposer().Compose(builder);
            // Keep the real provider's field configuration; replace only physical indexes with a local test index.
            services.RemoveAll<IIndex>();
            foreach (ServiceDescriptor registration in services.Where(x =>
                         x.ServiceType == typeof(IConfigureOptions<LuceneDirectoryIndexOptions>)
                         && x.ImplementationFactory?.Method.DeclaringType?.FullName?.StartsWith("Examine.ServicesCollectionExtensions", StringComparison.Ordinal) is true).ToArray())
            {
                services.Remove(registration);
            }
            services.AddSingleton<TestDirectoryFactory>(_ => new TestDirectoryFactory(_directory));
            services.AddExamineLuceneIndex<LuceneIndex, TestDirectoryFactory>(Constants.InternalIndex.Alias, _ => { });
            services.AddSingleton(_content.Object);
            services.AddSingleton(_snapshots.Object);
            services.AddSingleton(_systemFields.Object);
            services.AddSingleton(_protection.Object);
            services.AddSingleton(_filter.Object);
            services.AddSingleton(Mock.Of<IEventAggregator>());
            _services = services.BuildServiceProvider();
            var manager = _services.GetRequiredService<IExamineManager>();
            Assert.True(manager.TryGetIndex(Constants.InternalIndex.Alias, out IIndex? index));
            _index = Assert.IsType<LuceneIndex>(index);
            var indexOptions = _services.GetRequiredService<IOptionsMonitor<LuceneDirectoryIndexOptions>>().Get(Constants.InternalIndex.Alias);
            Assert.Equal(FieldDefinitionTypes.Raw, indexOptions.FieldDefinitions.Single(x => x.Name == "Sys_Culture").Type);
            Assert.Equal(FieldDefinitionTypes.Raw, indexOptions.FieldDefinitions.Single(x => x.Name == "Field_Umb_PathIds_keywords").Type);
            _index.CreateIndex();
            _indexer = _services.GetRequiredService<IExamineIndexer>();
            _strategy = new(_services.GetRequiredService<IServiceScopeFactory>(), NullLogger<RazorSearchContentIndexChangeStrategy>.Instance);
            _snapshots.Setup(x => x.GetByContentKeyAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
                .Returns((Guid key, CancellationToken _) => Task.FromResult<IReadOnlyCollection<RazorSearchSnapshot>>(_stored[key]));
            _snapshots.Setup(x => x.GetByContentKeysAsync(It.IsAny<IEnumerable<Guid>>(), It.IsAny<CancellationToken>()))
                .Returns((IEnumerable<Guid> keys, CancellationToken _) => Task.FromResult<IReadOnlyCollection<RazorSearchSnapshot>>(keys.SelectMany(key => _stored[key]).ToArray()));
            var context = Mock.Of<IUmbracoContext>(x => x.Content == _published.Object);
            var factory = new Mock<IUmbracoContextFactory>();
            factory.Setup(x => x.EnsureUmbracoContext()).Returns(new UmbracoContextReference(context, false, Mock.Of<IUmbracoContextAccessor>()));
            var languages = new Mock<ILanguageService>();
            languages.Setup(x => x.GetAllAsync()).ReturnsAsync([Mock.Of<ILanguage>(x => x.IsoCode == "da-DK"), Mock.Of<ILanguage>(x => x.IsoCode == "en-US")]);
            var resolver = Mock.Of<ISearcherResolver>(x => x.GetSearcher(Constants.InternalIndex.Alias) == _services.GetRequiredService<IExamineSearcher>());
            var urls = new Mock<IPublishedUrlProvider>();
            urls.Setup(x => x.GetUrl(It.IsAny<IPublishedContent>(), UrlMode.Absolute, It.IsAny<string?>(), null))
                .Returns((IPublishedContent content, UrlMode _, string? culture, Uri? _) =>
                    culture is not null && !content.ContentType.Variations.HasFlag(ContentVariation.Culture)
                        ? "#" : "https://public.example/" + content.Key);
            Search = new(_snapshots.Object, resolver, factory.Object, Options.Create(new RazorSearchOptions { ExcludeFromSearchPropertyAlias = "excludeFromSearch" }), languages.Object, urls.Object);
        }

        public async Task<Guid> IndexAsync(string? culture, string title, string body, Guid? key = null, Guid? root = null,
            string alias = "article", bool excluded = false, ContentProtection? protection = null)
        {
            Guid id = key ?? Guid.NewGuid();
            if (!_stored.TryGetValue(id, out var snapshots)) _stored[id] = snapshots = [];
            snapshots.Add(new() { ContentKey = id, Culture = culture?.ToLowerInvariant(), Route = "https://public.example/" + id,
                Renderer = "http", Snapshot = body, TitleText = title, BodyText = body });
            var type = Mock.Of<ISimpleContentType>(x => x.Alias == alias && x.Variations == (culture == null ? ContentVariation.Nothing : ContentVariation.Culture));
            var content = new Mock<IContent>();
            content.SetupGet(x => x.Key).Returns(id);
            content.SetupGet(x => x.Id).Returns(123);
            content.SetupGet(x => x.Path).Returns("-1,123");
            content.SetupGet(x => x.Published).Returns(true);
            content.SetupGet(x => x.ContentType).Returns(type);
            content.SetupGet(x => x.PublishedCultures).Returns(snapshots.Where(x => x.Culture is not null)
                .Select(x => x.Culture == "da-dk" ? "da-DK" : "en-US").ToArray());
            _content.Setup(x => x.GetById(id)).Returns(content.Object);
            var publishedType = Mock.Of<IPublishedContentType>(x => x.Variations == (culture == null ? ContentVariation.Nothing : ContentVariation.Culture));
            _published.Setup(x => x.GetById(id)).Returns(Mock.Of<IPublishedContent>(x => x.Key == id && x.ContentType == publishedType));
            _systemFields.Setup(x => x.GetIndexFieldsAsync(content.Object, It.IsAny<string?[]>(), true, It.IsAny<CancellationToken>()))
                .ReturnsAsync([new IndexField(Umbraco.Cms.Search.Core.Constants.FieldNames.PathIds,
                    new IndexValue { Keywords = [(root ?? id).AsKeyword()] }, null, null)]);
            _protection.Setup(x => x.GetContentProtectionAsync(content.Object)).ReturnsAsync(protection);
            _filter.Setup(x => x.IsExcluded(content.Object, It.IsAny<string?>(), true)).Returns(excluded);
            using var synchronousIndexing = _index.WithThreadingMode(IndexThreadingMode.Synchronous);
            await _strategy.HandleAsync([new ContentIndexInfo(Constants.InternalIndex.Alias, [UmbracoObjectTypes.Document], _indexer)],
                [ContentChange.Document(id, ChangeImpact.Refresh, ContentState.Published)], default);
            _index.WaitForChanges();
            return id;
        }

        public void Dispose()
        {
            _services.Dispose();
            if (System.IO.Directory.Exists(_directory)) System.IO.Directory.Delete(_directory, recursive: true);
        }
    }

    private sealed class TestDirectoryFactory(string path) : IDirectoryFactory
    {
        public Lucene.Net.Store.Directory CreateDirectory(LuceneIndex index, bool forceUnlock)
            => FSDirectory.Open(System.IO.Directory.CreateDirectory(Path.Combine(path, index.Name)));
    }
}

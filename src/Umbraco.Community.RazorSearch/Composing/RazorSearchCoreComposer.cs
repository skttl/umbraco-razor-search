using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Options;
using Umbraco.Cms.Core.Models;
using Umbraco.Cms.Core.Composing;
using Umbraco.Cms.Core.DependencyInjection;
using Umbraco.Cms.Search.Core.Configuration;
using Umbraco.Cms.Search.Core.Services;
using Umbraco.Cms.Search.Core.Services.ContentIndexing;
using Umbraco.Community.RazorSearch.Indexing;
using Umbraco.Community.RazorSearch.Searching;
using Umbraco.Community.RazorSearch.Services;

namespace Umbraco.Community.RazorSearch.Composing;

public sealed class RazorSearchCoreComposer : IComposer
{
    public void Compose(IUmbracoBuilder builder)
    {
        builder.Services.TryAddSingleton<IRenderRequestTokenProvider, DefaultRenderRequestTokenProvider>();
        builder.Services.TryAddScoped<IRazorSearchService, RazorSearchService>();
        builder.Services.AddTransient<RazorSearchContentIndexChangeStrategy>();
        builder.Services.Configure<IndexOptions>(options =>
            options.RegisterContentIndex<IIndexer, ISearcher, RazorSearchContentIndexChangeStrategy>(
                Constants.InternalIndex.Alias,
                UmbracoObjectTypes.Document));
    }
}

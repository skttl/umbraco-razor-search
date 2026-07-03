using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Umbraco.Cms.Core.Composing;
using Umbraco.Cms.Core.DependencyInjection;
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
        builder.Services.TryAddEnumerable(ServiceDescriptor.Transient<IContentIndexer, RazorSearchSnapshotContentIndexer>());
    }
}

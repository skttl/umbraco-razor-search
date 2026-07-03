using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Umbraco.Cms.Core.Composing;
using Umbraco.Cms.Core.DependencyInjection;
using Umbraco.Cms.Web.Common.ApplicationBuilder;
using Umbraco.Community.RazorSearch.Composers;
using Umbraco.Community.RazorSearch.Configuration;
using Umbraco.Community.RazorSearch.Indexing;
using Umbraco.Community.RazorSearch.Middleware;
using Umbraco.Community.RazorSearch.Persistence.Stores;
using Umbraco.Community.RazorSearch.Rendering;
using Umbraco.Community.RazorSearch.Searching;
using Umbraco.Community.RazorSearch.Services;

namespace Umbraco.Community.RazorSearch;

[ComposeAfter(typeof(RazorSearchPersistenceComposer))]
public sealed class RazorSearchComposer : IComposer
{
    public void Compose(IUmbracoBuilder builder)
    {
        builder.Services.Configure<RazorSearchOptions>(builder.Config.GetSection(Constants.ConfigurationSection));
        builder.Services.TryAddSingleton<IRazorSearchContentFilter, RazorSearchContentFilter>();

        builder.Services.TryAddEnumerable(ServiceDescriptor.Singleton<IRazorSearchRenderer, HttpRazorSearchRenderer>());
        builder.Services.TryAddSingleton<IRazorSearchRendererResolver, RazorSearchRendererResolver>();
        builder.Services.TryAddSingleton<InMemoryRazorSearchRenderQueue>();
        builder.Services.TryAddSingleton<IBackgroundRazorSearchRenderQueue>(sp => sp.GetRequiredService<InMemoryRazorSearchRenderQueue>());
        builder.Services.TryAddSingleton<IRazorSearchRenderQueue>(sp =>
            ActivatorUtilities.CreateInstance<FilteredRazorSearchRenderQueue>(
                sp,
                sp.GetRequiredService<InMemoryRazorSearchRenderQueue>()));
        builder.Services.Replace(ServiceDescriptor.Scoped<IRazorSearchSnapshotStore>(sp =>
            ActivatorUtilities.CreateInstance<RefreshingRazorSearchSnapshotStore>(
                sp,
                ActivatorUtilities.CreateInstance<EfCoreRazorSearchSnapshotStore>(sp))));
        builder.Services.AddHostedService<RazorSearchRenderQueueHostedService>();

        builder.Services.Configure<UmbracoPipelineOptions>(options =>
            options.AddFilter(new UmbracoPipelineFilter("RazorSearchRenderingContext")
            {
                PreRouting = app => app.UseMiddleware<RazorSearchRenderingContextMiddleware>(),
            }));
    }
}

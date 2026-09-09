using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Umbraco.Cms.Core.Composing;
using Umbraco.Cms.Core.DependencyInjection;
using Umbraco.Community.RazorSearch.Api;
using Umbraco.Community.RazorSearch.Services;

namespace Umbraco.Community.RazorSearch.Composing;

public sealed class RazorSearchManagementComposer : IComposer
{
    public void Compose(IUmbracoBuilder builder)
    {
        builder.Services.TryAddSingleton(TimeProvider.System);
        builder.Services.TryAddSingleton<RazorSearchWorkCoordinator>();
        builder.Services.TryAddSingleton<RazorSearchRebuildQueue>();
        builder.Services.AddHostedService(sp => sp.GetRequiredService<RazorSearchRebuildQueue>());
        builder.Services.TryAddSingleton<IRazorSearchQueueActivityNotifier, RazorSearchQueueActivityNotifier>();
        builder.Services.TryAddScoped<IRazorSearchManagementService, DefaultRazorSearchManagementService>();
    }
}

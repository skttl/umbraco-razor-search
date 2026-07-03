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
        builder.Services.TryAddSingleton<IRazorSearchQueueActivityNotifier, RazorSearchQueueActivityNotifier>();
        builder.Services.TryAddScoped<IRazorSearchManagementService, DefaultRazorSearchManagementService>();
    }
}

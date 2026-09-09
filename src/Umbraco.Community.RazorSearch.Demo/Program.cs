using Umbraco.Cms.Search.Core.DependencyInjection;
using Umbraco.Cms.Search.Provider.Examine.DependencyInjection;
using Umbraco.Cms.Core.Sync;

WebApplicationBuilder builder = WebApplication.CreateBuilder(args);

var umbracoBuilder = builder.CreateUmbracoBuilder()
    .AddBackOffice()
    .AddWebsite()
    .AddComposers()
    .AddSearchCore()
    .AddExamineSearchProvider();

// The demo is deliberately a single-server installation. Avoid the startup
// window where the default server-role election reports Unknown.
umbracoBuilder.Services.AddSingleton<IServerRoleAccessor, DemoServerRoleAccessor>();
umbracoBuilder.Build();

WebApplication app = builder.Build();

await app.BootUmbracoAsync();

app.UseUmbraco()
    .WithMiddleware(u =>
    {
        u.UseBackOffice();
        u.UseWebsite();
    })
    .WithEndpoints(u =>
    {
        u.UseBackOfficeEndpoints();
        u.UseWebsiteEndpoints();
    });

await app.RunAsync();

sealed class DemoServerRoleAccessor : IServerRoleAccessor
{
    public ServerRole CurrentServerRole => ServerRole.Single;
}

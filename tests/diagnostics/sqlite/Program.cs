using System.Net;
using Microsoft.AspNetCore.TestHost;
using Umbraco.Cms.Core.Models;
using Umbraco.Cms.Core.Services;
using Umbraco.Cms.Core.Strings;
using Umbraco.Cms.Core.Sync;
using Umbraco.Cms.Search.Core.DependencyInjection;
using Umbraco.Cms.Search.Provider.Examine.DependencyInjection;
using Umbraco.Community.RazorSearch.Api;
using Umbraco.Community.RazorSearch.Persistence.Stores;

// This executable has no TCP listener. Every request, including RazorSearch's HTTP
// renderer, uses the in-process TestServer handler against a new disposable database.
var cache = args.Length == 1 && args[0] is "Shared" or "Private" ? args[0] : throw new ArgumentException("Pass Shared or Private.");
var directory = Path.Combine(Path.GetTempPath(), "razorsearch-sqlite-diagnostic-" + Guid.NewGuid().ToString("N"));
Directory.CreateDirectory(directory);
System.IO.File.WriteAllText(Path.Combine(directory, "appsettings.json"), "{}");
Console.WriteLine($"DIAGNOSTIC cache={cache}; artifacts={directory}");
using var deadline = new Timer(_ => { Console.Error.WriteLine("DIAGNOSTIC FAIL: 180 second deadline exceeded"); Environment.Exit(3); }, null, TimeSpan.FromSeconds(180), Timeout.InfiniteTimeSpan);
var builder = WebApplication.CreateBuilder(new WebApplicationOptions { ContentRootPath = directory, Args = [] });
builder.WebHost.UseTestServer();
builder.Configuration.AddInMemoryCollection(new Dictionary<string, string?>
{
    ["ConnectionStrings:umbracoDbDSN"] = $"Data Source={Path.Combine(directory, "diagnostic.sqlite.db")};Cache={cache};Foreign Keys=True;Pooling=True;Default Timeout=5",
    ["ConnectionStrings:umbracoDbDSN_ProviderName"] = "Microsoft.Data.Sqlite",
    ["Umbraco:CMS:Global:Id"] = Guid.NewGuid().ToString(),
    ["Umbraco:CMS:Unattended:InstallUnattended"] = "true",
    ["Umbraco:CMS:Unattended:UpgradeUnattended"] = "true",
    ["Umbraco:CMS:Unattended:UnattendedUserName"] = "Diagnostic admin",
    ["Umbraco:CMS:Unattended:UnattendedUserEmail"] = "diagnostic@example.invalid",
    ["Umbraco:CMS:Unattended:UnattendedUserPassword"] = "Diagnostic!" + Guid.NewGuid().ToString("N"),
    ["Umbraco:CMS:ModelsBuilder:ModelsMode"] = "Nothing",
    ["Umbraco:CMS:WebRouting:UmbracoApplicationUrl"] = "http://localhost",
    ["Umbraco:Community:RazorSearch:RenderRequestToken"] = Guid.NewGuid().ToString("N"),
    ["Umbraco:Community:RazorSearch:HttpRenderer:BaseAddress"] = "http://localhost",
    ["Umbraco:Community:RazorSearch:HttpRenderer:RenderBaseAddress"] = "http://localhost",
    ["Umbraco:Community:RazorSearch:SnapshotExtraction:BodySources:0:Type"] = "selector",
    ["Umbraco:Community:RazorSearch:SnapshotExtraction:BodySources:0:Selector"] = "main",
    ["Logging:LogLevel:Default"] = "Warning",
});
builder.CreateUmbracoBuilder().AddBackOffice().AddWebsite().AddComposers().AddSearchCore().AddExamineSearchProvider().Build();
builder.Services.AddSingleton<IServerRoleAccessor>(new SingleServerRole());
TestServer? server = null;
builder.Services.AddHttpClient("RazorSearch.Rendering").ConfigurePrimaryHttpMessageHandler(() => server!.CreateHandler());
var app = builder.Build();
server = app.GetTestServer();
await app.BootUmbracoAsync();
app.UseUmbraco().WithMiddleware(u => { u.UseBackOffice(); u.UseWebsite(); }).WithEndpoints(u => { u.UseBackOfficeEndpoints(); u.UseWebsiteEndpoints(); });
app.MapPost("/acceptance/seed", async (IContentService contentService, IContentTypeService types, ITemplateService templates, IShortStringHelper strings, IUserService users, ILanguageService languages) =>
{
    var admin = users.GetUserById(-1)!;
    await languages.CreateAsync(new Language("da-DK", "Danish"), admin.Key);
    var template = (await templates.CreateAsync("Acceptance", "acceptance", "<main>pending</main>", admin.Key, null)).Result;
    template.Content = "@inherits Umbraco.Cms.Web.Common.Views.UmbracoViewPage\n<main>@Model.Name</main>";
    await templates.UpdateAsync(template, admin.Key);
    var invariant = new ContentType(strings, -1) { Alias = "acceptanceInvariant", Name = "Invariant", AllowedAsRoot = true, AllowedTemplates = [template] };
    invariant.SetDefaultTemplate(template);
    await types.CreateAsync(invariant, admin.Key);
    var variant = new ContentType(strings, -1) { Alias = "acceptanceVariant", Name = "Variant", AllowedAsRoot = true, Variations = ContentVariation.Culture, AllowedTemplates = [template] };
    variant.SetDefaultTemplate(template);
    await types.CreateAsync(variant, admin.Key);
    var inv = contentService.Create("commonword invariantonly", -1, invariant.Alias);
    contentService.Save(inv);
    var invPublish = contentService.Publish(inv, ["*"]);
    var vari = contentService.Create("commonword englishonly", -1, variant.Alias);
    vari.SetCultureName("commonword englishonly", "en-US");
    vari.SetCultureName("commonword danishonly", "da-DK");
    contentService.Save(vari);
    var varPublish = contentService.Publish(vari, ["en-US", "da-DK"]);
    return Results.Ok(new { invariantKey = inv.Key, variantKey = vari.Key, invariantPublished = invPublish.Success, variantPublished = varPublish.Success });
});
app.MapGet("/acceptance/snapshots/{key:guid}", async (Guid key, IRazorSearchSnapshotStore store) => Results.Ok(await store.GetByContentKeyAsync(key)));
app.MapPost("/acceptance/rebuild", async (IRazorSearchManagementService service, IUserService users) => Results.Ok(await service.QueuePublishedContentAsync(users.GetUserById(-1)!.Key, default)));
app.MapPost("/acceptance/mutate/{key:guid}", (Guid key, string action, string? name, string culture, IContentService service) =>
{
    var content = service.GetById(key)!;
    if (action == "publish-culture") { content.SetCultureName(name!, culture); service.Save(content); service.Publish(content, [culture]); }
    if (action == "unpublish-culture") service.Unpublish(content, culture);
    return Results.Ok(new { content.Key, content.Published });
});
app.MapPost("/acceptance/domains/{key:guid}", async (Guid key, IDomainService domains) => Results.Ok(await domains.UpdateDomainsAsync(key, new Umbraco.Cms.Core.Models.ContentEditing.DomainsUpdateModel { DefaultIsoCode = "en-US", Domains = [new() { DomainName = "http://localhost/en", IsoCode = "en-US" }, new() { DomainName = "http://localhost/da", IsoCode = "da-DK" }] })));
await Diagnostic.RunAsync(app);
sealed class SingleServerRole : IServerRoleAccessor { public ServerRole CurrentServerRole => ServerRole.Single; }

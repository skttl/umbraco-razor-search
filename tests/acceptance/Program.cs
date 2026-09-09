using Umbraco.Cms.Search.Core.DependencyInjection;
using Umbraco.Cms.Search.Provider.Examine.DependencyInjection;
using Umbraco.Cms.Core.Sync;
using Umbraco.Community.RazorSearch.Services;
using Umbraco.Community.RazorSearch.Persistence.Stores;
using Umbraco.Cms.Core.Models;
using Umbraco.Cms.Core.Services;
using Umbraco.Cms.Core.Strings;
using Umbraco.Community.RazorSearch;
using Umbraco.Community.RazorSearch.Api;
using Umbraco.Community.RazorSearch.Models;
if(args.Contains("--inspect-domain")) { var t=typeof(Umbraco.Cms.Core.Models.ContentEditing.DomainsUpdateModel);foreach(var p in t.GetProperties()){Console.WriteLine(p.Name+" "+p.PropertyType);foreach(var a in p.PropertyType.GenericTypeArguments) foreach(var v in a.GetProperties()) Console.WriteLine("  "+v.Name+" "+v.PropertyType);} return; }
var builder = WebApplication.CreateBuilder(args);
if (!builder.Configuration.GetValue<bool>("Acceptance:Enabled"))
    throw new InvalidOperationException("This test fixture requires Acceptance:Enabled=true and must only run against an isolated test database.");
builder.CreateUmbracoBuilder().AddBackOffice().AddWebsite().AddComposers().AddSearchCore().AddExamineSearchProvider().Build();
builder.Services.AddSingleton<IServerRoleAccessor>(new RoleAccessor(Enum.Parse<ServerRole>(builder.Configuration["Acceptance:Role"] ?? "Single")));
var app = builder.Build();
await app.BootUmbracoAsync();
app.Use(async (context, next) => {
    if (context.Request.Path.StartsWithSegments("/acceptance") &&
        (context.Connection.RemoteIpAddress is not { } remote || !System.Net.IPAddress.IsLoopback(remote)))
    { context.Response.StatusCode = 403; return; }
    if (context.Request.Path.StartsWithSegments("/acceptance") && HttpMethods.IsPost(context.Request.Method) &&
        !context.RequestServices.GetRequiredService<Umbraco.Cms.Core.Routing.IContentRoutingReadiness>().IsReady)
    { context.Response.StatusCode = 503; return; }
    await next(context);
});
app.Use(async (context, next) => {
    if (RenderFailure.Enabled && context.Request.Headers.ContainsKey(Constants.RenderRequestHeaderName)) { context.Response.StatusCode = 503; return; }
    await next(context);
});
app.UseUmbraco().WithMiddleware(u => {u.UseBackOffice();u.UseWebsite();}).WithEndpoints(u=>{u.UseBackOfficeEndpoints();u.UseWebsiteEndpoints();});
app.MapGet("/acceptance/health", (IServiceProvider services) => Results.Ok(new {
    role = services.GetRequiredService<IServerRoleAccessor>().CurrentServerRole.ToString(),
    queue = services.GetRequiredService<IRazorSearchRenderQueue>().GetAllStatuses().Count,
    ready = services.GetRequiredService<Umbraco.Cms.Core.Routing.IContentRoutingReadiness>().IsReady,
    bootFailed = services.GetRequiredService<IRuntimeState>().BootFailedException is not null
}));
app.MapPost("/acceptance/seed", async (IContentService contentService, IContentTypeService types, ITemplateService templates, IShortStringHelper strings, IUserService users, ILanguageService languages) => {
    var admin = users.GetUserById(-1)!;
    if (await languages.GetAsync("da-DK") is null) await languages.CreateAsync(new Language("da-DK", "Danish"), admin.Key);
    var template = await templates.GetAsync("acceptance");
    if (template is null) {
        var created = await templates.CreateAsync("Acceptance", "acceptance", "<main>pending</main>", admin.Key, null);
        template = created.Result;
        template.Content = "@inherits Umbraco.Cms.Web.Common.Views.UmbracoViewPage\n<!doctype html><html><head><title>@Model.Name</title></head><body><nav>navigationexcluded</nav><main>@Model.Name</main></body></html>";
        await templates.UpdateAsync(template, admin.Key);
    }
    var invariant = types.Get("acceptanceInvariant");
    if (invariant is null) { invariant = new ContentType(strings, -1) { Alias="acceptanceInvariant", Name="Acceptance invariant", AllowedAsRoot=true, AllowedTemplates=[template] }; invariant.SetDefaultTemplate(template); await types.CreateAsync(invariant, admin.Key); }
    var variant = types.Get("acceptanceVariant");
    if (variant is null) { variant = new ContentType(strings, -1) { Alias="acceptanceVariant", Name="Acceptance variant", AllowedAsRoot=true, Variations=ContentVariation.Culture, AllowedTemplates=[template] }; variant.SetDefaultTemplate(template); await types.CreateAsync(variant, admin.Key); }
    var inv = contentService.Create("commonword invariantonly", -1, invariant.Alias);
    contentService.Save(inv); var invPublish = contentService.Publish(inv, ["*"]);
    var vari = contentService.Create("commonword englishonly", -1, variant.Alias);
    vari.SetCultureName("commonword englishonly", "en-US"); vari.SetCultureName("commonword danishonly", "da-DK");
    contentService.Save(vari); var varPublish = contentService.Publish(vari,["en-US","da-DK"]);
    return Results.Ok(new { invariantKey=inv.Key,variantKey=vari.Key,invariantPublished=invPublish.Success,variantPublished=varPublish.Success });
});
app.MapGet("/acceptance/snapshots/{key:guid}", async (Guid key, IRazorSearchSnapshotStore store) => Results.Ok(await store.GetByContentKeyAsync(key)));
app.MapGet("/acceptance/search", async (string q, string? culture, IRazorSearchService service) => {
    var search = new RazorSearch(q); if(culture is not null) search.InCulture(culture);
    var result = await service.SearchAsync(search); return Results.Ok(new {result.Total,result.Skip,result.Take,Items=result.Items.Select(x=>new{x.ContentKey,x.Title,x.Url,x.SummaryHtml})});
});
app.MapPost("/acceptance/rebuild", async (IRazorSearchManagementService service,IUserService users) => Results.Ok(await service.QueuePublishedContentAsync(users.GetUserById(-1)!.Key,default)));
app.MapGet("/acceptance/status", async (IRazorSearchManagementService service) => Results.Ok(await service.GetQueueStatusAsync(default)));
app.MapPost("/acceptance/mutate/{key:guid}", (Guid key,string action,string? name,string? culture,IContentService service) => {
    var content=service.GetById(key)!;
    if(action=="draft") { content.Name=name!;service.Save(content); }
    if(action=="publish") { content.Name=name!;service.Save(content);service.Publish(content,["*"]); }
    if(action=="unpublish") service.Unpublish(content,"*");
    if(action=="publish-culture") { content.SetCultureName(name!,culture!);service.Save(content);service.Publish(content,[culture!]); }
    if(action=="unpublish-culture") service.Unpublish(content,culture!);
    return Results.Ok(new {content.Key,content.Published});
});
app.MapGet("/acceptance/inspect", async (IContentService contentService,IUserService users,Umbraco.Cms.Core.Web.IUmbracoContextFactory contexts,IContentPermissionService permissions) => {
    using var context=contexts.EnsureUmbracoContext(); var user=users.GetUserById(-1)!; var fetched=await users.GetAsync(user.Key);
    var items=new List<object>();
    foreach(var content in contentService.GetRootContent()) {
        var published=context.UmbracoContext.Content?.GetById(content.Key);
        items.Add(new {content.Key,content.Id,content.Name,content.Published,content.Path,content.TemplateId,PublishedExists=published is not null,Cultures=published?.Cultures.Keys,
            Permission=(await permissions.AuthorizeAccessAsync(user,[content.Key],new HashSet<string>{Umbraco.Cms.Core.Actions.ActionPublish.ActionLetter})).ToString()});
    }
    return Results.Ok(new {user.Key,user.IsApproved,user.IsLockedOut,Fetched=fetched is not null,Items=items});
});
app.MapPost("/acceptance/domains/{key:guid}",async(Guid key,IDomainService domains,IConfiguration configuration)=>Results.Ok(await domains.UpdateDomainsAsync(key,new Umbraco.Cms.Core.Models.ContentEditing.DomainsUpdateModel{DefaultIsoCode="en-US",Domains=[new(){DomainName=configuration["Acceptance:PublicBaseUrl"]+"/en",IsoCode="en-US"},new(){DomainName=configuration["Acceptance:PublicBaseUrl"]+"/da",IsoCode="da-DK"}]})));
app.MapGet("/acceptance/index", (Examine.IExamineManager examine) => {
    var index=examine.Indexes.Single(i=>i.Name=="Umb_RazorSearch"); var all=index.Searcher.CreateQuery().All().Execute();
    return Results.Ok(new {all.TotalItemCount,Items=all.Take(10).Select(x=>new{x.Id,x.Values})});
});
app.MapPost("/acceptance/bulk", (int count,IServiceScopeFactory scopes) => {
    if(count<1||count>10000)return Results.BadRequest();
    BulkState.Requested=count;BulkState.Completed=0;BulkState.Error=null;
    using (ExecutionContext.SuppressFlow()) { _=Task.Run(()=>{try{using var scope=scopes.CreateScope();var services=scope.ServiceProvider;var cs=services.GetRequiredService<IContentService>();
        for(int i=0;i<count;i++){var item=cs.Create("bulktoken item "+i,-1,"acceptanceInvariant");cs.Save(item);var published=cs.Publish(item,["*"]);if(!published.Success)throw new Exception("Publish failed "+i);Interlocked.Increment(ref BulkState.Completed);}
    }catch(Exception e){BulkState.Error=e.ToString();}}); }
    return Results.Accepted();
});
app.MapGet("/acceptance/bulk",()=>new{BulkState.Requested,BulkState.Completed,BulkState.Error});
app.MapPost("/acceptance/failure",(bool enabled)=> { RenderFailure.Enabled=enabled; return Results.Ok(new {enabled}); });
await app.RunAsync();
sealed class RoleAccessor(ServerRole role):IServerRoleAccessor {public ServerRole CurrentServerRole => role;}


static class BulkState {public static int Requested;public static int Completed;public static string? Error;}

static class RenderFailure { public static volatile bool Enabled; }



using Umbraco.Community.RazorSearch.Services;
using Umbraco.Community.RazorSearch.Models;
using Umbraco.Community.RazorSearch.Persistence.Stores;
using Microsoft.AspNetCore.TestHost;
using System.Net.Http.Json;
using System.Text.Json;

static class Diagnostic
{
    public static async Task RunAsync(WebApplication app)
    {
        using var deadline = new CancellationTokenSource(TimeSpan.FromSeconds(340));
        await app.StartAsync(deadline.Token);
        using var client = app.GetTestClient();
        client.Timeout = TimeSpan.FromSeconds(20);
        var queue = app.Services.GetRequiredService<IRazorSearchRenderQueue>();
        var store = app.Services.GetRequiredService<IRazorSearchSnapshotStore>();
        string phase = app.Configuration["phase"] ?? "seed";
        string manifestPath = Path.Combine(app.Environment.ContentRootPath, "restart-manifest.json");
        async Task Wait(Func<Task<bool>> predicate, string description)
        {
            for(int i=0;i<900;i++) { if(await predicate()) return; await Task.Delay(200,deadline.Token); }
            Console.WriteLine("DIAGNOSTIC timeout query="+await client.GetStringAsync("/acceptance/search?q=commonword",deadline.Token)); Console.WriteLine("DIAGNOSTIC timeout inspect="+await client.GetStringAsync("/acceptance/inspect",deadline.Token)); Console.WriteLine("DIAGNOSTIC timeout rawindex="+await client.GetStringAsync("/acceptance/index",deadline.Token)); throw new Exception("Timed out: " + description);
        }
        async Task<JsonElement> Post(string path)
        {
            for(int i=0;i<100;i++)
            {
                using var response=await client.PostAsync(path,null,deadline.Token);
                if(response.StatusCode==System.Net.HttpStatusCode.ServiceUnavailable) { await Task.Delay(100,deadline.Token); continue; }
                response.EnsureSuccessStatusCode();
                return await response.Content.ReadFromJsonAsync<JsonElement>(deadline.Token);
            }
            throw new Exception("Routing did not become ready: " + path);
        }
        async Task<JsonElement> Get(string path) => await client.GetFromJsonAsync<JsonElement>(path,deadline.Token);
        async Task WaitDrained() => await Wait(()=>Task.FromResult(!queue.GetAllStatuses().Any(s=>s.State is RazorSearchRenderJobState.Queued or RazorSearchRenderJobState.Running)),"queue drained");
        async Task AssertSearch()
        {
            foreach(var pair in new[] { (Culture:"",Count:1),(Culture:"da-DK",Count:2),(Culture:"en-US",Count:2) })
            {
                string path="/acceptance/search?q=commonword"+(pair.Culture.Length>0?"&culture="+pair.Culture:"");
                JsonElement search = default;
                await Wait(async () =>
                {
                    search = await Get(path);
                    return search.GetProperty("total").GetInt64() == pair.Count
                        && search.GetProperty("items").GetArrayLength() == pair.Count
                        && search.GetProperty("items").EnumerateArray().All(x =>
                            Uri.TryCreate(x.GetProperty("url").GetString(), UriKind.Absolute, out var url)
                            && url.Scheme == "http" && url.Host == "localhost"
                            && url.IsDefaultPort && url.Fragment.Length == 0);
                }, "search exact culture and public URLs " + pair.Culture);
                Console.WriteLine($"DIAGNOSTIC {phase}: verified response at {DateTimeOffset.UtcNow:O} {path} = {search.GetRawText()}");
            }
        }
        await Wait(() => Task.FromResult(app.Services.GetRequiredService<Umbraco.Cms.Core.Routing.IContentRoutingReadiness>().IsReady), "Umbraco content routing initialization");
        Console.WriteLine($"DIAGNOSTIC {phase}: Umbraco routing ready at {DateTimeOffset.UtcNow:O}");
        if(phase is "seed" or "bootstrap")
        {
            var existing=app.Services.GetRequiredService<Umbraco.Cms.Core.Services.IContentService>().GetRootContent().ToArray();
            var seed=existing.Length==0 ? await Post("/acceptance/seed") : JsonSerializer.SerializeToElement(new { invariantKey=existing.Single(x=>x.ContentType.Alias=="acceptanceInvariant").Key, variantKey=existing.Single(x=>x.ContentType.Alias=="acceptanceVariant").Key, invariantPublished=true, variantPublished=true });
            var invariant=seed.GetProperty("invariantKey").GetGuid(); var variant=seed.GetProperty("variantKey").GetGuid();
            if(!seed.GetProperty("invariantPublished").GetBoolean()||!seed.GetProperty("variantPublished").GetBoolean()) throw new Exception("Seed failed publication");
            await Post($"/acceptance/domains/{variant}");
            await Post("/acceptance/rebuild");
            await Wait(async()=> (await store.GetByContentKeyAsync(invariant)).Count==1 && (await store.GetByContentKeyAsync(variant)).Count==2,"all snapshots");
            await WaitDrained();
            await AssertSearch();
            var snapshots=(await store.GetByContentKeyAsync(invariant)).Concat(await store.GetByContentKeyAsync(variant)).ToArray();
            if(snapshots.Any(x=>x.RenderStatus!="Success"||x.RenderedAtUtc==null)) throw new Exception("Seed snapshot not successful");
            if(phase=="bootstrap")
            {
                Console.WriteLine("DIAGNOSTIC bootstrap PASS: first process published, rendered and searched all three snapshots without a restart");
                await app.StopAsync(TimeSpan.FromSeconds(10)); await app.DisposeAsync();
                return;
            }
            HoldingHandler.Hold=true;
            await Post("/acceptance/rebuild");
            await HoldingHandler.Entered.Task.WaitAsync(TimeSpan.FromSeconds(20));
            await Wait(()=>Task.FromResult(queue.GetAllStatuses().Count(s=>s.State==RazorSearchRenderJobState.Queued)>0),"pending work while one render is held");
            var pending=queue.GetAllStatuses().Where(s=>s.State is RazorSearchRenderJobState.Queued or RazorSearchRenderJobState.Running).ToArray();
            var manifest=new Manifest(invariant,variant,snapshots.Select(s=>new SavedSnapshot(s.Id,s.ContentKey,s.Culture,s.RenderedAtUtc!.Value,s.Snapshot)).ToArray(),pending.Select(s=>s.JobId).ToArray());
            await File.WriteAllTextAsync(manifestPath,JsonSerializer.Serialize(manifest),deadline.Token);
            Console.WriteLine($"DIAGNOSTIC seed PASS: {snapshots.Length} usable snapshots; exiting own process abruptly with {pending.Length} unfinished jobs ({pending.Count(s=>s.State==RazorSearchRenderJobState.Running)} running)");
            Console.Out.Flush();
            Environment.Exit(0);
        }
        else
        {
            var manifest=JsonSerializer.Deserialize<Manifest>(await File.ReadAllTextAsync(manifestPath,deadline.Token))!;
            if(queue.GetAllStatuses().Count!=0) throw new Exception("A restarted/fresh process restored in-memory jobs");
            var current=(await store.GetByContentKeyAsync(manifest.Invariant)).Concat(await store.GetByContentKeyAsync(manifest.Variant)).ToArray();
            if(current.Length != manifest.Snapshots.Length) throw new Exception("Persisted snapshot count changed during restart");
            foreach(var saved in manifest.Snapshots)
            {
                var retained=current.Single(x=>x.Id==saved.Id);
                if(retained.Snapshot!=saved.Text||retained.RenderedAtUtc!=saved.RenderedAt) throw new Exception("Persisted snapshot changed/lost during restart");
            }
            Console.WriteLine($"DIAGNOSTIC {phase}: new process queue empty, all {current.Length} persisted snapshots retain identity/text/timestamp");
            await AssertSearch();
            if(phase=="recover")
            {
                await Post("/acceptance/rebuild");
                await Wait(async()=>{var rows=(await store.GetByContentKeyAsync(manifest.Invariant)).Concat(await store.GetByContentKeyAsync(manifest.Variant)).ToArray();return rows.Length==manifest.Snapshots.Length && rows.All(x=>x.RenderStatus=="Success" && x.RenderedAtUtc>manifest.Snapshots.Single(s=>s.Id==x.Id).RenderedAt);},"manual recovery refreshed every retained snapshot");
                await WaitDrained();
                if(queue.GetAllStatuses().Any(s=>s.State==RazorSearchRenderJobState.Failed)) throw new Exception("Manual rebuild failed");
                var rows=(await store.GetByContentKeyAsync(manifest.Invariant)).Concat(await store.GetByContentKeyAsync(manifest.Variant)).ToArray();
                await File.WriteAllTextAsync(manifestPath,JsonSerializer.Serialize(manifest with { Snapshots=rows.Select(s=>new SavedSnapshot(s.Id,s.ContentKey,s.Culture,s.RenderedAtUtc!.Value,s.Snapshot)).ToArray() }),deadline.Token);
                Console.WriteLine("DIAGNOSTIC recover PASS: manual rebuild refreshed every snapshot, same identities, no failed jobs");
            }
            else if(phase=="subscriber")
            {
                if(queue.GetAllStatuses().Count!=0) throw new Exception("Subscriber enqueued rendering while rebuilding its index");
                Console.WriteLine("DIAGNOSTIC subscriber PASS: fresh index bootstrapped all three stored snapshots; rendering queue stayed empty");
            }
            else throw new Exception("Unknown diagnostic phase");
            await app.StopAsync(TimeSpan.FromSeconds(10)); await app.DisposeAsync();
        }
    }
    public sealed record SavedSnapshot(Guid Id,Guid ContentKey,string? Culture,DateTimeOffset RenderedAt,string Text);
    public sealed record Manifest(Guid Invariant,Guid Variant,SavedSnapshot[] Snapshots,Guid[] LostJobs);
}
sealed class HoldingHandler(HttpMessageHandler inner): DelegatingHandler(inner)
{
    public static volatile bool Hold;
    public static readonly TaskCompletionSource Entered=new(TaskCreationOptions.RunContinuationsAsynchronously);
    protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request,CancellationToken token)
    {
        if(Hold) { Entered.TrySetResult(); await Task.Delay(Timeout.Infinite,token); }
        return await base.SendAsync(request,token);
    }
}



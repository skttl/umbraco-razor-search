using Umbraco.Community.RazorSearch.Services;
using Umbraco.Community.RazorSearch.Models;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.AspNetCore.TestHost;

static class Diagnostic
{
    public static async Task RunAsync(WebApplication app)
    {
        using var deadline = new CancellationTokenSource(TimeSpan.FromMinutes(3));
        await app.StartAsync(deadline.Token);
        using var client = app.GetTestClient();
        client.Timeout = TimeSpan.FromSeconds(20);
        async Task<JsonElement> Post(string path)
        {
            using var response = await client.PostAsync(path, null, deadline.Token);
            response.EnsureSuccessStatusCode();
            return await response.Content.ReadFromJsonAsync<JsonElement>(deadline.Token);
        }
        async Task<JsonElement> Get(string path) => await client.GetFromJsonAsync<JsonElement>(path, deadline.Token);
        async Task Wait(Func<Task<bool>> predicate, string description)
        {
            for (var i = 0; i < 100; i++)
            {
                if (await predicate()) return;
                await Task.Delay(200, deadline.Token);
            }
            throw new Exception("Timed out: " + description);
        }
        await Wait(() => Task.FromResult(app.Services.GetRequiredService<Umbraco.Cms.Core.Routing.IContentRoutingReadiness>().IsReady),
            "Umbraco content routing ready after startup handlers");
        var seed = await Post("/acceptance/seed");
        var variant = seed.GetProperty("variantKey").GetGuid();
        if (!seed.GetProperty("invariantPublished").GetBoolean() || !seed.GetProperty("variantPublished").GetBoolean())
            throw new Exception("Seed publication failed");
        await Post($"/acceptance/domains/{variant}");
        await Post("/acceptance/rebuild");
        await Wait(async () => (await Get($"/acceptance/snapshots/{variant}")).GetArrayLength() == 2, "initial culture snapshots");
        Console.WriteLine("DIAGNOSTIC seed published and two variant snapshots persisted via TestServer HTTP rendering");
        for (var iteration = 0; iteration < 10; iteration++)
        {
            await Post("/acceptance/rebuild");
            await Post($"/acceptance/mutate/{variant}?action=unpublish-culture&culture=da-DK");
            await Wait(async () => {
                var rows = await Get($"/acceptance/snapshots/{variant}");
                return rows.GetArrayLength() == 1 && rows[0].GetProperty("culture").GetString() == "en-us";
            }, "only English snapshot remains");
            await Task.Delay(200, deadline.Token);
            var after = await Get($"/acceptance/snapshots/{variant}");
            if (after.GetArrayLength() != 1) throw new Exception("Unpublished Danish snapshot resurrected");
            Console.WriteLine($"DIAGNOSTIC concurrent rebuild/unpublish iteration {iteration + 1} completed, English retained and Danish removed");
            await Post($"/acceptance/mutate/{variant}?action=publish-culture&culture=da-DK&name=commonword%20danishonly");
            await Wait(async () => (await Get($"/acceptance/snapshots/{variant}")).GetArrayLength() == 2, "Danish republish");
        }
        var queue = app.Services.GetRequiredService<IRazorSearchRenderQueue>();
        await Wait(() => Task.FromResult(!queue.GetAllStatuses().Any(x =>
            x.State is RazorSearchRenderJobState.Queued or RazorSearchRenderJobState.Running)), "render queue drained");
        var failed = queue.GetAllStatuses().Where(x => x.State == RazorSearchRenderJobState.Failed).ToArray();
        Console.WriteLine($"DIAGNOSTIC failed render jobs: {failed.Length}");
        foreach (var failedJob in failed) Console.WriteLine($"DIAGNOSTIC job failure: {failedJob.ErrorMessage}");
        if (failed.Length != 0) throw new Exception("Concurrent operations caused failed render jobs");
        Console.WriteLine("DIAGNOSTIC PASS 10 concurrent cycles and zero failed jobs");
        await app.StopAsync(TimeSpan.FromSeconds(10));
        await app.DisposeAsync();
    }
}



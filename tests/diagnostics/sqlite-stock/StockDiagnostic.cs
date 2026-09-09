using Microsoft.AspNetCore.TestHost;
using System.Net.Http.Json;
using System.Text.Json;
using Umbraco.Cms.Core.Services;
using Umbraco.Cms.Core.Routing;

static class StockDiagnostic
{
    public static async Task RunAsync(WebApplication app)
    {
        await app.StartAsync();
        for(var i=0;i<100 && !app.Services.GetRequiredService<IContentRoutingReadiness>().IsReady;i++) await Task.Delay(200);
        if(!app.Services.GetRequiredService<IContentRoutingReadiness>().IsReady) throw new Exception("CMS startup not ready");
        using var client=app.GetTestClient();
        var response=await client.PostAsync("/acceptance/seed",null);
        response.EnsureSuccessStatusCode();
        var seed=await response.Content.ReadFromJsonAsync<JsonElement>();
        var key=seed.GetProperty("variantKey").GetGuid();
        var service=app.Services.GetRequiredService<IContentService>();
        Console.WriteLine("STOCK CMS seed published; no RazorSearch assembly or composer installed");
        for(var iteration=0;iteration<30;iteration++)
        {
            var content=service.GetById(key)!;
            var reader=Task.Run(()=> { for(var i=0;i<50;i++) service.GetPagedDescendants(content.Id,0,128,out _).ToArray(); });
            var writer=Task.Run(()=>service.Unpublish(service.GetById(key)!,"da-DK"));
            await Task.WhenAll(reader,writer).WaitAsync(TimeSpan.FromSeconds(60));
            if(!writer.Result.Success) throw new Exception("Unpublish failed");
            var current=service.GetById(key)!;
            current.SetCultureName("commonword danishonly","da-DK");
            service.Save(current);
            if(!service.Publish(current,["da-DK"]).Success)throw new Exception("Republish failed");
            Console.WriteLine($"STOCK CMS concurrent read/unpublish cycle {iteration+1} passed");
        }
        Console.WriteLine("STOCK CMS PASS");
        await app.StopAsync(TimeSpan.FromSeconds(10));
        await app.DisposeAsync();
    }
}


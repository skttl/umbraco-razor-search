using System.Net;
using System.Text;
using Microsoft.Extensions.Options;
using Moq;
using Umbraco.Cms.Core.Configuration.Models;
using Umbraco.Community.RazorSearch.Configuration;
using Umbraco.Community.RazorSearch.Models;
using Umbraco.Community.RazorSearch.Rendering;
using Umbraco.Community.RazorSearch.Services;

namespace Umbraco.Community.RazorSearch.Tests;

public sealed class HttpRazorSearchRendererTests
{
    [Fact]
    public async Task InternalDestinationPreservesPublicHostPathSchemeAndResultUrl()
    {
        var settings = new RazorSearchOptions { RenderRequestToken = "shared-test-token", HttpRenderer = new() { RenderBaseAddress = "http://127.0.0.1:5000" } };
        var handler = new StubHandler(request =>
        {
            Assert.Equal("http://127.0.0.1:5000/da/side?x=1", request.RequestUri!.ToString());
            Assert.Equal("public.example", request.Headers.Host);
            Assert.Equal("https", Assert.Single(request.Headers.GetValues(Constants.PublicSchemeHeaderName)));
            Assert.Equal("shared-test-token", Assert.Single(request.Headers.GetValues(settings.RenderRequestHeaderName)));
            Assert.True(request.Headers.CacheControl!.NoCache);
            return Html("<main>fresh</main>");
        });
        var result = await Renderer(settings, handler).RenderAsync(Job("https://public.example/da/side?x=1"));
        Assert.True(result.Success);
        Assert.Equal("https://public.example/da/side?x=1", result.FinalUrl);
        Assert.Equal("<main>fresh</main>", result.Content);
        Assert.Equal(1, handler.RequestCount);
    }

    [Theory]
    [InlineData("application/json", "{\"title\":\"wrong content\"}")]
    [InlineData("text/plain", "not html")]
    [InlineData("text/html", "")]
    public async Task SuccessfulHttpStatusDoesNotAcceptNonHtmlOrEmptyBodies(string mediaType, string body)
    {
        var handler = new StubHandler(_ => new(HttpStatusCode.OK) { Content = new StringContent(body, Encoding.UTF8, mediaType) });
        var result = await Renderer(new(), handler).RenderAsync(Job("https://public.example/"));
        Assert.False(result.Success);
        Assert.NotNull(result.ErrorMessage);
        Assert.Empty(result.Content);
    }

    [Fact]
    public async Task CrossOriginRedirectDoesNotSendAnotherAuthenticatedRequest()
    {
        var handler = new StubHandler(_ => Redirect("https://other.example/private"));
        var result = await Renderer(new() { HttpRenderer = new() { AllowAutoRedirect = true } }, handler).RenderAsync(Job("https://public.example/"));
        Assert.False(result.Success);
        Assert.Contains("outside", result.ErrorMessage);
        Assert.Equal(1, handler.RequestCount);
    }

    [Fact]
    public async Task SameOriginRedirectUsesInternalOriginAndKeepsPublicFinalUrl()
    {
        var handler = new StubHandler(request => request.RequestUri!.AbsolutePath == "/old" ? Redirect("/new") : Html("<main>new</main>"));
        var result = await Renderer(new() { HttpRenderer = new() { AllowAutoRedirect = true, RenderBaseAddress = "http://localhost:5000" } }, handler).RenderAsync(Job("https://public.example/old"));
        Assert.True(result.Success);
        Assert.Equal("https://public.example/new", result.FinalUrl);
        Assert.Equal(2, handler.RequestCount);
    }

    [Fact]
    public async Task RedirectIsDisabledByDefaultAndRedirectLoopsAreBounded()
    {
        var handler = new StubHandler(_ => Redirect("/loop"));
        var result = await Renderer(new(), handler).RenderAsync(Job("https://public.example/"));
        Assert.False(result.Success);
        Assert.Equal(1, handler.RequestCount);
        var loopHandler = new StubHandler(_ => Redirect("/loop"));
        var loop = await Renderer(new() { HttpRenderer = new() { AllowAutoRedirect = true } }, loopHandler).RenderAsync(Job("https://public.example/"));
        Assert.False(loop.Success);
        Assert.Equal(6, loopHandler.RequestCount);
        Assert.Contains("five redirects", loop.ErrorMessage);
    }

    [Fact]
    public async Task HttpFailurePreservesStatusForRetryDecisions()
    {
        var handler = new StubHandler(_ => new(HttpStatusCode.ServiceUnavailable));
        var result = await Renderer(new(), handler).RenderAsync(Job("https://public.example/"));
        Assert.False(result.Success);
        Assert.Equal(503, result.StatusCode);
    }

    private static HttpRazorSearchRenderer Renderer(RazorSearchOptions settings, StubHandler handler)
    {
        var options = new Mock<IOptionsMonitor<RazorSearchOptions>>();
        options.SetupGet(x => x.CurrentValue).Returns(settings);
        var routing = new Mock<IOptionsMonitor<WebRoutingSettings>>();
        routing.SetupGet(x => x.CurrentValue).Returns(new WebRoutingSettings());
        var token = new Mock<IRenderRequestTokenProvider>();
        token.Setup(x => x.GetToken()).Returns(settings.RenderRequestToken ?? "test-token");
        var factory = new Mock<IHttpClientFactory>();
        factory.Setup(x => x.CreateClient(HttpRazorSearchRenderer.HttpClientName)).Returns(() => new HttpClient(handler, false));
        return new(options.Object, routing.Object, token.Object, factory.Object);
    }
    private static RazorSearchRenderJob Job(string route) => new() { Id = Guid.NewGuid(), ContentKey = Guid.NewGuid(), Route = route, Renderer = "http" };
    private static HttpResponseMessage Html(string html) => new(HttpStatusCode.OK) { Content = new StringContent(html, Encoding.UTF8, "text/html") };
    private static HttpResponseMessage Redirect(string target) { var response = new HttpResponseMessage(HttpStatusCode.Found); response.Headers.Location = new Uri(target, UriKind.RelativeOrAbsolute); return response; }
    private sealed class StubHandler(Func<HttpRequestMessage, HttpResponseMessage> response) : HttpMessageHandler
    {
        public int RequestCount { get; private set; }
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        { RequestCount++; return Task.FromResult(response(request)); }
    }
}

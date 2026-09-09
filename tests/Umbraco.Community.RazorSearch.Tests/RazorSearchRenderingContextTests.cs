using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Options;
using Moq;
using Umbraco.Community.RazorSearch.Configuration;
using Umbraco.Community.RazorSearch.Middleware;
using Umbraco.Community.RazorSearch.Services;

namespace Umbraco.Community.RazorSearch.Tests;

public sealed class RazorSearchRenderingContextTests
{
    [Fact]
    public async Task AuthenticatedRenderSetsPublicSchemeAndRestoresContextOnFailure()
    {
        var context = new DefaultHttpContext();
        context.Request.Scheme = "http";
        context.Request.Headers[Constants.RenderRequestHeaderName] = "test-token";
        context.Request.Headers[Constants.PublicSchemeHeaderName] = "https";
        var middleware = Middleware(httpContext =>
        {
            Assert.True(SearchRenderingContext.IsActive);
            Assert.Equal("https", httpContext.Request.Scheme);
            Assert.Equal("no-store, no-cache", httpContext.Response.Headers.CacheControl.ToString());
            throw new InvalidOperationException("Rendering failure");
        });
        await Assert.ThrowsAsync<InvalidOperationException>(() => middleware.InvokeAsync(context));
        Assert.False(SearchRenderingContext.IsActive);
        Assert.Equal("http", context.Request.Scheme);
    }

    [Fact]
    public async Task UnauthenticatedRequestCannotSetRenderContextOrPublicScheme()
    {
        var context = new DefaultHttpContext();
        context.Request.Scheme = "http";
        context.Request.Headers[Constants.RenderRequestHeaderName] = "wrong-token";
        context.Request.Headers[Constants.PublicSchemeHeaderName] = "https";
        await Middleware(httpContext =>
        {
            Assert.False(SearchRenderingContext.IsActive);
            Assert.Equal("http", httpContext.Request.Scheme);
            Assert.Empty(httpContext.Response.Headers.CacheControl.ToString());
            return Task.CompletedTask;
        }).InvokeAsync(context);
    }

    [Fact]
    public void DefaultTokenIsStableWithinProviderAndRandomBetweenInstances()
    {
        var monitor = new Mock<IOptionsMonitor<RazorSearchOptions>>();
        var settings = new RazorSearchOptions();
        monitor.SetupGet(x => x.CurrentValue).Returns(settings);
        var first = new DefaultRenderRequestTokenProvider(monitor.Object);
        var second = new DefaultRenderRequestTokenProvider(monitor.Object);
        Assert.Equal(64, first.GetToken().Length);
        Assert.Equal(first.GetToken(), first.GetToken());
        Assert.NotEqual(first.GetToken(), second.GetToken());
        settings.RenderRequestToken = "explicit-shared-token";
        Assert.Equal("explicit-shared-token", first.GetToken());
        Assert.Equal(first.GetToken(), second.GetToken());
    }

    private static RazorSearchRenderingContextMiddleware Middleware(RequestDelegate next)
    {
        var token = new Mock<IRenderRequestTokenProvider>();
        token.Setup(x => x.GetToken()).Returns("test-token");
        return new(next, Options.Create(new RazorSearchOptions()), token.Object);
    }
}

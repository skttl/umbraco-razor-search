using System.Security.Cryptography;
using Microsoft.Extensions.Options;
using Umbraco.Community.RazorSearch.Configuration;

namespace Umbraco.Community.RazorSearch.Services;

internal sealed class DefaultRenderRequestTokenProvider(IOptionsMonitor<RazorSearchOptions> options) : IRenderRequestTokenProvider
{
    // Shared by the renderer and middleware singleton in this process. Never derive secrets from public installation metadata.
    private readonly string _processToken = Convert.ToHexString(RandomNumberGenerator.GetBytes(32));
    public string GetToken()
    {
        string? configuredToken = options.CurrentValue.RenderRequestToken;
        return string.IsNullOrWhiteSpace(configuredToken) ? _processToken : configuredToken.Trim();
    }
}

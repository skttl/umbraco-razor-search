using System.Reflection;
using System.Security.Cryptography;
using System.Text;
using Microsoft.Extensions.Options;
using Umbraco.Cms.Core.Configuration.Models;
using Umbraco.Community.RazorSearch.Configuration;

namespace Umbraco.Community.RazorSearch.Services;

internal sealed class DefaultRenderRequestTokenProvider(
    IOptionsMonitor<RazorSearchOptions> razorSearchOptions,
    IOptionsMonitor<GlobalSettings> globalSettings) : IRenderRequestTokenProvider
{
    private readonly IOptionsMonitor<RazorSearchOptions> _razorSearchOptions = razorSearchOptions;
    private readonly Lazy<string> _fallbackToken = new(
        () => CreateFallbackToken(globalSettings.CurrentValue),
        LazyThreadSafetyMode.ExecutionAndPublication);

    public string GetToken()
    {
        string? configuredToken = _razorSearchOptions.CurrentValue.RenderRequestToken;
        return string.IsNullOrWhiteSpace(configuredToken)
            ? _fallbackToken.Value
            : configuredToken.Trim();
    }

    private static string CreateFallbackToken(GlobalSettings globalSettings)
    {
        Assembly packageAssembly = typeof(DefaultRenderRequestTokenProvider).Assembly;
        Assembly entryAssembly = Assembly.GetEntryAssembly() ?? packageAssembly;

        string seed = string.Join(
            "|",
            globalSettings.Id,
            entryAssembly.GetName().Name,
            entryAssembly.GetName().Version,
            packageAssembly.GetName().Name);

        byte[] hash = SHA256.HashData(Encoding.UTF8.GetBytes(seed));
        return Convert.ToHexString(hash);
    }
}

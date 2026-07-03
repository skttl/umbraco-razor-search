using Microsoft.Extensions.Options;
using Umbraco.Community.RazorSearch.Configuration;

namespace Umbraco.Community.RazorSearch.Rendering;

internal sealed class RazorSearchRendererResolver(
    IEnumerable<IRazorSearchRenderer> renderers,
    IOptionsMonitor<RazorSearchOptions> options) : IRazorSearchRendererResolver
{
    private readonly IReadOnlyDictionary<string, IRazorSearchRenderer> _renderers = renderers.ToDictionary(
        renderer => renderer.Name,
        StringComparer.OrdinalIgnoreCase);

    private readonly IOptionsMonitor<RazorSearchOptions> _options = options;

    public IRazorSearchRenderer GetRenderer(string? rendererName = null)
    {
        string name = string.IsNullOrWhiteSpace(rendererName)
            ? _options.CurrentValue.DefaultRenderer
            : rendererName;

        if (_renderers.TryGetValue(name, out IRazorSearchRenderer? renderer))
        {
            return renderer;
        }

        throw new InvalidOperationException($"No RazorSearch renderer is registered for '{name}'.");
    }
}

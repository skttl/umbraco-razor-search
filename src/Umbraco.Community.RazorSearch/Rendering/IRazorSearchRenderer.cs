using Umbraco.Community.RazorSearch.Models;

namespace Umbraco.Community.RazorSearch.Rendering;

public interface IRazorSearchRenderer
{
    string Name { get; }

    Task<RazorSearchRenderResult> RenderAsync(RazorSearchRenderJob job, CancellationToken cancellationToken = default);
}

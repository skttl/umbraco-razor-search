namespace Umbraco.Community.RazorSearch.Rendering;

public interface IRazorSearchRendererResolver
{
    IRazorSearchRenderer GetRenderer(string? rendererName = null);
}

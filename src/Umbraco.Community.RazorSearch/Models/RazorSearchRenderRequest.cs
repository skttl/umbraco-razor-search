namespace Umbraco.Community.RazorSearch.Models;

public sealed record RazorSearchRenderRequest
{
    public required Guid ContentKey { get; init; }

    public required string Route { get; init; }

    public string? Culture { get; init; }

    public string? Segment { get; init; }

    public string? Renderer { get; init; }

    public bool Force { get; init; }
}

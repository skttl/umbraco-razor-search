namespace Umbraco.Community.RazorSearch.Api.Models;

public sealed class RazorSearchDocumentIndexEntryResponse
{
    public string? Culture { get; init; }

    public IReadOnlyCollection<string> Titles { get; init; } = [];

    public IReadOnlyCollection<string> Headings { get; init; } = [];

    public IReadOnlyCollection<string> Content { get; init; } = [];
}

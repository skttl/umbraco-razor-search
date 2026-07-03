namespace Umbraco.Community.RazorSearch.Api.Models;

public sealed class QueueRazorSearchDocumentRequest
{
    public bool IncludeDescendants { get; init; }

    public int? MaxDocuments { get; init; }
}

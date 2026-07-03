namespace Umbraco.Community.RazorSearch.Models;

public sealed record RazorSearchRenderResult
{
    public required Guid JobId { get; init; }

    public required bool Success { get; init; }

    public required string Content { get; init; }

    public string? FinalUrl { get; init; }

    public string? ContentType { get; init; }

    public int? StatusCode { get; init; }

    public string? ErrorMessage { get; init; }

    public required DateTimeOffset CompletedAtUtc { get; init; }
}

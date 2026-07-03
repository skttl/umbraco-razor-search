namespace Umbraco.Community.RazorSearch.Api.Models;

public sealed class RazorSearchQueueStatusResponse
{
    public string State { get; init; } = "idle";

    public int TotalJobCount { get; init; }

    public int PendingJobCount { get; init; }

    public int RunningJobCount { get; init; }

    public int CompletedJobCount { get; init; }

    public int FailedJobCount { get; init; }

    public int CancelledJobCount { get; init; }

    public int ProgressPercent { get; init; }

    public DateTimeOffset UpdatedAt { get; init; }

    public string? Message { get; init; }
}

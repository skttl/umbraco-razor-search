using Microsoft.Extensions.Options;
using Moq;
using Umbraco.Community.RazorSearch.Configuration;
using Umbraco.Community.RazorSearch.Models;
using Umbraco.Community.RazorSearch.Persistence.Models;
using Umbraco.Community.RazorSearch.Services;

namespace Umbraco.Community.RazorSearch.Tests;

public sealed class RenderQueueTests
{
    [Fact]
    public async Task Waiting_job_uses_latest_route_and_renderer()
    {
        var queue = Queue();
        var key = Guid.NewGuid();
        var first = await queue.EnqueueAsync(Request(key, "/old", "da-DK"));
        var second = await queue.EnqueueAsync(Request(key, "/new", "DA-dk") with { Renderer = "custom" });
        Assert.False(second.WasQueued);
        Assert.Equal(first.Job.Id, second.Job.Id);
        var dequeued = await queue.DequeueAsync(default);
        Assert.Equal("/new", dequeued.Route);
        Assert.Equal("custom", dequeued.Renderer);
        Assert.Single(queue.GetAllStatuses());
    }

    [Fact]
    public async Task Publishing_during_render_schedules_followup_and_invalidates_running_result()
    {
        var queue = Queue();
        var key = Guid.NewGuid();
        await queue.EnqueueAsync(Request(key, "/old"));
        var running = await queue.DequeueAsync(default);
        var followup = await queue.EnqueueAsync(Request(key, "/new"));
        Assert.True(followup.WasQueued);
        Assert.NotEqual(running.Id, followup.Job.Id);
        Assert.False(queue.IsCurrent(running));
        queue.MarkCompleted(running, Result(running));
        Assert.True(queue.IsCurrent(followup.Job));
        Assert.Equal("/new", (await queue.DequeueAsync(default)).Route);
    }

    [Fact]
    public async Task Culture_invalidation_leaves_other_cultures_current()
    {
        var queue = Queue();
        var key = Guid.NewGuid();
        var da = await queue.EnqueueAsync(Request(key, "/da", "da-DK"));
        var en = await queue.EnqueueAsync(Request(key, "/en", "en-US"));
        queue.Invalidate(key, "DA-dk", allCultures: false);
        Assert.False(queue.IsCurrent(da.Job));
        Assert.True(queue.IsCurrent(en.Job));
        Assert.Equal(RazorSearchRenderJobState.Cancelled, queue.GetStatuses(key).Single(s => s.JobId == da.Job.Id).State);
        Assert.Equal(en.Job.Id, (await queue.DequeueAsync(default)).Id);
    }

    [Fact]
    public async Task Persistence_failure_retains_recorded_render_attempts()
    {
        var queue = Queue();
        var key = Guid.NewGuid();
        await queue.EnqueueAsync(Request(key, "/"));
        var original = await queue.DequeueAsync(default);
        queue.MarkRunning(original with { AttemptCount = 3 });

        queue.MarkFailed(original, "Snapshot write failed");

        var status = Assert.Single(queue.GetStatuses(key));
        Assert.Equal(RazorSearchRenderJobState.Failed, status.State);
        Assert.Equal(3, status.AttemptCount);
    }

    [Fact]
    public async Task Completed_history_is_bounded_without_evicting_pending_work()
    {
        var queue = Queue(retention: 2);
        for (int i = 0; i < 5; i++)
        {
            await queue.EnqueueAsync(Request(Guid.NewGuid(), "/"));
            var job = await queue.DequeueAsync(default);
            queue.MarkCompleted(job, Result(job));
        }
        var pending = await queue.EnqueueAsync(Request(Guid.NewGuid(), "/pending"));
        var statuses = queue.GetAllStatuses();
        Assert.Equal(3, statuses.Count);
        Assert.Equal(2, statuses.Count(s => s.State == RazorSearchRenderJobState.Succeeded));
        Assert.Contains(statuses, s => s.JobId == pending.Job.Id && s.State == RazorSearchRenderJobState.Queued);
    }

    [Fact]
    public async Task Cancelled_capacity_wait_does_not_leave_active_job()
    {
        var queue = Queue(capacity: 1);
        await queue.EnqueueAsync(Request(Guid.NewGuid(), "/first"));
        using var cancelled = new CancellationTokenSource();
        var key = Guid.NewGuid();
        var waiting = queue.EnqueueAsync(Request(key, "/second"), cancelled.Token).AsTask();
        Assert.False(waiting.IsCompleted);
        cancelled.Cancel();
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => waiting);
        Assert.Equal(RazorSearchRenderJobState.Cancelled, Assert.Single(queue.GetStatuses(key)).State);
    }

    [Fact]
    public void Render_failure_preserves_successful_text_route_and_render_timestamp()
    {
        DateTimeOffset successfulAt = DateTimeOffset.Parse("2026-09-01T10:00:00Z");
        var job = new RazorSearchRenderJob { Id = Guid.NewGuid(), ContentKey = Guid.NewGuid(), Route = "/new", Renderer = "http" };
        var snapshot = new RazorSearchSnapshot { ContentKey = job.ContentKey, Route = "/old", Renderer = "custom", Snapshot = "working text",
            SnapshotHtml = "<p>working text</p>", RenderedAtUtc = successfulAt, RenderStatus = "Success" };
        var failed = Result(job) with { Success = false, ErrorMessage = "503 unavailable" };
        var kept = RazorSearchRenderQueueHostedService.PreserveSnapshotOnFailure(snapshot, job, failed);
        Assert.Equal("Success", kept.RenderStatus);
        Assert.Equal(snapshot.Snapshot, kept.Snapshot);
        Assert.Equal(snapshot.SnapshotHtml, kept.SnapshotHtml);
        Assert.Equal("/old", kept.Route);
        Assert.Equal(successfulAt, kept.RenderedAtUtc);
        Assert.Equal(failed.CompletedAtUtc, kept.LastAttemptAtUtc);
        Assert.Equal("503 unavailable", kept.LastRenderError);
    }

    [Fact]
    public void First_render_failure_has_no_successful_snapshot()
    {
        var job = new RazorSearchRenderJob { Id = Guid.NewGuid(), ContentKey = Guid.NewGuid(), Route = "/new", Renderer = "http" };
        var snapshot = RazorSearchRenderQueueHostedService.PreserveSnapshotOnFailure(null, job, Result(job) with { Success = false });
        Assert.Equal("Failed", snapshot.RenderStatus);
        Assert.Empty(snapshot.Snapshot);
        Assert.Null(snapshot.RenderedAtUtc);
        Assert.NotNull(snapshot.LastAttemptAtUtc);
    }

    private static InMemoryRazorSearchRenderQueue Queue(int capacity = 16, int retention = 1000)
    {
        var options = new Mock<IOptionsMonitor<RazorSearchOptions>>();
        options.SetupGet(x => x.CurrentValue).Returns(new RazorSearchOptions { RenderQueue = new() { Capacity = capacity, CompletedJobRetention = retention } });
        return new(options.Object, new RazorSearchQueueActivityNotifier(), new RazorSearchWorkCoordinator());
    }
    private static RazorSearchRenderRequest Request(Guid key, string route, string? culture = null) => new() { ContentKey = key, Route = route, Culture = culture };
    private static RazorSearchRenderResult Result(RazorSearchRenderJob job) => new() { JobId = job.Id, Success = true, Content = "ok", CompletedAtUtc = DateTimeOffset.UtcNow };
}

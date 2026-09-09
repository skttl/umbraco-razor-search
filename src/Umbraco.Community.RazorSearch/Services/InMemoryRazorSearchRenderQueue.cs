using System.Collections.Concurrent;
using System.Threading.Channels;
using Microsoft.Extensions.Options;
using Umbraco.Community.RazorSearch.Configuration;
using Umbraco.Community.RazorSearch.Models;

namespace Umbraco.Community.RazorSearch.Services;

internal sealed class InMemoryRazorSearchRenderQueue : IRazorSearchRenderQueue, IBackgroundRazorSearchRenderQueue
{
    private readonly Lock _sync = new();
    private readonly Dictionary<Guid, RazorSearchRenderJob> _jobs = [];
    private readonly Dictionary<Guid, RazorSearchRenderJobStatus> _statuses = [];
    private readonly Dictionary<(Guid, string), Guid> _activeJobs = [];
    private readonly Channel<Guid> _channel;
    private readonly IRazorSearchQueueActivityNotifier _notifier;
    private readonly RazorSearchOptions _options;
    private readonly RazorSearchWorkCoordinator _coordinator;
    private long _batchId;

    public InMemoryRazorSearchRenderQueue(IOptionsMonitor<RazorSearchOptions> options,
        IRazorSearchQueueActivityNotifier notifier, RazorSearchWorkCoordinator coordinator)
    {
        _options = options.CurrentValue;
        _notifier = notifier;
        _coordinator = coordinator;
        _channel = Channel.CreateBounded<Guid>(new BoundedChannelOptions(Math.Max(1, _options.RenderQueue.Capacity))
        { SingleReader = true, FullMode = BoundedChannelFullMode.Wait });
    }

    public async ValueTask<RazorSearchRenderEnqueueResult> EnqueueAsync(RazorSearchRenderRequest request, CancellationToken cancellationToken = default)
    {
        RazorSearchRenderJob job;
        RazorSearchRenderJobStatus status;
        using (await _coordinator.EnterAsync(request.ContentKey, cancellationToken))
        {
            lock (_sync)
            {
                var key = Key(request.ContentKey, request.Culture);
                if (_activeJobs.TryGetValue(key, out Guid previousId)
                    && _statuses[previousId].State == RazorSearchRenderJobState.Queued)
                {
                    // A waiting job always reads the latest request when dequeued.
                    job = _jobs[previousId] with { Route = request.Route, Renderer = request.Renderer ?? _options.DefaultRenderer };
                    _jobs[previousId] = job;
                    status = _statuses[previousId] with { Route = job.Route, Renderer = job.Renderer };
                    _statuses[previousId] = status;
                    return new() { Job = job, Status = status with { IsDuplicate = true } };
                }
                if (!_statuses.Values.Any(x => x.State is RazorSearchRenderJobState.Queued or RazorSearchRenderJobState.Running))
                    _batchId++;
                DateTimeOffset now = DateTimeOffset.UtcNow;
                job = new() { Id = Guid.NewGuid(), ContentKey = request.ContentKey, Culture = request.Culture,
                    Route = request.Route, Renderer = request.Renderer ?? _options.DefaultRenderer, EnqueuedAtUtc = now };
                status = new() { JobId = job.Id, BatchId = _batchId, ContentKey = job.ContentKey, Culture = job.Culture,
                    Route = job.Route, Renderer = job.Renderer, State = RazorSearchRenderJobState.Queued,
                    EnqueuedAtUtc = now, UpdatedAtUtc = now };
                _jobs[job.Id] = job;
                _statuses[job.Id] = status;
                // A running generation is superseded and may no longer commit its result.
                _activeJobs[key] = job.Id;
            }
        }
        try { await _channel.Writer.WriteAsync(job.Id, cancellationToken); }
        catch { MarkCancelled(job, "Enqueue was cancelled."); throw; }
        _notifier.Publish();
        return new() { Job = job, Status = status };
    }

    public async ValueTask<RazorSearchRenderJob> DequeueAsync(CancellationToken cancellationToken)
    {
        while (true)
        {
            Guid id = await _channel.Reader.ReadAsync(cancellationToken);
            lock (_sync)
            {
                if (!_jobs.TryGetValue(id, out var job) || _statuses[id].State != RazorSearchRenderJobState.Queued) continue;
                _statuses[id] = _statuses[id] with { State = RazorSearchRenderJobState.Running, StartedAtUtc = DateTimeOffset.UtcNow };
                return job;
            }
        }
    }

    public bool IsCurrent(RazorSearchRenderJob job)
    { lock (_sync) return _activeJobs.TryGetValue(Key(job.ContentKey, job.Culture), out Guid id) && id == job.Id; }

    public void Invalidate(Guid contentKey, string? culture = null, bool allCultures = true)
    {
        lock (_sync)
        {
            foreach (var pair in _activeJobs.Where(x => x.Key.Item1 == contentKey && (allCultures || x.Key == Key(contentKey, culture))).ToArray())
            {
                _activeJobs.Remove(pair.Key);
                var status = _statuses[pair.Value];
                _statuses[pair.Value] = status with { State = RazorSearchRenderJobState.Cancelled,
                    ErrorMessage = "Content changed while this job was pending.", CompletedAtUtc = DateTimeOffset.UtcNow, UpdatedAtUtc = DateTimeOffset.UtcNow };
            }
            Prune();
        }
        _notifier.Publish();
    }

    public bool TryGetStatus(Guid jobId, out RazorSearchRenderJobStatus? status)
    { lock (_sync) return _statuses.TryGetValue(jobId, out status); }
    public IReadOnlyCollection<RazorSearchRenderJobStatus> GetStatuses(Guid contentKey)
    { lock (_sync) return _statuses.Values.Where(x => x.ContentKey == contentKey).OrderByDescending(x => x.UpdatedAtUtc).ToArray(); }
    public IReadOnlyCollection<RazorSearchRenderJobStatus> GetAllStatuses()
    { lock (_sync) return _statuses.Values.OrderByDescending(x => x.UpdatedAtUtc).ToArray(); }
    public void MarkRunning(RazorSearchRenderJob job)
    {
        lock (_sync) if (_statuses.TryGetValue(job.Id, out var status))
            _statuses[job.Id] = status with { Route = job.Route, AttemptCount = job.AttemptCount, UpdatedAtUtc = DateTimeOffset.UtcNow };
        _notifier.Publish();
    }
    public void MarkCompleted(RazorSearchRenderJob job, RazorSearchRenderResult result) => Finish(job, RazorSearchRenderJobState.Succeeded, null);
    public void MarkFailed(RazorSearchRenderJob job, string errorMessage) => Finish(job, RazorSearchRenderJobState.Failed, errorMessage);
    public void MarkCancelled(RazorSearchRenderJob job, string? errorMessage = null) => Finish(job, RazorSearchRenderJobState.Cancelled, errorMessage);
    private void Finish(RazorSearchRenderJob job, RazorSearchRenderJobState state, string? error)
    {
        lock (_sync)
        {
            if (_statuses.TryGetValue(job.Id, out var status))
                _statuses[job.Id] = status with { State = state, ErrorMessage = error,
                    CompletedAtUtc = DateTimeOffset.UtcNow, UpdatedAtUtc = DateTimeOffset.UtcNow, AttemptCount = Math.Max(status.AttemptCount, job.AttemptCount) };
            var key = Key(job.ContentKey, job.Culture);
            if (_activeJobs.TryGetValue(key, out Guid id) && id == job.Id) _activeJobs.Remove(key);
            Prune();
        }
        _notifier.Publish();
    }
    private void Prune()
    {
        foreach (var status in _statuses.Values.Where(x => x.State is not (RazorSearchRenderJobState.Queued or RazorSearchRenderJobState.Running))
            .OrderByDescending(x => x.UpdatedAtUtc).Skip(_options.RenderQueue.CompletedJobRetention).ToArray())
        { _statuses.Remove(status.JobId); _jobs.Remove(status.JobId); }
    }
    private static (Guid, string) Key(Guid key, string? culture) => (key, (culture ?? string.Empty).Trim().ToUpperInvariant());
}

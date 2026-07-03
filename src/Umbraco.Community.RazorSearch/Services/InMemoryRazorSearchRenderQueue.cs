using System.Collections.Concurrent;
using System.Threading.Channels;
using Microsoft.Extensions.Options;
using Umbraco.Community.RazorSearch.Configuration;
using Umbraco.Community.RazorSearch.Models;
using Umbraco.Community.RazorSearch.Rendering;

namespace Umbraco.Community.RazorSearch.Services;

internal sealed class InMemoryRazorSearchRenderQueue : IRazorSearchRenderQueue, IBackgroundRazorSearchRenderQueue
{
    private readonly ConcurrentDictionary<Guid, RazorSearchRenderJob> _jobs = new();
    private readonly ConcurrentDictionary<Guid, RazorSearchRenderJobStatus> _statuses = new();
    private readonly ConcurrentDictionary<RazorSearchRenderDeduplicationKey, Guid> _activeJobs = new();
    private readonly Channel<RazorSearchRenderJob> _channel;
    private readonly RazorSearchOptions _options;

    public InMemoryRazorSearchRenderQueue(IOptionsMonitor<RazorSearchOptions> options)
    {
        _options = options.CurrentValue;

        _channel = _options.RenderQueue.Capacity > 0
            ? Channel.CreateBounded<RazorSearchRenderJob>(new BoundedChannelOptions(_options.RenderQueue.Capacity)
            {
                FullMode = BoundedChannelFullMode.Wait,
                SingleReader = true,
                SingleWriter = false,
            })
            : Channel.CreateUnbounded<RazorSearchRenderJob>(new UnboundedChannelOptions
            {
                SingleReader = true,
                SingleWriter = false,
            });
    }

    public async ValueTask<RazorSearchRenderEnqueueResult> EnqueueAsync(
        RazorSearchRenderRequest request,
        CancellationToken cancellationToken = default)
    {
        RazorSearchRenderDeduplicationKey deduplicationKey = RazorSearchRenderDeduplicationKey.Create(
            request.ContentKey,
            request.Culture);

        if (_options.RenderQueue.DeduplicateActiveJobs && request.Force is false)
        {
            RazorSearchRenderEnqueueResult? duplicate = TryGetDuplicate(deduplicationKey);
            if (duplicate is not null)
            {
                return duplicate;
            }
        }

        DateTimeOffset now = DateTimeOffset.UtcNow;
        RazorSearchRenderJob job = new()
        {
            Id = Guid.NewGuid(),
            ContentKey = request.ContentKey,
            Culture = request.Culture,
            Segment = request.Segment,
            Route = request.Route,
            Renderer = string.IsNullOrWhiteSpace(request.Renderer)
                ? _options.DefaultRenderer
                : request.Renderer,
            EnqueuedAtUtc = now,
        };

        if (_options.RenderQueue.DeduplicateActiveJobs
            && request.Force is false
            && _activeJobs.TryAdd(deduplicationKey, job.Id) is false)
        {
            RazorSearchRenderEnqueueResult? duplicate = TryGetDuplicate(deduplicationKey);
            if (duplicate is not null)
            {
                return duplicate;
            }
        }

        RazorSearchRenderJobStatus status = new()
        {
            JobId = job.Id,
            ContentKey = job.ContentKey,
            Culture = job.Culture,
            Segment = job.Segment,
            Renderer = job.Renderer,
            State = RazorSearchRenderJobState.Queued,
            EnqueuedAtUtc = now,
            UpdatedAtUtc = now,
            AttemptCount = job.AttemptCount,
        };

        _jobs[job.Id] = job;
        _statuses[job.Id] = status;

        try
        {
            await _channel.Writer.WriteAsync(job, cancellationToken);
        }
        catch
        {
            _jobs.TryRemove(job.Id, out _);
            _statuses.TryRemove(job.Id, out _);
            ReleaseDeduplicationKey(job);
            throw;
        }

        return new RazorSearchRenderEnqueueResult
        {
            Job = job,
            Status = status,
        };
    }

    public bool TryGetStatus(Guid jobId, out RazorSearchRenderJobStatus? status) => _statuses.TryGetValue(jobId, out status);

    public IReadOnlyCollection<RazorSearchRenderJobStatus> GetStatuses(Guid contentKey) =>
        _statuses.Values
            .Where(x => x.ContentKey == contentKey)
            .OrderByDescending(x => x.UpdatedAtUtc)
            .ToArray();

    public IReadOnlyCollection<RazorSearchRenderJobStatus> GetAllStatuses() =>
        _statuses.Values
            .OrderByDescending(x => x.UpdatedAtUtc)
            .ToArray();

    public ValueTask<RazorSearchRenderJob> DequeueAsync(CancellationToken cancellationToken) => _channel.Reader.ReadAsync(cancellationToken);

    public void MarkRunning(RazorSearchRenderJob job)
    {
        if (_statuses.TryGetValue(job.Id, out RazorSearchRenderJobStatus? status) is false)
        {
            return;
        }

        _statuses[job.Id] = status with
        {
            State = RazorSearchRenderJobState.Running,
            StartedAtUtc = DateTimeOffset.UtcNow,
            UpdatedAtUtc = DateTimeOffset.UtcNow,
            IsDuplicate = false,
        };
    }

    public void MarkCompleted(RazorSearchRenderJob job, RazorSearchRenderResult result)
    {
        if (_statuses.TryGetValue(job.Id, out RazorSearchRenderJobStatus? status))
        {
            _statuses[job.Id] = status with
            {
                State = RazorSearchRenderJobState.Succeeded,
                CompletedAtUtc = result.CompletedAtUtc,
                UpdatedAtUtc = result.CompletedAtUtc,
                ErrorMessage = null,
                IsDuplicate = false,
            };
        }

        ReleaseDeduplicationKey(job);
    }

    public void MarkFailed(RazorSearchRenderJob job, string errorMessage)
    {
        DateTimeOffset now = DateTimeOffset.UtcNow;

        if (_statuses.TryGetValue(job.Id, out RazorSearchRenderJobStatus? status))
        {
            _statuses[job.Id] = status with
            {
                State = RazorSearchRenderJobState.Failed,
                CompletedAtUtc = now,
                UpdatedAtUtc = now,
                ErrorMessage = errorMessage,
                IsDuplicate = false,
            };
        }

        ReleaseDeduplicationKey(job);
    }

    public void MarkCancelled(RazorSearchRenderJob job, string? errorMessage = null)
    {
        DateTimeOffset now = DateTimeOffset.UtcNow;

        if (_statuses.TryGetValue(job.Id, out RazorSearchRenderJobStatus? status))
        {
            _statuses[job.Id] = status with
            {
                State = RazorSearchRenderJobState.Cancelled,
                CompletedAtUtc = now,
                UpdatedAtUtc = now,
                ErrorMessage = errorMessage,
                IsDuplicate = false,
            };
        }

        ReleaseDeduplicationKey(job);
    }

    private RazorSearchRenderEnqueueResult? TryGetDuplicate(RazorSearchRenderDeduplicationKey deduplicationKey)
    {
        if (_activeJobs.TryGetValue(deduplicationKey, out Guid existingJobId) is false)
        {
            return null;
        }

        if (_jobs.TryGetValue(existingJobId, out RazorSearchRenderJob? existingJob) is false
            || _statuses.TryGetValue(existingJobId, out RazorSearchRenderJobStatus? existingStatus) is false)
        {
            _activeJobs.TryRemove(deduplicationKey, out _);
            return null;
        }

        return new RazorSearchRenderEnqueueResult
        {
            Job = existingJob,
            Status = existingStatus with
            {
                IsDuplicate = true,
                UpdatedAtUtc = DateTimeOffset.UtcNow,
            },
        };
    }

    private void ReleaseDeduplicationKey(RazorSearchRenderJob job)
    {
        RazorSearchRenderDeduplicationKey key = RazorSearchRenderDeduplicationKey.Create(job.ContentKey, job.Culture);

        if (_activeJobs.TryGetValue(key, out Guid jobId) && jobId == job.Id)
        {
            _activeJobs.TryRemove(key, out _);
        }
    }

    private readonly record struct RazorSearchRenderDeduplicationKey(Guid ContentKey, string Culture)
    {
        public static RazorSearchRenderDeduplicationKey Create(Guid contentKey, string? culture) =>
            new(contentKey, (culture ?? string.Empty).ToUpperInvariant());
    }
}

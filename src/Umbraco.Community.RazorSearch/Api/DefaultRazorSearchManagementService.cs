using Umbraco.Cms.Core;
using Umbraco.Cms.Core.Models;
using Umbraco.Cms.Core.Models.PublishedContent;
using Umbraco.Cms.Core.Services;
using Umbraco.Cms.Core.Web;
using Umbraco.Community.RazorSearch.Api.Models;
using Umbraco.Community.RazorSearch.Indexing;
using Umbraco.Community.RazorSearch.Models;
using Umbraco.Community.RazorSearch.Persistence.Models;
using Umbraco.Community.RazorSearch.Persistence.Stores;
using Umbraco.Community.RazorSearch.Services;
using Umbraco.Extensions;

namespace Umbraco.Community.RazorSearch.Api;

internal sealed class DefaultRazorSearchManagementService(
    TimeProvider timeProvider,
    IRazorSearchRenderQueue renderQueue,
    IRazorSearchSnapshotStore snapshotStore,
    IContentService contentService,
    RazorSearchRebuildQueue rebuildQueue) : IRazorSearchManagementService
{
    public Task<QueueRazorSearchDocumentResponse> QueueDocumentAsync(Guid documentId, bool includeDescendants,
        Guid userKey, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        if (contentService.GetById(documentId) is null) throw new InvalidOperationException("The document does not exist.");
        Guid operationId = rebuildQueue.Enqueue(documentId, includeDescendants, userKey);
        DateTimeOffset now = timeProvider.GetUtcNow();
        return Task.FromResult(new QueueRazorSearchDocumentResponse {
            OperationId = operationId, DocumentId = documentId, IncludeDescendants = includeDescendants,
            Scope = includeDescendants ? "documentTree" : "document", State = "accepted", QueuedAt = now,
            Message = "Rebuild accepted. Published documents are discovered in the background without a document limit.",
            Status = new() { DocumentId = documentId, State = "queued", UpdatedAt = now }
        });
    }

    public Task<QueueRazorSearchPublishedContentResponse> QueuePublishedContentAsync(
        Guid userKey, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        Guid operationId = rebuildQueue.Enqueue(null, true, userKey);
        return Task.FromResult(new QueueRazorSearchPublishedContentResponse {
            OperationId = operationId, State = "accepted", QueuedAt = timeProvider.GetUtcNow(),
            Message = "Full rebuild accepted. All published documents are discovered in the background."
        });
    }

    public async Task<RazorSearchDocumentStatusResponse> GetDocumentStatusAsync(Guid documentId, CancellationToken cancellationToken)
    {
        DateTimeOffset updatedAt = timeProvider.GetUtcNow();
        IReadOnlyCollection<RazorSearchRenderJobStatus> queueStatuses = renderQueue.GetStatuses(documentId);
        IReadOnlyCollection<Umbraco.Community.RazorSearch.Persistence.Models.RazorSearchSnapshot> snapshots =
            await snapshotStore.GetByContentKeyAsync(documentId, cancellationToken);
        string? documentName = contentService.GetById(documentId)?.Name;

        RazorSearchRenderJobStatus? latestQueueStatus = queueStatuses
            .OrderByDescending(x => x.UpdatedAtUtc)
            .FirstOrDefault();

        if (latestQueueStatus is not null)
        {
            updatedAt = latestQueueStatus.UpdatedAtUtc;
        }
        else if (snapshots.Count > 0)
        {
            updatedAt = snapshots.Max(x => x.UpdatedAtUtc);
        }

        RazorSearchQueueJobResponse[] jobs = queueStatuses
            .OrderByDescending(x => x.State == RazorSearchRenderJobState.Running)
            .ThenByDescending(x => x.UpdatedAtUtc)
            .Select(MapQueueJobResponse)
            .ToArray();

        RazorSearchDocumentSnapshotResponse[] snapshotResponses = snapshots
            .OrderByDescending(x => x.UpdatedAtUtc)
            .Select(MapSnapshotResponse)
            .ToArray();

        RazorSearchDocumentIndexEntryResponse[] indexedEntries = RazorSearchSnapshotIndexProjection
            .ProjectSuccessfulVariants(snapshots)
            .Select(MapIndexEntryResponse)
            .ToArray();

        return new RazorSearchDocumentStatusResponse
        {
            DocumentId = documentId,
            DocumentName = documentName,
            State = latestQueueStatus?.State.ToString().ToLowerInvariant()
                ?? ResolveSnapshotState(snapshots),
            PendingDocumentCount = queueStatuses.Count(x => x.State is RazorSearchRenderJobState.Queued or RazorSearchRenderJobState.Running),
            CompletedDocumentCount = snapshots.Count(x => x.RenderStatus == RazorSearchSnapshotStatuses.Success),
            FailedDocumentCount = queueStatuses.Count(x => x.State == RazorSearchRenderJobState.Failed)
                + snapshots.Count(x => x.RenderStatus == RazorSearchSnapshotStatuses.Failed),
            UpdatedAt = updatedAt,
            Message = latestQueueStatus is null && snapshots.Count == 0
                ? "No queued job or stored snapshot exists for this document yet."
                : null,
            Jobs = jobs,
            Snapshots = snapshotResponses,
            ExpectedIndexEntries = indexedEntries,
        };
    }

    public Task<RazorSearchQueueStatusResponse> GetQueueStatusAsync(CancellationToken cancellationToken)
    {
        IReadOnlyCollection<RazorSearchRenderJobStatus> allStatuses = renderQueue.GetAllStatuses();
        DateTimeOffset now = timeProvider.GetUtcNow();

        if (allStatuses.Count == 0)
        {
            return Task.FromResult(new RazorSearchQueueStatusResponse
            {
                RebuildOperations = rebuildQueue.GetStatuses(),
                State = "idle",
                UpdatedAt = now,
                Message = "No RazorSearch jobs have been queued since the application started.",
            });
        }

        QueueBatchSnapshot batchSnapshot = BuildQueueBatchSnapshot(allStatuses);

        if (batchSnapshot.ActiveStatuses.Length == 0)
        {
            int idleCompletedCount = batchSnapshot.BatchStatuses.Count(x => x.State == RazorSearchRenderJobState.Succeeded);
            int idleFailedCount = batchSnapshot.BatchStatuses.Count(x => x.State == RazorSearchRenderJobState.Failed);
            int idleCancelledCount = batchSnapshot.BatchStatuses.Count(x => x.State == RazorSearchRenderJobState.Cancelled);
            int idleTotalJobCount = batchSnapshot.BatchStatuses.Length;
            int idleFinishedCount = idleCompletedCount + idleFailedCount + idleCancelledCount;
            int idleProgressPercent = idleTotalJobCount == 0
                ? 0
                : (int)Math.Round((double)idleFinishedCount / idleTotalJobCount * 100, MidpointRounding.AwayFromZero);

            return Task.FromResult(new RazorSearchQueueStatusResponse
            {
                RebuildOperations = rebuildQueue.GetStatuses(),
                State = "idle",
                TotalJobCount = idleTotalJobCount,
                CompletedJobCount = idleCompletedCount,
                FailedJobCount = idleFailedCount,
                CancelledJobCount = idleCancelledCount,
                ProgressPercent = idleProgressPercent,
                UpdatedAt = batchSnapshot.BatchStatuses.Max(x => x.UpdatedAtUtc),
                Message = BuildIdleQueueStatusMessage(idleTotalJobCount, idleCompletedCount, idleFailedCount, idleCancelledCount),
            });
        }

        int pendingCount = batchSnapshot.BatchStatuses.Count(x => x.State == RazorSearchRenderJobState.Queued);
        int runningCount = batchSnapshot.BatchStatuses.Count(x => x.State == RazorSearchRenderJobState.Running);
        int completedCount = batchSnapshot.BatchStatuses.Count(x => x.State == RazorSearchRenderJobState.Succeeded);
        int failedCount = batchSnapshot.BatchStatuses.Count(x => x.State == RazorSearchRenderJobState.Failed);
        int cancelledCount = batchSnapshot.BatchStatuses.Count(x => x.State == RazorSearchRenderJobState.Cancelled);
        int totalJobCount = batchSnapshot.BatchStatuses.Length;
        int finishedCount = completedCount + failedCount + cancelledCount;
        int progressPercent = totalJobCount == 0
            ? 0
            : (int)Math.Round((double)finishedCount / totalJobCount * 100, MidpointRounding.AwayFromZero);

        return Task.FromResult(new RazorSearchQueueStatusResponse
        {
            RebuildOperations = rebuildQueue.GetStatuses(),
            State = runningCount > 0 ? "running" : "queued",
            TotalJobCount = totalJobCount,
            PendingJobCount = pendingCount,
            RunningJobCount = runningCount,
            CompletedJobCount = completedCount,
            FailedJobCount = failedCount,
            CancelledJobCount = cancelledCount,
            ProgressPercent = progressPercent,
            UpdatedAt = batchSnapshot.BatchStatuses.Max(x => x.UpdatedAtUtc),
            Message = BuildQueueStatusMessage(totalJobCount, pendingCount, runningCount, finishedCount),
            CurrentJob = ResolveCurrentJobResponse(batchSnapshot.ActiveStatuses),
        });
    }

    public Task<RazorSearchQueueBatchDetailsResponse> GetQueueBatchDetailsAsync(CancellationToken cancellationToken)
    {
        IReadOnlyCollection<RazorSearchRenderJobStatus> allStatuses = renderQueue.GetAllStatuses();
        DateTimeOffset now = timeProvider.GetUtcNow();

        if (allStatuses.Count == 0)
        {
            return Task.FromResult(new RazorSearchQueueBatchDetailsResponse
            {
                RebuildOperations = rebuildQueue.GetStatuses(),
                State = "idle",
                UpdatedAt = now,
                Message = "No RazorSearch jobs have been queued since the application started.",
            });
        }

        QueueBatchSnapshot batchSnapshot = BuildQueueBatchSnapshot(allStatuses);
        RazorSearchQueueJobResponse[] jobs = batchSnapshot.BatchStatuses
            .OrderBy(GetQueueStatusDisplayOrder)
            .ThenByDescending(x => x.UpdatedAtUtc)
            .Select(MapQueueJobResponse)
            .ToArray();

        int pendingCount = batchSnapshot.BatchStatuses.Count(x => x.State == RazorSearchRenderJobState.Queued);
        int runningCount = batchSnapshot.BatchStatuses.Count(x => x.State == RazorSearchRenderJobState.Running);
        int completedCount = batchSnapshot.BatchStatuses.Count(x => x.State == RazorSearchRenderJobState.Succeeded);
        int failedCount = batchSnapshot.BatchStatuses.Count(x => x.State == RazorSearchRenderJobState.Failed);
        int cancelledCount = batchSnapshot.BatchStatuses.Count(x => x.State == RazorSearchRenderJobState.Cancelled);
        int totalJobCount = batchSnapshot.BatchStatuses.Length;
        int finishedCount = completedCount + failedCount + cancelledCount;
        int progressPercent = totalJobCount == 0
            ? 0
            : (int)Math.Round((double)finishedCount / totalJobCount * 100, MidpointRounding.AwayFromZero);

        return Task.FromResult(new RazorSearchQueueBatchDetailsResponse
        {
            RebuildOperations = rebuildQueue.GetStatuses(),
            State = batchSnapshot.ActiveStatuses.Length > 0
                ? (runningCount > 0 ? "running" : "queued")
                : "idle",
            TotalJobCount = totalJobCount,
            PendingJobCount = pendingCount,
            RunningJobCount = runningCount,
            CompletedJobCount = completedCount,
            FailedJobCount = failedCount,
            CancelledJobCount = cancelledCount,
            ProgressPercent = progressPercent,
            UpdatedAt = batchSnapshot.BatchStatuses.Max(x => x.UpdatedAtUtc),
            Message = batchSnapshot.ActiveStatuses.Length > 0
                ? BuildQueueStatusMessage(totalJobCount, pendingCount, runningCount, finishedCount)
                : BuildIdleQueueStatusMessage(totalJobCount, completedCount, failedCount, cancelledCount),
            Jobs = jobs,
        });
    }

    private static string ResolveSnapshotState(IReadOnlyCollection<RazorSearchSnapshot> snapshots)
    {
        if (snapshots.Count == 0)
        {
            return "unknown";
        }

        return snapshots.Any(x => x.RenderStatus == RazorSearchSnapshotStatuses.Success)
            ? "completed"
            : "failed";
    }

    private static string BuildQueueStatusMessage(
        int totalJobCount,
        int pendingCount,
        int runningCount,
        int finishedCount)
    {
        if (runningCount > 0)
        {
            return $"{finishedCount} of {totalJobCount} queued RazorSearch job(s) have finished. {runningCount} job(s) are running and {pendingCount} are waiting.";
        }

        return $"{finishedCount} of {totalJobCount} queued RazorSearch job(s) have finished. {pendingCount} job(s) are waiting to start.";
    }

    private static string BuildIdleQueueStatusMessage(
        int totalJobCount,
        int completedCount,
        int failedCount,
        int cancelledCount)
    {
        if (totalJobCount == 0)
        {
            return "The RazorSearch queue is idle. Queue a document or run a published content rebuild to start new work.";
        }

        return $"The RazorSearch queue is idle. The last batch finished with {completedCount} completed, {failedCount} failed, and {cancelledCount} cancelled job(s).";
    }

    private static QueueBatchSnapshot BuildQueueBatchSnapshot(IReadOnlyCollection<RazorSearchRenderJobStatus> allStatuses)
    {
        RazorSearchRenderJobStatus[] activeStatuses = allStatuses
            .Where(x => x.State is RazorSearchRenderJobState.Queued or RazorSearchRenderJobState.Running)
            .ToArray();

        long currentBatchId = activeStatuses.Length > 0
            ? activeStatuses.Max(x => x.BatchId)
            : allStatuses.Max(x => x.BatchId);

        RazorSearchRenderJobStatus[] currentBatchStatuses = allStatuses
            .Where(x => x.BatchId == currentBatchId)
            .ToArray();

        return new QueueBatchSnapshot(activeStatuses, currentBatchStatuses);
    }

    private static int GetQueueStatusDisplayOrder(RazorSearchRenderJobStatus status)
        => status.State switch
        {
            RazorSearchRenderJobState.Running => 0,
            RazorSearchRenderJobState.Queued => 1,
            RazorSearchRenderJobState.Failed => 2,
            RazorSearchRenderJobState.Cancelled => 3,
            RazorSearchRenderJobState.Succeeded => 4,
            _ => 5,
        };

    private RazorSearchQueueJobResponse? ResolveCurrentJobResponse(IReadOnlyCollection<RazorSearchRenderJobStatus> activeStatuses)
    {
        RazorSearchRenderJobStatus? currentStatus = activeStatuses
            .OrderByDescending(x => x.State == RazorSearchRenderJobState.Running)
            .ThenBy(x => x.State == RazorSearchRenderJobState.Running
                ? x.StartedAtUtc ?? x.EnqueuedAtUtc
                : x.EnqueuedAtUtc)
            .FirstOrDefault();

        if (currentStatus is null)
        {
            return null;
        }

        return MapQueueJobResponse(currentStatus);
    }

    private RazorSearchQueueJobResponse MapQueueJobResponse(RazorSearchRenderJobStatus currentStatus)
    {
        string? documentName = contentService.GetById(currentStatus.ContentKey)?.Name;

        return new RazorSearchQueueJobResponse
        {
            DocumentId = currentStatus.ContentKey,
            DocumentName = documentName,
            State = currentStatus.State.ToString().ToLowerInvariant(),
            Route = currentStatus.Route,
            Renderer = currentStatus.Renderer,
            Culture = currentStatus.Culture,

            EnqueuedAt = currentStatus.EnqueuedAtUtc,
            UpdatedAt = currentStatus.UpdatedAtUtc,
            StartedAt = currentStatus.StartedAtUtc,
            CompletedAt = currentStatus.CompletedAtUtc,
            ErrorMessage = currentStatus.ErrorMessage,
        };
    }

    private static RazorSearchDocumentSnapshotResponse MapSnapshotResponse(RazorSearchSnapshot snapshot)
        => new()
        {
            Route = snapshot.Route,
            FinalUrl = snapshot.FinalUrl,
            Renderer = snapshot.Renderer,
            Culture = snapshot.Culture,

            State = snapshot.RenderStatus.ToLowerInvariant(),
            Snapshot = snapshot.Snapshot,
            SnapshotHtml = snapshot.SnapshotHtml,
            TitleText = snapshot.TitleText,
            SummaryText = snapshot.SummaryText,
            HeadingText = snapshot.HeadingText,
            BodyText = snapshot.BodyText,
            ErrorMessage = snapshot.LastRenderError,
            RenderedAt = snapshot.RenderedAtUtc,
            LastAttemptAt = snapshot.LastAttemptAtUtc,
            UpdatedAt = snapshot.UpdatedAtUtc,
        };

    private static RazorSearchDocumentIndexEntryResponse MapIndexEntryResponse(RazorSearchIndexedVariant variant)
        => new()
        {
            Culture = variant.Culture,

            Titles = variant.Titles,
            Headings = variant.Headings,
            Content = variant.Bodies,
        };

    private sealed record QueueBatchSnapshot(
        RazorSearchRenderJobStatus[] ActiveStatuses,
        RazorSearchRenderJobStatus[] BatchStatuses);
}

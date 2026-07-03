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
    IUmbracoContextFactory umbracoContextFactory) : IRazorSearchManagementService
{
    private const int DefaultMaxDocuments = 250;
    private const int MaximumAllowedDocuments = 5000;

    public async Task<QueueRazorSearchDocumentResponse> QueueDocumentAsync(
        Guid documentId,
        bool includeDescendants,
        int? maxDocuments,
        CancellationToken cancellationToken)
    {
        DateTimeOffset queuedAt = timeProvider.GetUtcNow();
        IContent rootContent = contentService.GetById(documentId)
            ?? throw new InvalidOperationException($"Could not find content with key '{documentId}'.");

        IReadOnlyCollection<IContent> contentItems = GetContentItems(rootContent, includeDescendants);
        int resolvedMaxDocuments = ResolveMaxDocuments(maxDocuments);

        using UmbracoContextReference contextReference = umbracoContextFactory.EnsureUmbracoContext();
        PublishedContentSelection selection = SelectPublishedContentItems(contextReference, contentItems, resolvedMaxDocuments);
        QueueExecutionSummary execution = await QueuePublishedContentAsync(
            selection.SelectedDocuments,
            cancellationToken);
        RazorSearchDocumentStatusResponse status = BuildQueuedStatus(
            documentId,
            includeDescendants,
            queuedAt,
            selection,
            execution);

        QueueRazorSearchDocumentResponse response = new()
        {
            DocumentId = documentId,
            Scope = includeDescendants ? "documentTree" : "document",
            IncludeDescendants = includeDescendants,
            State = ResolveOperationState(execution),
            MaxDocumentCount = resolvedMaxDocuments,
            DiscoveredDocumentCount = selection.TotalPublishedDocumentCount,
            ProcessedDocumentCount = selection.SelectedDocuments.Count,
            QueuedRouteCount = execution.QueuedRouteCount,
            DuplicateRouteCount = execution.DuplicateRouteCount,
            SkippedDocumentCount = execution.SkippedDocumentCount,
            WasTruncated = selection.WasTruncated,
            QueuedAt = queuedAt,
            Message = status.Message ?? string.Empty,
            Status = status,
        };

        return response;
    }

    public async Task<QueueRazorSearchPublishedContentResponse> QueuePublishedContentAsync(int? maxDocuments, CancellationToken cancellationToken)
    {
        DateTimeOffset queuedAt = timeProvider.GetUtcNow();
        int resolvedMaxDocuments = ResolveMaxDocuments(maxDocuments);

        using UmbracoContextReference contextReference = umbracoContextFactory.EnsureUmbracoContext();
        IReadOnlyCollection<IContent> allContentItems = GetAllContentItems();
        PublishedContentSelection selection = SelectPublishedContentItems(contextReference, allContentItems, resolvedMaxDocuments);
        QueueExecutionSummary execution = await QueuePublishedContentAsync(
            selection.SelectedDocuments,
            cancellationToken);

        return new QueueRazorSearchPublishedContentResponse
        {
            Scope = "allPublishedContent",
            State = ResolveOperationState(execution),
            MaxDocumentCount = resolvedMaxDocuments,
            DiscoveredDocumentCount = selection.TotalPublishedDocumentCount,
            ProcessedDocumentCount = selection.SelectedDocuments.Count,
            QueuedRouteCount = execution.QueuedRouteCount,
            DuplicateRouteCount = execution.DuplicateRouteCount,
            SkippedDocumentCount = execution.SkippedDocumentCount,
            WasTruncated = selection.WasTruncated,
            QueuedAt = queuedAt,
            Message = BuildPublishedContentMessage(selection, execution),
        };
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
            IndexedEntries = indexedEntries,
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

    private IReadOnlyCollection<IContent> GetContentItems(IContent rootContent, bool includeDescendants)
    {
        if (includeDescendants is false)
        {
            return [rootContent];
        }

        List<IContent> contentItems = [rootContent];
        long pageIndex = 0;
        const int pageSize = 128;

        while (true)
        {
            IEnumerable<IContent> descendants = contentService.GetPagedDescendants(
                rootContent.Id,
                pageIndex,
                pageSize,
                out long totalRecords,
                filter: null,
                ordering: Ordering.ByDefault());

            IContent[] page = descendants.ToArray();
            if (page.Length == 0)
            {
                break;
            }

            contentItems.AddRange(page);
            pageIndex++;

            if (pageIndex * pageSize >= totalRecords)
            {
                break;
            }
        }

        return contentItems;
    }

    private IReadOnlyCollection<IContent> GetAllContentItems()
    {
        List<IContent> contentItems = [];

        foreach (IContent rootContent in contentService.GetRootContent())
        {
            contentItems.AddRange(GetContentItems(rootContent, includeDescendants: true));
        }

        return contentItems
            .DistinctBy(x => x.Key)
            .ToArray();
    }

    private PublishedContentSelection SelectPublishedContentItems(
        UmbracoContextReference contextReference,
        IReadOnlyCollection<IContent> contentItems,
        int maxDocuments)
    {
        List<PublishedContentTarget> selectedDocuments = [];
        int totalPublishedDocumentCount = 0;

        foreach (IContent contentItem in contentItems.DistinctBy(x => x.Key))
        {
            IPublishedContent? publishedContent = contextReference.UmbracoContext.Content?.GetById(contentItem.Key);
            if (publishedContent is null)
            {
                continue;
            }

            totalPublishedDocumentCount++;

            if (selectedDocuments.Count >= maxDocuments)
            {
                continue;
            }

            selectedDocuments.Add(new PublishedContentTarget(contentItem, publishedContent));
        }

        return new PublishedContentSelection(
            selectedDocuments,
            totalPublishedDocumentCount,
            totalPublishedDocumentCount > selectedDocuments.Count);
    }

    private async Task<QueueExecutionSummary> QueuePublishedContentAsync(
        IReadOnlyCollection<PublishedContentTarget> contentItems,
        CancellationToken cancellationToken)
    {
        List<RazorSearchRenderJobStatus> statuses = [];
        int queuedRouteCount = 0;
        int duplicateRouteCount = 0;
        int skippedDocumentCount = 0;

        foreach (PublishedContentTarget contentItem in contentItems)
        {
            DocumentQueueSummary documentSummary = await QueuePublishedDocumentAsync(contentItem, cancellationToken);
            statuses.AddRange(documentSummary.Statuses);
            queuedRouteCount += documentSummary.QueuedRouteCount;
            duplicateRouteCount += documentSummary.DuplicateRouteCount;

            if (documentSummary.HasRoutableUrl is false)
            {
                skippedDocumentCount++;
            }
        }

        return new QueueExecutionSummary(
            statuses,
            queuedRouteCount,
            duplicateRouteCount,
            skippedDocumentCount);
    }

    private static RazorSearchDocumentStatusResponse BuildQueuedStatus(
        Guid documentId,
        bool includeDescendants,
        DateTimeOffset updatedAt,
        PublishedContentSelection selection,
        QueueExecutionSummary execution)
        => new()
        {
            DocumentId = documentId,
            State = ResolveOperationState(execution),
            IncludeDescendants = includeDescendants,
            PendingDocumentCount = execution.Statuses.Count(x => x.State is RazorSearchRenderJobState.Queued or RazorSearchRenderJobState.Running),
            CompletedDocumentCount = 0,
            FailedDocumentCount = execution.Statuses.Count(x => x.State == RazorSearchRenderJobState.Failed),
            UpdatedAt = updatedAt,
            Message = BuildDocumentMessage(includeDescendants, selection, execution),
        };

    private async Task<DocumentQueueSummary> QueuePublishedDocumentAsync(
        PublishedContentTarget contentItem,
        CancellationToken cancellationToken)
    {
        IEnumerable<string?> cultures = contentItem.PublishedContent.Cultures.Count > 0
            ? contentItem.PublishedContent.Cultures.Keys.Cast<string?>()
            : [null];

        List<RazorSearchRenderJobStatus> statuses = [];
        int queuedRouteCount = 0;
        int duplicateRouteCount = 0;
        bool hasRoutableUrl = false;

        foreach (string? culture in cultures.Distinct(StringComparer.OrdinalIgnoreCase))
        {
            string route = contentItem.PublishedContent.Url(culture, UrlMode.Absolute);
            if (string.IsNullOrWhiteSpace(route) || route.StartsWith('#'))
            {
                continue;
            }

            hasRoutableUrl = true;

            RazorSearchRenderEnqueueResult result = await renderQueue.EnqueueAsync(
                new RazorSearchRenderRequest
                {
                    ContentKey = contentItem.Content.Key,
                    Culture = culture,
                    Route = route,
                },
                cancellationToken);

            if (result.WasQueued)
            {
                queuedRouteCount++;
            }
            else
            {
                duplicateRouteCount++;
            }

            statuses.Add(result.Status);
        }

        return new DocumentQueueSummary(
            statuses,
            queuedRouteCount,
            duplicateRouteCount,
            hasRoutableUrl);
    }

    private static int ResolveMaxDocuments(int? maxDocuments)
    {
        if (maxDocuments is null or <= 0)
        {
            return DefaultMaxDocuments;
        }

        return Math.Min(maxDocuments.Value, MaximumAllowedDocuments);
    }

    private static string ResolveOperationState(QueueExecutionSummary execution)
    {
        if (execution.QueuedRouteCount > 0)
        {
            return "queued";
        }

        if (execution.DuplicateRouteCount > 0)
        {
            return "duplicate";
        }

        return "failed";
    }

    private static string BuildDocumentMessage(
        bool includeDescendants,
        PublishedContentSelection selection,
        QueueExecutionSummary execution)
    {
        if (selection.TotalPublishedDocumentCount == 0)
        {
            return "No published documents were found in the selected scope.";
        }

        if (execution.QueuedRouteCount == 0 && execution.DuplicateRouteCount > 0 && execution.SkippedDocumentCount == 0)
        {
            return "All selected RazorSearch routes were already queued.";
        }

        string scopeDescription = includeDescendants
            ? "the selected document tree"
            : "the selected document";

        string truncationMessage = selection.WasTruncated
            ? $" Processing was limited to {selection.SelectedDocuments.Count} published document(s)."
            : string.Empty;

        return $"Queued {execution.QueuedRouteCount} route(s) from {selection.SelectedDocuments.Count} published document(s) in {scopeDescription}.{truncationMessage}";
    }

    private static string BuildPublishedContentMessage(
        PublishedContentSelection selection,
        QueueExecutionSummary execution)
    {
        if (selection.TotalPublishedDocumentCount == 0)
        {
            return "No published documents were found to backfill for RazorSearch.";
        }

        if (execution.QueuedRouteCount == 0 && execution.DuplicateRouteCount > 0 && execution.SkippedDocumentCount == 0)
        {
            return "All selected published RazorSearch routes were already queued.";
        }

        string truncationMessage = selection.WasTruncated
            ? $" Processing was limited to {selection.SelectedDocuments.Count} published document(s)."
            : string.Empty;

        return $"Queued {execution.QueuedRouteCount} route(s) from {selection.SelectedDocuments.Count} published document(s) across all published content.{truncationMessage}";
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
            Segment = currentStatus.Segment,
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
            Segment = snapshot.Segment,
            State = snapshot.RenderStatus.ToLowerInvariant(),
            Snapshot = snapshot.Snapshot,
            SnapshotHtml = snapshot.SnapshotHtml,
            TitleText = snapshot.TitleText,
            SummaryText = snapshot.SummaryText,
            HeadingText = snapshot.HeadingText,
            BodyText = snapshot.BodyText,
            ErrorMessage = snapshot.LastRenderError,
            RenderedAt = snapshot.RenderedAtUtc,
            UpdatedAt = snapshot.UpdatedAtUtc,
        };

    private static RazorSearchDocumentIndexEntryResponse MapIndexEntryResponse(RazorSearchIndexedVariant variant)
        => new()
        {
            Culture = variant.Culture,
            Segment = variant.Segment,
            Titles = variant.Titles,
            Summaries = variant.Summaries,
            Headings = variant.Headings,
            Content = variant.Bodies,
        };

    private sealed record PublishedContentTarget(IContent Content, IPublishedContent PublishedContent);

    private sealed record PublishedContentSelection(
        IReadOnlyCollection<PublishedContentTarget> SelectedDocuments,
        int TotalPublishedDocumentCount,
        bool WasTruncated);

    private sealed record QueueBatchSnapshot(
        RazorSearchRenderJobStatus[] ActiveStatuses,
        RazorSearchRenderJobStatus[] BatchStatuses);

    private sealed record DocumentQueueSummary(
        IReadOnlyCollection<RazorSearchRenderJobStatus> Statuses,
        int QueuedRouteCount,
        int DuplicateRouteCount,
        bool HasRoutableUrl);

    private sealed record QueueExecutionSummary(
        IReadOnlyCollection<RazorSearchRenderJobStatus> Statuses,
        int QueuedRouteCount,
        int DuplicateRouteCount,
        int SkippedDocumentCount);
}

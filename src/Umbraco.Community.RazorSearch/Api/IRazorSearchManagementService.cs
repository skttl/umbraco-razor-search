using Umbraco.Community.RazorSearch.Api.Models;

namespace Umbraco.Community.RazorSearch.Api;

public interface IRazorSearchManagementService
{
    Task<QueueRazorSearchDocumentResponse> QueueDocumentAsync(Guid documentId, bool includeDescendants, int? maxDocuments, CancellationToken cancellationToken);

    Task<QueueRazorSearchPublishedContentResponse> QueuePublishedContentAsync(int? maxDocuments, CancellationToken cancellationToken);

    Task<RazorSearchDocumentStatusResponse> GetDocumentStatusAsync(Guid documentId, CancellationToken cancellationToken);

    Task<RazorSearchQueueStatusResponse> GetQueueStatusAsync(CancellationToken cancellationToken);

    Task<RazorSearchQueueBatchDetailsResponse> GetQueueBatchDetailsAsync(CancellationToken cancellationToken);
}

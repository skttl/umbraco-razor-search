using Umbraco.Community.RazorSearch.Persistence.Models;

namespace Umbraco.Community.RazorSearch.Persistence.Stores;

public interface IRazorSearchSnapshotStore
{
    Task<RazorSearchSnapshot?> GetAsync(Guid id, CancellationToken cancellationToken = default);

    Task<IReadOnlyCollection<RazorSearchSnapshot>> GetManyAsync(IEnumerable<Guid> ids, CancellationToken cancellationToken = default);

    Task<IReadOnlyCollection<RazorSearchSnapshot>> GetByContentKeyAsync(Guid contentKey, CancellationToken cancellationToken = default);

    Task<IReadOnlyCollection<RazorSearchSnapshot>> GetByContentKeysAsync(IEnumerable<Guid> contentKeys, CancellationToken cancellationToken = default);

    Task<IReadOnlyCollection<RazorSearchSnapshot>> SearchAsync(IEnumerable<string> terms, CancellationToken cancellationToken = default);

    Task<RazorSearchSnapshot> UpsertAsync(RazorSearchSnapshot snapshot, CancellationToken cancellationToken = default);

    Task<IReadOnlyCollection<RazorSearchSnapshot>> UpsertManyAsync(IEnumerable<RazorSearchSnapshot> snapshots, CancellationToken cancellationToken = default);

    Task<bool> DeleteAsync(Guid id, CancellationToken cancellationToken = default);

    Task<int> DeleteByContentKeyAsync(Guid contentKey, CancellationToken cancellationToken = default);
}

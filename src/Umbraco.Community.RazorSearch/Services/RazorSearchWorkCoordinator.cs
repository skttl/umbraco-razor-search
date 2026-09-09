namespace Umbraco.Community.RazorSearch.Services;

// Fixed lock stripes bound memory while making invalidation and snapshot commits atomic.
internal sealed class RazorSearchWorkCoordinator
{
    private readonly SemaphoreSlim[] _gates = Enumerable.Range(0, 64).Select(_ => new SemaphoreSlim(1, 1)).ToArray();
    public async ValueTask<IDisposable> EnterAsync(Guid key, CancellationToken cancellationToken = default)
    {
        SemaphoreSlim gate = _gates[(uint)key.GetHashCode() % (uint)_gates.Length];
        await gate.WaitAsync(cancellationToken);
        return new Lease(gate);
    }
    private sealed class Lease(SemaphoreSlim gate) : IDisposable
    {
        public void Dispose() => gate.Release();
    }
}

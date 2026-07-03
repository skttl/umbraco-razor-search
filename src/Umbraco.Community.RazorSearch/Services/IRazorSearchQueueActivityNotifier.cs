using System.Threading.Channels;

namespace Umbraco.Community.RazorSearch.Services;

internal interface IRazorSearchQueueActivityNotifier
{
    void Publish();

    IRazorSearchQueueActivitySubscription Subscribe(CancellationToken cancellationToken = default);
}

internal interface IRazorSearchQueueActivitySubscription : IAsyncDisposable
{
    ChannelReader<long> Reader { get; }
}

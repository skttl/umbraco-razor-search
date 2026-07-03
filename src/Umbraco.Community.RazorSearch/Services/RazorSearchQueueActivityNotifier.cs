using System.Collections.Concurrent;
using System.Threading.Channels;

namespace Umbraco.Community.RazorSearch.Services;

internal sealed class RazorSearchQueueActivityNotifier : IRazorSearchQueueActivityNotifier
{
    private readonly ConcurrentDictionary<Guid, Channel<long>> _subscriptions = new();
    private long _version;

    public void Publish()
    {
        long version = Interlocked.Increment(ref _version);

        foreach ((Guid subscriptionId, Channel<long> channel) in _subscriptions)
        {
            if (channel.Writer.TryWrite(version))
            {
                continue;
            }

            RemoveSubscription(subscriptionId, channel);
        }
    }

    public IRazorSearchQueueActivitySubscription Subscribe(CancellationToken cancellationToken = default)
    {
        Guid subscriptionId = Guid.NewGuid();
        Channel<long> channel = Channel.CreateUnbounded<long>(new UnboundedChannelOptions
        {
            SingleReader = true,
            SingleWriter = false,
        });

        _subscriptions[subscriptionId] = channel;

        CancellationTokenRegistration registration = cancellationToken.CanBeCanceled
            ? cancellationToken.Register(() => RemoveSubscription(subscriptionId, channel))
            : default;

        return new Subscription(channel.Reader, registration, () => RemoveSubscription(subscriptionId, channel));
    }

    private void RemoveSubscription(Guid subscriptionId, Channel<long> channel)
    {
        if (_subscriptions.TryRemove(subscriptionId, out _))
        {
            channel.Writer.TryComplete();
        }
    }

    private sealed class Subscription(
        ChannelReader<long> reader,
        CancellationTokenRegistration registration,
        Action dispose) : IRazorSearchQueueActivitySubscription
    {
        private readonly Action _dispose = dispose;
        private readonly CancellationTokenRegistration _registration = registration;
        private int _isDisposed;

        public ChannelReader<long> Reader { get; } = reader;

        public ValueTask DisposeAsync()
        {
            if (Interlocked.Exchange(ref _isDisposed, 1) == 1)
            {
                return ValueTask.CompletedTask;
            }

            _registration.Dispose();
            _dispose();
            return ValueTask.CompletedTask;
        }
    }
}

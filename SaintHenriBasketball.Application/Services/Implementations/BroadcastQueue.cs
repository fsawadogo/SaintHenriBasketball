using System.Threading.Channels;
using SaintHenriBasketball.Application.DTOs.Broadcast;

namespace SaintHenriBasketball.Application.Services.Implementations;

/// In-memory hand-off from the send request to BroadcastWorker, so emails aren't sent inside
/// the HTTP request. Broadcasts still queued when the app stops are lost.
public class BroadcastQueue
{
    private readonly Channel<QueuedBroadcast> _channel = Channel.CreateUnbounded<QueuedBroadcast>();

    public ValueTask EnqueueAsync(QueuedBroadcast broadcast) => _channel.Writer.WriteAsync(broadcast);

    public IAsyncEnumerable<QueuedBroadcast> ReadAllAsync(CancellationToken cancellationToken) =>
        _channel.Reader.ReadAllAsync(cancellationToken);
}

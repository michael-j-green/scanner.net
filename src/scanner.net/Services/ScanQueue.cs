using System.Threading.Channels;
using ScannerNet.Models;

namespace ScannerNet.Services;

public interface IScanQueue
{
    ValueTask EnqueueAsync(ScanRequest request, CancellationToken cancellationToken);
    IAsyncEnumerable<ScanRequest> ReadAllAsync(CancellationToken cancellationToken);
}

public sealed class ScanQueue : IScanQueue
{
    private readonly Channel<ScanRequest> _queue =
        Channel.CreateUnbounded<ScanRequest>(new UnboundedChannelOptions
        {
            SingleReader = true,
            SingleWriter = false,
        });

    public ValueTask EnqueueAsync(ScanRequest request, CancellationToken cancellationToken) =>
        _queue.Writer.WriteAsync(request, cancellationToken);

    public IAsyncEnumerable<ScanRequest> ReadAllAsync(CancellationToken cancellationToken) =>
        _queue.Reader.ReadAllAsync(cancellationToken);
}

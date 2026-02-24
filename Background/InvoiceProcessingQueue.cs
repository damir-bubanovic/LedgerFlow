using System.Threading.Channels;

namespace LedgerFlow.Background;

public sealed class InvoiceProcessingQueue : IInvoiceProcessingQueue
{
    private readonly Channel<Guid> _channel;

    public InvoiceProcessingQueue()
    {
        _channel = Channel.CreateUnbounded<Guid>(new UnboundedChannelOptions
        {
            SingleReader = true,
            SingleWriter = false
        });
    }

    public ValueTask EnqueueAsync(Guid invoiceId, CancellationToken ct = default)
        => _channel.Writer.WriteAsync(invoiceId, ct);

    public ValueTask<Guid> DequeueAsync(CancellationToken ct)
        => _channel.Reader.ReadAsync(ct);
}
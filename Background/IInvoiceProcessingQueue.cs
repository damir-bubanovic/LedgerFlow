namespace LedgerFlow.Background;

public interface IInvoiceProcessingQueue
{
    ValueTask EnqueueAsync(Guid invoiceId, CancellationToken ct = default);

    ValueTask<Guid> DequeueAsync(CancellationToken ct);
}
namespace LedgerFlow.Services.Extraction;

public interface IInvoiceExtractor
{
    Task<InvoiceExtractionResult> ExtractAsync(string filePath, CancellationToken ct);
}
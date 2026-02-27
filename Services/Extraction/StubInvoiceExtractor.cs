using LedgerFlow.Models.Extraction;

namespace LedgerFlow.Services.Extraction;

public sealed class StubInvoiceExtractor : IInvoiceExtractor
{
    public Task<InvoiceExtractionResult> ExtractAsync(string filePath, CancellationToken ct)
    {
        // TODO: Replace with real AI/OCR extraction (Azure/Google/OpenAI/etc.)
        // For now return deterministic placeholders.
        var fields = new List<ExtractedField>
        {
            new(InvoiceFieldType.Vendor, "Unknown Vendor", 0.10),
            new(InvoiceFieldType.InvoiceNumber, null, 0.00),
            new(InvoiceFieldType.InvoiceDate, null, 0.00),
            new(InvoiceFieldType.Subtotal, null, 0.00),
            new(InvoiceFieldType.Tax, null, 0.00),
            new(InvoiceFieldType.Total, null, 0.00),
        };

        return Task.FromResult(new InvoiceExtractionResult(fields));
    }
}
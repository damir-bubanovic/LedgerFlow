namespace LedgerFlow.Models.Invoices;

public class InvoiceDocument
{
    public Guid Id { get; set; } = Guid.NewGuid();

    public Guid InvoiceId { get; set; }

    public string OriginalFileName { get; set; } = default!;

    public string StoredFileName { get; set; } = default!;

    public DateTime UploadedAt { get; set; } = DateTime.UtcNow;

    public long FileSize { get; set; }
}
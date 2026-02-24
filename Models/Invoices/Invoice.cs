namespace LedgerFlow.Models.Invoices;

public class Invoice
{
    public Guid Id { get; set; } = Guid.NewGuid();

    public string UserId { get; set; } = default!;

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    public InvoiceStatus Status { get; set; } = InvoiceStatus.Pending;

    // Processing metadata
    public DateTime? ProcessingStartedAt { get; set; }
    public DateTime? ProcessingCompletedAt { get; set; }
    public string? ProcessingError { get; set; }

    public ICollection<InvoiceDocument> Documents { get; set; } = new List<InvoiceDocument>();
}
namespace LedgerFlow.Models.Invoices;

public class Invoice
{
    public Guid Id { get; set; } = Guid.NewGuid();

    public string UserId { get; set; } = default!;

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    public InvoiceStatus Status { get; set; } = InvoiceStatus.Pending;

    public ICollection<InvoiceDocument> Documents { get; set; } = new List<InvoiceDocument>();
}
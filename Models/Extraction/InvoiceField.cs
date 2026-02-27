namespace LedgerFlow.Models.Extraction;

public class InvoiceField
{
    public Guid Id { get; set; } = Guid.NewGuid();

    public Guid InvoiceId { get; set; }

    public InvoiceFieldType FieldType { get; set; }

    public string? Value { get; set; }

    public double Confidence { get; set; }
}
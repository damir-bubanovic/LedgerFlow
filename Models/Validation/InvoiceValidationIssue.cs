namespace LedgerFlow.Models.Validation;

public class InvoiceValidationIssue
{
    public Guid Id { get; set; } = Guid.NewGuid();

    public Guid InvoiceId { get; set; }

    public string Code { get; set; } = default!; // e.g. "TOTAL_MISSING", "TOTAL_MISMATCH"

    public string Message { get; set; } = default!;

    public string Severity { get; set; } = "Warning"; // "Warning" or "Error"

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}
using LedgerFlow.Models.Validation;

namespace LedgerFlow.Services.Validation;

public sealed record InvoiceValidationResult(IReadOnlyList<InvoiceValidationIssue> Issues)
{
    public bool HasErrors => Issues.Any(i => string.Equals(i.Severity, "Error", StringComparison.OrdinalIgnoreCase));
}
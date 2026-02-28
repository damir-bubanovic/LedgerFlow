using System.Globalization;
using LedgerFlow.Models.Extraction;
using LedgerFlow.Models.Validation;

namespace LedgerFlow.Services.Validation;

public sealed class BasicInvoiceValidator : IInvoiceValidator
{
    // Allow minor rounding differences
    private const decimal Tolerance = 0.02m;

    public InvoiceValidationResult Validate(Guid invoiceId, IReadOnlyCollection<InvoiceField> fields)
    {
        var issues = new List<InvoiceValidationIssue>();

        string? totalStr = GetValue(fields, InvoiceFieldType.Total);
        if (string.IsNullOrWhiteSpace(totalStr))
        {
            issues.Add(Error(invoiceId, "TOTAL_MISSING", "Total amount is missing."));
            return new InvoiceValidationResult(issues);
        }

        if (!TryParseMoney(totalStr, out var total))
        {
            issues.Add(Error(invoiceId, "TOTAL_INVALID", $"Total amount '{totalStr}' could not be parsed."));
            return new InvoiceValidationResult(issues);
        }

        var subtotalStr = GetValue(fields, InvoiceFieldType.Subtotal);
        var taxStr = GetValue(fields, InvoiceFieldType.Tax);

        if (!string.IsNullOrWhiteSpace(subtotalStr) && !string.IsNullOrWhiteSpace(taxStr))
        {
            if (TryParseMoney(subtotalStr, out var subtotal) && TryParseMoney(taxStr, out var tax))
            {
                var sum = subtotal + tax;
                var diff = Math.Abs(sum - total);

                if (diff > Tolerance)
                {
                    issues.Add(new InvoiceValidationIssue
                    {
                        InvoiceId = invoiceId,
                        Code = "TOTAL_MISMATCH",
                        Message = $"Subtotal + Tax ({sum}) does not match Total ({total}). Difference: {diff}.",
                        Severity = "Warning"
                    });
                }
            }
            else
            {
                issues.Add(new InvoiceValidationIssue
                {
                    InvoiceId = invoiceId,
                    Code = "SUBTOTAL_TAX_INVALID",
                    Message = "Subtotal or Tax could not be parsed; mismatch check skipped.",
                    Severity = "Warning"
                });
            }
        }

        return new InvoiceValidationResult(issues);
    }

    private static string? GetValue(IReadOnlyCollection<InvoiceField> fields, InvoiceFieldType type)
        => fields.FirstOrDefault(f => f.FieldType == type)?.Value;

    private static bool TryParseMoney(string input, out decimal value)
    {
        // Accept "123.45" and "123,45" by normalizing to invariant decimal point.
        var normalized = input.Trim()
            .Replace(" ", "")
            .Replace(",", ".");

        return decimal.TryParse(
            normalized,
            NumberStyles.Number,
            CultureInfo.InvariantCulture,
            out value);
    }

    private static InvoiceValidationIssue Error(Guid invoiceId, string code, string message) =>
        new()
        {
            InvoiceId = invoiceId,
            Code = code,
            Message = message,
            Severity = "Error"
        };
}
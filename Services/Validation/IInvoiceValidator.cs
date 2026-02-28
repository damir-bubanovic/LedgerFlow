using LedgerFlow.Models.Extraction;

namespace LedgerFlow.Services.Validation;

public interface IInvoiceValidator
{
    InvoiceValidationResult Validate(Guid invoiceId, IReadOnlyCollection<InvoiceField> fields);
}
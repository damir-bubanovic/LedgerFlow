namespace LedgerFlow.Models.Invoices;

public enum InvoiceStatus
{
    Pending = 0,
    Processing = 1,
    Processed = 2,
    Failed = 3,
    NeedsReview = 4
}
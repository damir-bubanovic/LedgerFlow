using LedgerFlow.Models.Extraction;

namespace LedgerFlow.Services.Extraction;

public sealed record InvoiceExtractionResult(
    IReadOnlyList<ExtractedField> Fields,
    string? RawText = null);

public sealed record ExtractedField(
    InvoiceFieldType FieldType,
    string? Value,
    double Confidence);
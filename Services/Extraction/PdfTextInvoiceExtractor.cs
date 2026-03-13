using System.Text;
using System.Text.RegularExpressions;
using LedgerFlow.Models.Extraction;
using UglyToad.PdfPig;

namespace LedgerFlow.Services.Extraction;

public sealed class PdfTextInvoiceExtractor : IInvoiceExtractor
{
    public Task<InvoiceExtractionResult> ExtractAsync(string filePath, CancellationToken ct)
    {
        var text = ExtractPdfText(filePath);

        var fields = new List<ExtractedField>
        {
            ExtractVendor(text),
            ExtractInvoiceNumber(text),
            ExtractInvoiceDate(text),
            ExtractSubtotal(text),
            ExtractTax(text),
            ExtractTotal(text)
        };

        return Task.FromResult(new InvoiceExtractionResult(fields, text));
    }

    private static string ExtractPdfText(string filePath)
    {
        var sb = new StringBuilder();

        using var document = PdfDocument.Open(filePath);

        foreach (var page in document.GetPages())
        {
            sb.Append(page.Text);
            sb.Append(' ');
        }

        return sb.ToString();
    }

    private static ExtractedField ExtractVendor(string text)
    {
        var value = MatchBetween(text, @"Vendor\s*:\s*", @"Invoice\s*Number\s*:");
        return value is not null
            ? new ExtractedField(InvoiceFieldType.Vendor, value, 0.95)
            : new ExtractedField(InvoiceFieldType.Vendor, "Unknown Vendor", 0.10);
    }

    private static ExtractedField ExtractInvoiceNumber(string text)
    {
        var value = MatchBetween(text, @"Invoice\s*Number\s*:\s*", @"Invoice\s*Date\s*:");
        return value is not null
            ? new ExtractedField(InvoiceFieldType.InvoiceNumber, value, 0.95)
            : new ExtractedField(InvoiceFieldType.InvoiceNumber, null, 0.00);
    }

    private static ExtractedField ExtractInvoiceDate(string text)
    {
        var value = MatchBetween(text, @"Invoice\s*Date\s*:\s*", @"Description");
        return value is not null
            ? new ExtractedField(InvoiceFieldType.InvoiceDate, value, 0.95)
            : new ExtractedField(InvoiceFieldType.InvoiceDate, null, 0.00);
    }

    private static ExtractedField ExtractSubtotal(string text)
    {
        var value = MatchBetween(text, @"Subtotal\s*:\s*", @"Tax\s*:");
        return value is not null
            ? new ExtractedField(InvoiceFieldType.Subtotal, ExtractMoney(value), 0.95)
            : new ExtractedField(InvoiceFieldType.Subtotal, null, 0.00);
    }

    private static ExtractedField ExtractTax(string text)
    {
        var value = MatchBetween(text, @"Tax\s*:\s*", @"Total\s*:");
        return value is not null
            ? new ExtractedField(InvoiceFieldType.Tax, ExtractMoney(value), 0.95)
            : new ExtractedField(InvoiceFieldType.Tax, null, 0.00);
    }

    private static ExtractedField ExtractTotal(string text)
    {
        var value = MatchAfter(text, @"(?<!Sub)Total\s*:\s*");
        return value is not null
            ? new ExtractedField(InvoiceFieldType.Total, ExtractMoney(value), 0.95)
            : new ExtractedField(InvoiceFieldType.Total, null, 0.00);
    }

    private static string? MatchBetween(string text, string startPattern, string endPattern)
    {
        var match = Regex.Match(
            text,
            startPattern + @"(.*?)" + endPattern,
            RegexOptions.IgnoreCase | RegexOptions.Singleline);

        if (!match.Success)
            return null;

        return CleanValue(match.Groups[1].Value);
    }

    private static string? MatchAfter(string text, string startPattern)
    {
        var match = Regex.Match(
            text,
            startPattern + @"(.*)$",
            RegexOptions.IgnoreCase | RegexOptions.Singleline);

        if (!match.Success)
            return null;

        return CleanValue(match.Groups[1].Value);
    }

    private static string CleanValue(string value)
    {
        return Regex.Replace(value, @"\s+", " ").Trim();
    }

    private static string? ExtractMoney(string value)
    {
        var match = Regex.Match(value, @"[0-9]+(?:[.,][0-9]{2})?");
        return match.Success ? match.Value : null;
    }
}
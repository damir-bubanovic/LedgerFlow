using LedgerFlow.Data;
using LedgerFlow.Models.Extraction;
using LedgerFlow.Models.Invoices;
using LedgerFlow.Models.Validation;
using LedgerFlow.Options;
using LedgerFlow.Services.Extraction;
using LedgerFlow.Services.Validation;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace LedgerFlow.Background;

public sealed class InvoiceProcessingWorker : BackgroundService
{
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ILogger<InvoiceProcessingWorker> _logger;
    private readonly IInvoiceProcessingQueue _queue;
    private readonly IInvoiceExtractor _extractor;
    private readonly IInvoiceValidator _validator;
    private readonly StorageOptions _storage;

    // MVP rule: if any required field is missing or below this confidence => NeedsReview
    private const double ReviewThreshold = 0.80;

    private static readonly InvoiceFieldType[] RequiredFields =
    {
        InvoiceFieldType.Vendor,
        InvoiceFieldType.InvoiceDate,
        InvoiceFieldType.Total
    };

    public InvoiceProcessingWorker(
        IServiceScopeFactory scopeFactory,
        ILogger<InvoiceProcessingWorker> logger,
        IInvoiceProcessingQueue queue,
        IInvoiceExtractor extractor,
        IInvoiceValidator validator,
        IOptions<StorageOptions> storageOptions)
    {
        _scopeFactory = scopeFactory;
        _logger = logger;
        _queue = queue;
        _extractor = extractor;
        _validator = validator;
        _storage = storageOptions.Value;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("InvoiceProcessingWorker started.");

        while (!stoppingToken.IsCancellationRequested)
        {
            Guid invoiceId;

            try
            {
                invoiceId = await _queue.DequeueAsync(stoppingToken);
            }
            catch (OperationCanceledException)
            {
                break;
            }

            try
            {
                await ProcessInvoiceAsync(invoiceId, stoppingToken);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Unhandled error while processing invoice {InvoiceId}", invoiceId);
            }
        }

        _logger.LogInformation("InvoiceProcessingWorker stopped.");
    }

    private async Task ProcessInvoiceAsync(Guid invoiceId, CancellationToken ct)
    {
        using var scope = _scopeFactory.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();

        var invoice = await db.Invoices
            .Include(i => i.Documents)
            .FirstOrDefaultAsync(i => i.Id == invoiceId, ct);

        if (invoice is null)
        {
            _logger.LogWarning("Invoice {InvoiceId} not found.", invoiceId);
            return;
        }

        if (invoice.Status != InvoiceStatus.Pending)
        {
            _logger.LogInformation("Invoice {InvoiceId} skipped (status: {Status}).", invoiceId, invoice.Status);
            return;
        }

        invoice.Status = InvoiceStatus.Processing;
        invoice.ProcessingStartedAt = DateTime.UtcNow;
        invoice.ProcessingCompletedAt = null;
        invoice.ProcessingError = null;
        await db.SaveChangesAsync(ct);

        try
        {
            var doc = invoice.Documents
                .OrderByDescending(d => d.UploadedAt)
                .FirstOrDefault();

            if (doc is null)
                throw new InvalidOperationException("No document attached to invoice.");

            var filePath = Path.Combine(_storage.UploadsPath, doc.StoredFileName);
            if (!File.Exists(filePath))
                throw new FileNotFoundException("Stored invoice file not found.", filePath);

            // Extract fields (stub for now)
            var extraction = await _extractor.ExtractAsync(filePath, ct);

            // Remove existing fields (idempotent reprocessing)
            var existingFields = await db.InvoiceFields
                .Where(f => f.InvoiceId == invoice.Id)
                .ToListAsync(ct);

            if (existingFields.Count > 0)
                db.InvoiceFields.RemoveRange(existingFields);

            // Remove existing validation issues (idempotent)
            var existingIssues = await db.InvoiceValidationIssues
                .Where(v => v.InvoiceId == invoice.Id)
                .ToListAsync(ct);

            if (existingIssues.Count > 0)
                db.InvoiceValidationIssues.RemoveRange(existingIssues);

            // Persist extracted fields
            var persistedFields = new List<InvoiceField>();
            foreach (var f in extraction.Fields)
            {
                var entity = new InvoiceField
                {
                    InvoiceId = invoice.Id,
                    FieldType = f.FieldType,
                    Value = f.Value,
                    Confidence = f.Confidence
                };

                persistedFields.Add(entity);
                db.InvoiceFields.Add(entity);
            }

            // Validate extracted fields
            var validation = _validator.Validate(invoice.Id, persistedFields);

            if (validation.Issues.Count > 0)
                db.InvoiceValidationIssues.AddRange(validation.Issues);

            // Decide status: validation errors always force NeedsReview
            var byType = extraction.Fields.ToDictionary(x => x.FieldType, x => x);

            var needsReviewByConfidence = RequiredFields.Any(t =>
                !byType.TryGetValue(t, out var field) ||
                string.IsNullOrWhiteSpace(field.Value) ||
                field.Confidence < ReviewThreshold);

            var needsReview = validation.HasErrors || needsReviewByConfidence;

            invoice.Status = needsReview ? InvoiceStatus.NeedsReview : InvoiceStatus.Processed;
            invoice.ProcessingCompletedAt = DateTime.UtcNow;

            await db.SaveChangesAsync(ct);

            _logger.LogInformation(
                "Invoice {InvoiceId} extracted+validated. Status={Status}. Issues={IssueCount}",
                invoiceId, invoice.Status, validation.Issues.Count);
        }
        catch (Exception ex)
        {
            invoice.Status = InvoiceStatus.Failed;
            invoice.ProcessingCompletedAt = DateTime.UtcNow;
            invoice.ProcessingError = ex.Message;

            await db.SaveChangesAsync(ct);

            _logger.LogError(ex, "Invoice {InvoiceId} failed.", invoiceId);
        }
    }
}
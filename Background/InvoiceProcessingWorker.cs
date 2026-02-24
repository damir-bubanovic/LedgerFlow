using LedgerFlow.Data;
using LedgerFlow.Models.Invoices;
using Microsoft.EntityFrameworkCore;

namespace LedgerFlow.Background;

public sealed class InvoiceProcessingWorker : BackgroundService
{
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ILogger<InvoiceProcessingWorker> _logger;
    private readonly IInvoiceProcessingQueue _queue;

    public InvoiceProcessingWorker(
        IServiceScopeFactory scopeFactory,
        ILogger<InvoiceProcessingWorker> logger,
        IInvoiceProcessingQueue queue)
    {
        _scopeFactory = scopeFactory;
        _logger = logger;
        _queue = queue;
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
            .FirstOrDefaultAsync(i => i.Id == invoiceId, ct);

        if (invoice is null)
        {
            _logger.LogWarning("Invoice {InvoiceId} not found.", invoiceId);
            return;
        }

        // Only process pending invoices
        if (invoice.Status != InvoiceStatus.Pending)
        {
            _logger.LogInformation("Invoice {InvoiceId} skipped (status: {Status}).", invoiceId, invoice.Status);
            return;
        }

        invoice.Status = InvoiceStatus.Processing;
        invoice.ProcessingStartedAt = DateTime.UtcNow;
        invoice.ProcessingError = null;
        await db.SaveChangesAsync(ct);

        try
        {
            // TODO (Chapter 5): AI extraction + validation.
            // For now, simulate work.
            await Task.Delay(TimeSpan.FromSeconds(1), ct);

            invoice.Status = InvoiceStatus.Processed;
            invoice.ProcessingCompletedAt = DateTime.UtcNow;

            await db.SaveChangesAsync(ct);

            _logger.LogInformation("Invoice {InvoiceId} processed.", invoiceId);
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
using LedgerFlow.Background;
using LedgerFlow.Data;
using LedgerFlow.Models.Invoices;
using LedgerFlow.Options;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Options;

namespace LedgerFlow.Controllers;

[ApiController]
[Route("api/invoices")]
[Authorize]
public class InvoicesController : ControllerBase
{
    private static readonly HashSet<string> AllowedContentTypes = new(StringComparer.OrdinalIgnoreCase)
    {
        "application/pdf",
        "image/jpeg",
        "image/png"
    };

    private readonly ApplicationDbContext _db;
    private readonly UserManager<LedgerFlow.Models.ApplicationUser> _userManager;
    private readonly StorageOptions _storage;
    private readonly IInvoiceProcessingQueue _queue;

    public InvoicesController(
        ApplicationDbContext db,
        UserManager<LedgerFlow.Models.ApplicationUser> userManager,
        IOptions<StorageOptions> storageOptions,
        IInvoiceProcessingQueue queue)
    {
        _db = db;
        _userManager = userManager;
        _storage = storageOptions.Value;
        _queue = queue;
    }

    [HttpPost("upload")]
    [RequestSizeLimit(long.MaxValue)]
    public async Task<ActionResult<UploadInvoiceResponse>> Upload([FromForm] IFormFile file, CancellationToken ct)
    {
        if (file is null || file.Length <= 0)
            return BadRequest("File is required.");

        if (file.Length > _storage.MaxUploadBytes)
            return BadRequest($"File is too large. Max allowed is {_storage.MaxUploadBytes} bytes.");

        if (!AllowedContentTypes.Contains(file.ContentType))
            return BadRequest("Unsupported file type. Allowed: PDF, JPG, PNG.");


        
        var userId = _userManager.GetUserId(User);
        if (string.IsNullOrWhiteSpace(userId))
            return Unauthorized();
        
        // var userId = _userManager.GetUserId(User) ?? "DEV_VALIDATION_TEST";



        Directory.CreateDirectory(_storage.UploadsPath);

        var extension = Path.GetExtension(file.FileName);
        if (string.IsNullOrWhiteSpace(extension))
        {
            extension = file.ContentType.Equals("application/pdf", StringComparison.OrdinalIgnoreCase) ? ".pdf"
                : file.ContentType.Equals("image/jpeg", StringComparison.OrdinalIgnoreCase) ? ".jpg"
                : ".png";
        }

        var storedFileName = $"{Guid.NewGuid():N}{extension}";
        var storedPath = Path.Combine(_storage.UploadsPath, storedFileName);

        await using (var stream = System.IO.File.Create(storedPath))
        {
            await file.CopyToAsync(stream, ct);
        }

        var invoice = new Invoice
        {
            UserId = userId,
            Status = InvoiceStatus.Pending
        };

        var doc = new InvoiceDocument
        {
            InvoiceId = invoice.Id,
            OriginalFileName = Path.GetFileName(file.FileName),
            StoredFileName = storedFileName,
            FileSize = file.Length
        };

        invoice.Documents.Add(doc);

        _db.Invoices.Add(invoice);
        await _db.SaveChangesAsync(ct);

        // Enqueue for background processing
        await _queue.EnqueueAsync(invoice.Id, ct);

        return Ok(new UploadInvoiceResponse(invoice.Id, doc.Id));
    }
}

public sealed record UploadInvoiceResponse(Guid InvoiceId, Guid DocumentId);
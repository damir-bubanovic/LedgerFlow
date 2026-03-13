using LedgerFlow.Background;
using LedgerFlow.Data;
using LedgerFlow.Models.Extraction;
using LedgerFlow.Models.Invoices;
using LedgerFlow.Options;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace LedgerFlow.Controllers;

[ApiController]
[Route("api/invoices")]
[Authorize]
public class InvoicesController : ControllerBase
{
    private static readonly HashSet<string> AllowedContentTypes = new(StringComparer.OrdinalIgnoreCase)
    {
        "application/pdf"
    };

    private readonly ApplicationDbContext _db;
    private readonly UserManager<LedgerFlow.Models.ApplicationUser> _userManager;
    private readonly StorageOptions _storage;
    private readonly IInvoiceProcessingQueue _queue;
    private readonly IWebHostEnvironment _environment;

    public InvoicesController(
        ApplicationDbContext db,
        UserManager<LedgerFlow.Models.ApplicationUser> userManager,
        IOptions<StorageOptions> storageOptions,
        IInvoiceProcessingQueue queue,
        IWebHostEnvironment environment)
    {
        _db = db;
        _userManager = userManager;
        _storage = storageOptions.Value;
        _queue = queue;
        _environment = environment;
    }

    [HttpPost("upload")]
    [RequestSizeLimit(long.MaxValue)]
    public async Task<IActionResult> Upload([FromForm] IFormFile? file, CancellationToken ct)
    {
        var acceptsHtml = Request.Headers.Accept.Any(h =>
            h?.Contains("text/html", StringComparison.OrdinalIgnoreCase) == true);

        if (file is null || file.Length <= 0)
        {
            if (acceptsHtml)
                return Redirect("/invoices/upload?error=Please select a PDF file before uploading.");

            return BadRequest(new { error = "The file field is required." });
        }

        if (file.Length > _storage.MaxUploadBytes)
        {
            if (acceptsHtml)
                return Redirect("/invoices/upload?error=File is too large.");

            return BadRequest(new { error = $"File is too large. Max allowed is {_storage.MaxUploadBytes} bytes." });
        }

        var extension = Path.GetExtension(file.FileName);

        var isPdfContentType = AllowedContentTypes.Contains(file.ContentType);
        var isPdfExtension = string.Equals(extension, ".pdf", StringComparison.OrdinalIgnoreCase);

        if (!isPdfContentType || !isPdfExtension)
        {
            if (acceptsHtml)
                return Redirect("/invoices/upload?error=Unsupported file type. Only PDF files are allowed.");

            return BadRequest(new { error = "Unsupported file type. Only PDF files are allowed." });
        }

        var userId = _userManager.GetUserId(User);
        if (string.IsNullOrWhiteSpace(userId))
            return Unauthorized();

        var uploadsRoot = GetUploadsRootPath();
        Directory.CreateDirectory(uploadsRoot);

        var storedFileName = $"{Guid.NewGuid():N}.pdf";
        var storedPath = Path.Combine(uploadsRoot, storedFileName);

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

        await _queue.EnqueueAsync(invoice.Id, ct);

        if (acceptsHtml)
            return Redirect($"/invoices/{invoice.Id}");

        return Ok(new UploadInvoiceResponse(invoice.Id, doc.Id));
    }

    [HttpGet]
    public async Task<ActionResult<IReadOnlyList<InvoiceListItemDto>>> List([FromQuery] string? status, CancellationToken ct)
    {
        var userId = _userManager.GetUserId(User);
        if (string.IsNullOrWhiteSpace(userId))
            return Unauthorized();

        InvoiceStatus? parsedStatus = null;
        if (!string.IsNullOrWhiteSpace(status) && Enum.TryParse<InvoiceStatus>(status, ignoreCase: true, out var s))
            parsedStatus = s;

        var invoicesQuery = _db.Invoices.AsNoTracking()
            .Where(i => i.UserId == userId);

        if (parsedStatus is not null)
            invoicesQuery = invoicesQuery.Where(i => i.Status == parsedStatus.Value);

        var invoices = await invoicesQuery
            .OrderByDescending(i => i.CreatedAt)
            .Take(200)
            .ToListAsync(ct);

        var invoiceIds = invoices.Select(i => i.Id).ToList();

        var fields = await _db.InvoiceFields.AsNoTracking()
            .Where(f => invoiceIds.Contains(f.InvoiceId))
            .ToListAsync(ct);

        string? Pick(Guid invoiceId, InvoiceFieldType type) =>
            fields.FirstOrDefault(f => f.InvoiceId == invoiceId && f.FieldType == type)?.Value;

        var results = invoices.Select(i => new InvoiceListItemDto(
            i.Id,
            i.CreatedAt,
            i.Status.ToString(),
            Vendor: Pick(i.Id, InvoiceFieldType.Vendor),
            InvoiceDate: Pick(i.Id, InvoiceFieldType.InvoiceDate),
            Total: Pick(i.Id, InvoiceFieldType.Total)
        )).ToList();

        return Ok(results);
    }

    [HttpGet("{invoiceId:guid}")]
    public async Task<ActionResult<InvoiceDetailDto>> Get(Guid invoiceId, CancellationToken ct)
    {
        var userId = _userManager.GetUserId(User);
        if (string.IsNullOrWhiteSpace(userId))
            return Unauthorized();

        var invoice = await _db.Invoices.AsNoTracking()
            .Include(i => i.Documents)
            .FirstOrDefaultAsync(i => i.Id == invoiceId && i.UserId == userId, ct);

        if (invoice is null)
            return NotFound();

        var fields = await _db.InvoiceFields.AsNoTracking()
            .Where(f => f.InvoiceId == invoiceId)
            .OrderBy(f => f.FieldType)
            .ToListAsync(ct);

        var issues = await _db.InvoiceValidationIssues.AsNoTracking()
            .Where(v => v.InvoiceId == invoiceId)
            .OrderByDescending(v => v.CreatedAt)
            .ToListAsync(ct);

        var docs = invoice.Documents
            .OrderByDescending(d => d.UploadedAt)
            .Select(d => new InvoiceDocumentDto(
                d.Id,
                d.OriginalFileName,
                d.UploadedAt,
                d.FileSize,
                PreviewUrl: $"/api/invoices/{invoiceId}/documents/{d.Id}"
            ))
            .ToList();

        var dto = new InvoiceDetailDto(
            invoice.Id,
            invoice.CreatedAt,
            invoice.Status.ToString(),
            invoice.ProcessingStartedAt,
            invoice.ProcessingCompletedAt,
            invoice.ProcessingError,
            Documents: docs,
            Fields: fields.Select(f => new InvoiceFieldDto(
                f.FieldType.ToString(),
                f.Value,
                f.Confidence
            )).ToList(),
            ValidationIssues: issues.Select(v => new InvoiceValidationIssueDto(
                v.Code,
                v.Message,
                v.Severity,
                v.CreatedAt
            )).ToList()
        );

        return Ok(dto);
    }

    [HttpGet("{invoiceId:guid}/documents/{documentId:guid}")]
    public async Task<IActionResult> GetDocument(Guid invoiceId, Guid documentId, CancellationToken ct)
    {
        var userId = _userManager.GetUserId(User);
        if (string.IsNullOrWhiteSpace(userId))
            return Unauthorized();

        var invoice = await _db.Invoices.AsNoTracking()
            .Include(i => i.Documents)
            .FirstOrDefaultAsync(i => i.Id == invoiceId && i.UserId == userId, ct);

        if (invoice is null)
            return NotFound();

        var doc = invoice.Documents.FirstOrDefault(d => d.Id == documentId);
        if (doc is null)
            return NotFound();

        var filePath = Path.Combine(GetUploadsRootPath(), doc.StoredFileName);

        Console.WriteLine($"[LedgerFlow] Document path: {filePath}");
        Console.WriteLine($"[LedgerFlow] File exists: {System.IO.File.Exists(filePath)}");

        if (!System.IO.File.Exists(filePath))
            return NotFound();

        var contentType = GetContentTypeFromExtension(Path.GetExtension(doc.StoredFileName));

        Response.Headers["Content-Disposition"] = $"inline; filename=\"{doc.OriginalFileName}\"";

        return PhysicalFile(filePath, contentType, enableRangeProcessing: true);
    }

    private string GetUploadsRootPath()
    {
        string path;

        if (Path.IsPathRooted(_storage.UploadsPath))
            path = _storage.UploadsPath;
        else
            path = Path.GetFullPath(Path.Combine(_environment.ContentRootPath, _storage.UploadsPath));

        Console.WriteLine($"[LedgerFlow] Uploads path: {path}");

        return path;
    }

    private static string GetContentTypeFromExtension(string? ext)
    {
        ext = (ext ?? "").ToLowerInvariant();
        return ext switch
        {
            ".pdf" => "application/pdf",
            _ => "application/octet-stream"
        };
    }
}

public sealed record UploadInvoiceResponse(Guid InvoiceId, Guid DocumentId);

public sealed record InvoiceListItemDto(
    Guid Id,
    DateTime CreatedAt,
    string Status,
    string? Vendor,
    string? InvoiceDate,
    string? Total);

public sealed record InvoiceDetailDto(
    Guid Id,
    DateTime CreatedAt,
    string Status,
    DateTime? ProcessingStartedAt,
    DateTime? ProcessingCompletedAt,
    string? ProcessingError,
    IReadOnlyList<InvoiceDocumentDto> Documents,
    IReadOnlyList<InvoiceFieldDto> Fields,
    IReadOnlyList<InvoiceValidationIssueDto> ValidationIssues);

public sealed record InvoiceDocumentDto(
    Guid Id,
    string OriginalFileName,
    DateTime UploadedAt,
    long FileSize,
    string PreviewUrl);

public sealed record InvoiceFieldDto(
    string FieldType,
    string? Value,
    double Confidence);

public sealed record InvoiceValidationIssueDto(
    string Code,
    string Message,
    string Severity,
    DateTime CreatedAt);
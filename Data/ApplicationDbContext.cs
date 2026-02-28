using LedgerFlow.Models;
using LedgerFlow.Models.Extraction;
using LedgerFlow.Models.Invoices;
using LedgerFlow.Models.Validation;
using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;

namespace LedgerFlow.Data;

public class ApplicationDbContext : IdentityDbContext<ApplicationUser>
{
    public ApplicationDbContext(DbContextOptions<ApplicationDbContext> options)
        : base(options)
    {
    }

    public DbSet<Invoice> Invoices => Set<Invoice>();
    public DbSet<InvoiceDocument> InvoiceDocuments => Set<InvoiceDocument>();

    public DbSet<InvoiceField> InvoiceFields => Set<InvoiceField>();

    public DbSet<InvoiceValidationIssue> InvoiceValidationIssues => Set<InvoiceValidationIssue>();
}
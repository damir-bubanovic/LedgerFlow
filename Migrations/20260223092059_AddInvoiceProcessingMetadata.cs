using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace LedgerFlow.Migrations
{
    /// <inheritdoc />
    public partial class AddInvoiceProcessingMetadata : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<DateTime>(
                name: "ProcessingCompletedAt",
                table: "Invoices",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "ProcessingError",
                table: "Invoices",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "ProcessingStartedAt",
                table: "Invoices",
                type: "timestamp with time zone",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "ProcessingCompletedAt",
                table: "Invoices");

            migrationBuilder.DropColumn(
                name: "ProcessingError",
                table: "Invoices");

            migrationBuilder.DropColumn(
                name: "ProcessingStartedAt",
                table: "Invoices");
        }
    }
}

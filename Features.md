# LedgerFlow — Project Roadmap

Web-based invoice processing and accounting automation platform built on modern .NET.  
LedgerFlow focuses on secure document ingestion, AI-driven data extraction, financial validation, and scalable background processing.

This roadmap is split into Completed and To-Do chapters for clear development tracking.

---

## ✅ Completed Chapters

### Chapter 1 — Project Setup & Core Architecture

- ASP.NET Core Blazor Web App (.NET 8)
- Git repository initialized with proper .gitignore
- Base solution structure:
  - Web UI
  - Server-side logic
- Default hosting and development environment
- Initial GitHub repository


### Chapter 2 — Database & Identity

- Add PostgreSQL via Entity Framework Core
- Core entities:
  - User
  - Invoice
  - InvoiceDocument
  - InvoiceField
- ASP.NET Core Identity:
  - Email + password registration
  - Login / logout
  - Password hashing
- Database migrations
- Development seed data

---

### Chapter 3 — Invoice Upload & Storage

- Manual invoice upload:
  - PDF
  - JPG / PNG
- Server-side validation:
  - File size
  - MIME type
- File storage:
  - Local development storage
  - Abstraction for cloud storage later
- Persist document metadata:
  - Original filename
  - Upload date
  - User ownership
- Initial invoice status:
  - Pending

---

### Chapter 4 — Background Processing Pipeline

- Background worker service:
  - Queue uploaded invoices
  - Process asynchronously
- Status lifecycle:
  - Pending
  - Processing
  - Processed
  - Failed
  - NeedsReview
- Central processing coordinator:
  - Upload → extraction → validation → persistence

---

### Chapter 5 — AI Document Extraction

- OCR + AI integration:
  - Vendor name
  - Invoice date
  - Invoice number
  - Subtotal
  - VAT / tax
  - Total amount
- Store:
  - Raw extracted text
  - Structured fields
  - Confidence scores
- Error handling for malformed documents

---

### Chapter 6 — Financial Validation & Integrity

- Automatic checks:
  - Line totals vs invoice total
  - VAT calculation consistency
  - Currency detection
- Flag invoices requiring manual review
- Maintain audit trail:
  - Original values
  - Extracted values
  - Adjusted values


---

## 🚧 To-Do Chapters (MVP)

---


### Chapter 7 — Management Dashboard

- Invoice list view:
  - Vendor
  - Date
  - Total
  - Status
- Invoice detail view:
  - Uploaded document preview
  - Extracted fields
  - Validation results
- Status filtering:
  - Pending
  - Processed
  - Failed
  - NeedsReview

---

### Chapter 8 — Progressive Web App (PWA)

- Installable from browser
- Offline-ready shell
- Mobile-friendly layout
- Home screen icon
- Basic caching strategy

---

### Chapter 9 — Security & Production Readiness

- Secure cookie or JWT authentication
- HTTPS enforcement
- Data Protection key persistence
- Environment-based configuration
- Structured logging
- Health checks

---

## 🔮 Future Expansion

---

### Chapter 10 — Line Items & Advanced Parsing

- Extract individual invoice line items:
  - Description
  - Quantity
  - Unit price
  - Total
- Category tagging
- Supplier learning

---

### Chapter 11 — Accounting System Integration

- Export formats:
  - CSV
  - JSON
- Integration targets:
  - QuickBooks
  - Xero
  - Generic ERP systems

---

### Chapter 12 — Reporting & Analytics

- Monthly spending summaries
- Vendor aggregation
- Tax reports
- Export to PDF

---

### Chapter 13 — Multi-Tenant Organizations

- Organizations / teams
- Role-based access:
  - Admin
  - Accountant
  - Viewer
- Shared invoice pools

---

### Chapter 14 — Polishing & QA

- Improved UI/UX
- Field editing with validation
- Remember last filters
- Cross-browser testing
- Dockerized deployment

---

## Goal

LedgerFlow aims to eliminate manual invoice entry by providing reliable automated extraction, strong financial validation, and a clean management interface built for long-term scalability.
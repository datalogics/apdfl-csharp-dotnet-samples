# CreateInvoiceFromStructuredData

This sample creates a tagged PDF invoice from structured source files using Datalogics Adobe PDF Library SDK/APDFL. It starts with JSON invoice metadata, CSV line items, a JSON style configuration, and a local PNG logo image. It does not use HTML conversion, browser rendering, report writers, or third-party PDF generation libraries.

## Input files

- `data/invoice.json` contains invoice fields, seller details, customer details, payment terms, notes, tax rate, and the logo image path. Relative logo paths are resolved from the invoice JSON file location.
- `data/line-items.csv` contains the invoice table rows.
- `data/style.json` controls fonts, colors, page size, margins, and logo sizing.
- `images/northstar-logo.png` is a fictional local logo image referenced by the invoice JSON.

All sample data is fictional.

## Quick start

```powershell
dotnet restore
dotnet build
dotnet run
start CreateInvoiceFromStructuredData-out.pdf
```

With no options, the sample reads the files in `data/`, places the logo from `images/`, and writes `CreateInvoiceFromStructuredData-out.pdf`.

Run the built-in parsing and calculation checks:

```powershell
dotnet run -- --self-test
```

Show help:

```powershell
dotnet run -- --help
```

## Custom input files

```powershell
dotnet run -- --invoice data/invoice.json --line-items data/line-items.csv --style data/style.json --output invoice.pdf
```

## What this sample demonstrates

- Creating a new PDF document from scratch with APDFL
- Loading structured source data from JSON and CSV using .NET standard libraries
- Loading and placing a local image file specified by JSON
- Drawing text, boxes, rules, and tabular layout directly with APDFL
- Applying fonts, colors, sizes, and margins from a style configuration file
- Calculating subtotal, tax, and invoice total from line items
- Paginating a long invoice table across multiple pages
- Creating tagged PDF structure for headings, paragraphs, figures, and tables
- Marking decorative rules and boxes as artifacts
- Embedding/subsetting fonts before saving

## Command line

```text
CreateInvoiceFromStructuredData.exe [options]
```

Options:

```text
--invoice <path>       Invoice JSON file with seller, customer, and invoice metadata.
--line-items <path>    CSV file containing invoice line items.
--style <path>         JSON style configuration for fonts, colors, and sizing.
--output <path>        Output PDF path.
--self-test            Validate parsing and totals without creating a PDF.
--help, -h, /?         Show help.
```

## Notes

The CSV reader is intentionally small and local to the sample so APDFL remains the only package dependency. The parser supports quoted CSV fields and doubled quotes, which is enough for the included invoice data and similar simple line item files.

The sample focuses on PDF generation from structured data. It is not a full invoicing application, tax engine, or accounting integration.

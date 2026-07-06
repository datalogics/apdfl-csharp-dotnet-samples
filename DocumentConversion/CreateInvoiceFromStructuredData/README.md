# CreateInvoiceFromStructuredData

This sample creates a tagged PDF invoice from structured source files using Datalogics Adobe PDF Library SDK/APDFL. It starts with JSON metadata, CSV line items, a JSON style configuration, and a local PNG logo image. The sample focuses on direct PDF generation from structured business data with PDFL.

## What this sample does

This is a C#/.NET 8 console sample that builds an invoice PDF from scratch. It reads seller and customer information from JSON, line items from CSV, presentation settings from a second JSON file, and a local logo image, then uses APDFL to lay out and generate the finished document.

It is designed as a public sample, not a full invoicing product. The point is to show the document-generation work clearly:

- create a PDF from structured business data
- place branding and company information
- format a multi-column line item table
- paginate long invoices across multiple pages
- calculate subtotal, tax, and total values
- create tagged PDF structure for accessibility
- optionally apply a restriction password before saving

## Why this sample exists

This sample shows that PDFL can be used for PDF generation from structured data, not just for manipulating or converting existing PDF files. If a customer already has invoice content in a database, JSON payload, CSV export, or similar structured format, PDFL can be used to create the final PDF directly.

That makes this sample a good starting point for adjacent workflows such as statements, receipts, quotes, purchase orders, summaries, or other generated business documents.

## How the sample works

The program loads the structured source files, calculates invoice totals, and renders the document directly with APDFL. It places the logo, draws text and boxes, builds the line-item table, inserts page breaks when needed, and creates the tagged PDF structure as part of the same rendering flow.

In other words, the sample is doing the layout and generation work itself:

- reading structured input from local files
- resolving document styling from configuration
- measuring and placing text on the page
- drawing table and section framing
- creating tags for headings, paragraphs, figures, and tables
- saving a finished PDF with embedded fonts and optional security restrictions

That is the point of the sample. It demonstrates PDF generation with PDFL while keeping the dependency story simple.

## AI-assisted development note

This sample was developed iteratively with AI assistance, but not from a single one-shot prompt. The useful pattern was to combine concrete layout goals, incremental refinements, and visual review of the generated PDF after each pass.

If someone wants to extend the sample, a good starter prompt is:

```text
Extend this APDFL invoice-generation sample without adding new dependencies.
Keep PDFL responsible for layout, rendering, tagging, and output security.
Add support for [feature], update the self-tests, and document the behavior
and limitations in the README.
```

That framing helps preserve the purpose of the sample as a PDFL-first example.

## Input files

- `data/metadata.json` contains invoice fields, seller details, customer details, payment terms, notes, tax rate, the logo image path, and optional output security settings. Relative logo paths are resolved from the metadata JSON file location.
- `data/line-items.csv` contains the invoice table rows.
- `data/style.json` controls fonts, colors, page size, margins, and logo sizing.
- `images/northstar-logo.png` is a fictional local logo image referenced by the metadata JSON.

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
dotnet run -- --metadata data/metadata.json --line-items data/line-items.csv --style data/style.json --output invoice.pdf
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
- Applying an owner-password restriction that prevents casual editing while still allowing normal viewing
- Embedding/subsetting fonts before saving

## Command line

```text
CreateInvoiceFromStructuredData.exe [options]
```

Options:

```text
--metadata <path>      JSON file with seller, customer, and invoice metadata.
--invoice <path>       Alias for --metadata.
--line-items <path>    CSV file containing invoice line items.
--style <path>         JSON style configuration for fonts, colors, and sizing.
--output <path>        Output PDF path.
--self-test            Validate parsing and totals without creating a PDF.
--help, -h, /?         Show help.
```

## Notes

The CSV reader is intentionally small and local to the sample so APDFL remains the only package dependency. The parser supports quoted CSV fields and doubled quotes, which is enough for the included invoice data and similar simple line item files.

By default, the sample applies a restriction password from `metadata.json` before saving. This uses PDF security permissions to allow normal opening and printing while blocking editing unless the restriction password is known. Set `applyRestrictionPassword` to `false` in the metadata file to disable that behavior.

The sample focuses on PDF generation from structured data. It is not a full invoicing application, tax engine, or accounting integration.

## How to extend or modify it

For most follow-on work, the easiest path is to keep the current separation of responsibilities:

- `InvoiceDataLoader` and `CsvLineItemReader`: input parsing and validation
- `InvoiceDocument`: subtotal, tax, and total calculations
- `PdfInvoiceRenderer`: layout, drawing, pagination, tagging, and output security
- `InvoiceStyle` and `InvoiceStyleLoader`: configurable fonts, colors, sizes, margins, and logo sizing
- `SelfTests`: sample-level validation for parsing, defaults, totals, and required assets

If you modify the sample, it is worth re-running both `--self-test` and a full PDF generation pass so you can validate the data handling and the visual layout together.

## Limitations

This sample is intentionally focused on a single generated-document workflow. It is not a full invoice engine, ERP connector, payment processor, or tax rules system. It assumes local input files, a simple flat line-item table, and a predefined visual structure. More advanced features such as currency localization rules, multiple tax jurisdictions, dynamic branding systems, or arbitrary template editing are outside the scope of this public sample.

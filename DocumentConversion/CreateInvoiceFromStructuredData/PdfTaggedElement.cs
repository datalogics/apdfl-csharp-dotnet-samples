using Datalogics.PDFL;

namespace CreateInvoiceFromStructuredData;

internal sealed record PdfTaggedElement(
    string TagName,
    PDFDict Dictionary,
    PDFArray Kids);

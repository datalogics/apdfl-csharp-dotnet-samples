namespace CreateInvoiceFromStructuredData;

internal sealed class InvoiceDocument
{
    public InvoiceDocument(InvoiceInput invoice, IReadOnlyList<InvoiceLineItem> lineItems)
    {
        Invoice = invoice;
        LineItems = lineItems;
    }

    public InvoiceInput Invoice { get; }

    public IReadOnlyList<InvoiceLineItem> LineItems { get; }

    public decimal Subtotal => LineItems.Sum(item => item.Amount);

    public decimal Tax => Math.Round(Subtotal * Invoice.TaxRate, 2, MidpointRounding.AwayFromZero);

    public decimal Total => Subtotal + Tax;
}

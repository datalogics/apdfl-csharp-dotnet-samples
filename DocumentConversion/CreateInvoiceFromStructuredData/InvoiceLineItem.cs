namespace CreateInvoiceFromStructuredData;

internal sealed class InvoiceLineItem
{
    public string ItemCode { get; init; } = string.Empty;

    public string Description { get; init; } = string.Empty;

    public decimal Quantity { get; init; }

    public decimal UnitPrice { get; init; }

    public decimal Amount => Quantity * UnitPrice;
}

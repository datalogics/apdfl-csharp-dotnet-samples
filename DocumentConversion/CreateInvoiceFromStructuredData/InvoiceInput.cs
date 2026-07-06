namespace CreateInvoiceFromStructuredData;

internal sealed class InvoiceInput
{
    public string InvoiceNumber { get; init; } = string.Empty;

    public string IssueDate { get; init; } = string.Empty;

    public string DueDate { get; init; } = string.Empty;

    public string Currency { get; init; } = "USD";

    public decimal TaxRate { get; init; }

    public string LogoPath { get; init; } = string.Empty;

    public string PaymentTerms { get; init; } = string.Empty;

    public string Notes { get; init; } = string.Empty;

    public bool ApplyRestrictionPassword { get; init; } = true;

    public string RestrictionPassword { get; init; } = "NSS-Restrict-2026-ReviewOnly!";

    public CompanyInfo Seller { get; init; } = new();

    public CompanyInfo Customer { get; init; } = new();
}

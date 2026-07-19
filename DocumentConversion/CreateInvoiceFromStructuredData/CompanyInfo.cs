namespace CreateInvoiceFromStructuredData;

internal sealed class CompanyInfo
{
    public string Name { get; init; } = string.Empty;

    public string TaxId { get; init; } = string.Empty;

    public string AddressLine1 { get; init; } = string.Empty;

    public string AddressLine2 { get; init; } = string.Empty;

    public string City { get; init; } = string.Empty;

    public string Region { get; init; } = string.Empty;

    public string PostalCode { get; init; } = string.Empty;

    public string Country { get; init; } = string.Empty;

    public string Email { get; init; } = string.Empty;

    public string Phone { get; init; } = string.Empty;

    public IEnumerable<string> AddressLines()
    {
        yield return AddressLine1;
        yield return AddressLine2;
        yield return $"{City}, {Region} {PostalCode}".Trim(' ', ',');
        yield return Country;
    }
}

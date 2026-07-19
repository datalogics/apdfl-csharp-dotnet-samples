using System.Text.Json;

namespace CreateInvoiceFromStructuredData;

internal static class InvoiceDataLoader
{
    private static readonly JsonSerializerOptions Options = new()
    {
        PropertyNameCaseInsensitive = true,
        ReadCommentHandling = JsonCommentHandling.Skip,
        AllowTrailingCommas = true
    };

    public static InvoiceInput Load(string path)
    {
        if (!File.Exists(path))
        {
            throw new FileNotFoundException("Metadata JSON file was not found.", path);
        }

        string json = File.ReadAllText(path);
        InvoiceInput? invoice = JsonSerializer.Deserialize<InvoiceInput>(json, Options);

        if (invoice is null)
        {
            throw new InvalidDataException("Metadata JSON did not contain a valid invoice object.");
        }

        Require(invoice.InvoiceNumber, "invoiceNumber");
        Require(invoice.Seller.Name, "seller.name");
        Require(invoice.Customer.Name, "customer.name");

        if (invoice.ApplyRestrictionPassword)
        {
            Require(invoice.RestrictionPassword, "restrictionPassword");
        }

        return invoice;
    }

    private static void Require(string value, string fieldName)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            throw new InvalidDataException($"Metadata JSON requires a non-empty {fieldName} value.");
        }
    }
}

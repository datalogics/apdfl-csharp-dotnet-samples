using System.Text.Json;

namespace CreateInvoiceFromStructuredData;

internal static class InvoiceStyleLoader
{
    private static readonly JsonSerializerOptions Options = new()
    {
        PropertyNameCaseInsensitive = true,
        ReadCommentHandling = JsonCommentHandling.Skip,
        AllowTrailingCommas = true
    };

    public static InvoiceStyle Load(string path)
    {
        if (!File.Exists(path))
        {
            throw new FileNotFoundException("Style JSON file was not found.", path);
        }

        string json = File.ReadAllText(path);
        InvoiceStyle? style = JsonSerializer.Deserialize<InvoiceStyle>(json, Options);

        if (style is null)
        {
            throw new InvalidDataException("Style JSON did not contain a valid style object.");
        }

        return style;
    }
}

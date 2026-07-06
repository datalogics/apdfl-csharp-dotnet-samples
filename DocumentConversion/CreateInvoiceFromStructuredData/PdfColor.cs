using System.Globalization;
using Datalogics.PDFL;

namespace CreateInvoiceFromStructuredData;

internal readonly record struct PdfColor(double Red, double Green, double Blue)
{
    public Color ToPdfColor()
    {
        return new Color(Red, Green, Blue);
    }

    public static PdfColor FromHex(string value)
    {
        string hex = value.Trim().TrimStart('#');
        if (hex.Length != 6)
        {
            throw new InvalidDataException($"Color value must be in #RRGGBB form: {value}");
        }

        int red = int.Parse(hex[..2], NumberStyles.HexNumber, CultureInfo.InvariantCulture);
        int green = int.Parse(hex[2..4], NumberStyles.HexNumber, CultureInfo.InvariantCulture);
        int blue = int.Parse(hex[4..6], NumberStyles.HexNumber, CultureInfo.InvariantCulture);

        return new PdfColor(red / 255.0, green / 255.0, blue / 255.0);
    }
}

using System.Diagnostics.CodeAnalysis;
using System.Globalization;
using System.Net;
using System.Text;
using System.Text.RegularExpressions;
using Datalogics.PDFL;

namespace MarkdownToPDF;

internal sealed class PdfTheme
{
    public double PageWidth { get; init; } = 612.0;

    public double PageHeight { get; init; } = 792.0;

    public double MarginLeft { get; init; } = 72.0;

    public double MarginRight { get; init; } = 72.0;

    public double MarginTop { get; init; } = 72.0;

    public double MarginBottom { get; init; } = 72.0;

    public double ContentWidth => PageWidth - MarginLeft - MarginRight;

    public double BodyFontSize { get; init; } = 11.0;

    public double BodyLineHeight { get; init; } = 15.0;

    public double ParagraphSpaceAfter { get; init; } = 7.0;

    public double[] HeadingFontSizes { get; init; } = { 24.0, 20.0, 17.0, 14.0, 12.0, 11.0 };

    public double[] HeadingSpaceBefore { get; init; } = { 0.0, 14.0, 12.0, 10.0, 8.0, 8.0 };

    public double[] HeadingSpaceAfter { get; init; } = { 10.0, 8.0, 7.0, 6.0, 5.0, 5.0 };

    public double ListIndent { get; init; } = 18.0;

    public double ListMarkerWidth { get; init; } = 22.0;

    public double ListItemSpaceAfter { get; init; } = 2.0;

    public double ListSpaceAfter { get; init; } = 5.0;

    public double CodeFontSize { get; init; } = 9.5;

    public double CodeLineHeight { get; init; } = 12.5;

    public double CodeTitleFontSize { get; init; } = 9.0;

    public double CodeTitleLineHeight { get; init; } = 14.0;

    public double CodeBlockPadding { get; init; } = 8.0;

    public double CodeBlockBackgroundGray { get; init; } = 0.94;

    public double InlineCodeBackgroundGray { get; init; } = 0.92;

    public double InlineCodeHorizontalPadding { get; init; } = 2.0;

    public double CodeIndent { get; init; } = 18.0;

    public double CodeBlockSpaceBefore { get; init; } = 6.0;

    public double CodeBlockSpaceAfter { get; init; } = 8.0;

    public double BlockQuoteIndent { get; init; } = 18.0;

    public double BlockQuoteSpaceBefore { get; init; } = 5.0;

    public double BlockQuoteSpaceAfter { get; init; } = 8.0;

    public double HorizontalRuleSpaceBefore { get; init; } = 8.0;

    public double HorizontalRuleSpaceAfter { get; init; } = 10.0;

    public double TableFontSize { get; init; } = 9.5;

    public double TableLineHeight { get; init; } = 12.0;

    public double TableCellPadding { get; init; } = 4.0;

    public double TableBorderWidth { get; init; } = 0.5;

    public double TableSpaceBefore { get; init; } = 8.0;

    public double TableSpaceAfter { get; init; } = 9.0;

    internal static readonly Regex CustomPageSizeRegex = new(
        @"^(?<width>\d+(?:\.\d+)?)\s*[xX]\s*(?<height>\d+(?:\.\d+)?)$",
        RegexOptions.Compiled);

    public static readonly IReadOnlyDictionary<string, (double Width, double Height)> NamedPageSizes =
        new Dictionary<string, (double Width, double Height)>(StringComparer.OrdinalIgnoreCase)
        {
            ["Letter"] = (612.0, 792.0),
            ["Legal"] = (612.0, 1008.0),
            ["Ledger"] = (1224.0, 792.0),
            ["A3"] = (842.0, 1191.0),
            ["A4"] = (595.0, 842.0),
            ["A5"] = (420.0, 595.0),
            ["Tabloid"] = (792.0, 1224.0)
        };

    public static PdfTheme Create(string pageSize, string orientation, double margin)
    {
        (double width, double height) = ResolvePageSize(pageSize);

        if (string.Equals(orientation, "Landscape", StringComparison.OrdinalIgnoreCase) && height > width)
        {
            (width, height) = (height, width);
        }
        else if (string.Equals(orientation, "Portrait", StringComparison.OrdinalIgnoreCase) && width > height)
        {
            (width, height) = (height, width);
        }

        if (width - 2.0 * margin < 72.0 || height - 2.0 * margin < 72.0)
        {
            throw new CommandLineException("The selected page size and margin leave less than 72 points of usable width or height.");
        }

        return new PdfTheme
        {
            PageWidth = width,
            PageHeight = height,
            MarginLeft = margin,
            MarginRight = margin,
            MarginTop = margin,
            MarginBottom = margin
        };
    }

    private static (double Width, double Height) ResolvePageSize(string pageSize)
    {
        if (NamedPageSizes.TryGetValue(pageSize, out (double Width, double Height) named))
        {
            return named;
        }

        Match match = CustomPageSizeRegex.Match(pageSize);
        if (match.Success &&
            double.TryParse(match.Groups["width"].Value, NumberStyles.Float, CultureInfo.InvariantCulture, out double width) &&
            double.TryParse(match.Groups["height"].Value, NumberStyles.Float, CultureInfo.InvariantCulture, out double height))
        {
            return (width, height);
        }

        return NamedPageSizes["Letter"];
    }

}

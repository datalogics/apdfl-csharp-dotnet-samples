using System.Diagnostics.CodeAnalysis;
using System.Globalization;
using System.Net;
using System.Text;
using System.Text.RegularExpressions;
using Datalogics.PDFL;

namespace MarkdownToPDF;

internal sealed class ConversionOptions
{
    public string InputPath { get; init; } = string.Empty;

    public string OutputPath { get; init; } = string.Empty;

    public string? Title { get; init; }

    public string Language { get; init; } = "en-US";

    public string PageSize { get; init; } = "Letter";

    public string Orientation { get; init; } = "Auto";

    public double MarginPoints { get; init; } = 72.0;

    public string FontFamily { get; init; } = "MyriadPro";

    public string HeadingFontFamily { get; init; } = "MyriadPro";

    public string CodeFontFamily { get; init; } = "Courier";

    public string? CjkFontFamily { get; init; }

    public IReadOnlyList<string> FallbackFontNames { get; init; } = Array.Empty<string>();

    public bool Overwrite { get; init; }

    public bool Verbose { get; init; }

    public bool Recursive { get; init; }

    public bool IncludeUnrenderedHtml { get; init; }

    public ConversionOptions WithPaths(string inputPath, string outputPath)
    {
        return new ConversionOptions
        {
            InputPath = inputPath,
            OutputPath = outputPath,
            Title = Title,
            Language = Language,
            PageSize = PageSize,
            Orientation = Orientation,
            MarginPoints = MarginPoints,
            FontFamily = FontFamily,
            HeadingFontFamily = HeadingFontFamily,
            CodeFontFamily = CodeFontFamily,
            CjkFontFamily = CjkFontFamily,
            FallbackFontNames = FallbackFontNames.ToList(),
            Overwrite = Overwrite,
            Verbose = Verbose,
            Recursive = Recursive,
            IncludeUnrenderedHtml = IncludeUnrenderedHtml
        };
    }

    public static ConversionOptions Parse(string[] args)
    {
        List<string> positional = new();
        string? title = null;
        string language = "en-US";
        string pageSize = "Letter";
        string orientation = "Auto";
        double margin = 72.0;
        string fontFamily = "MyriadPro";
        string? headingFontFamily = null;
        string codeFontFamily = "Courier";
        string? cjkFontFamily = null;
        List<string> fallbackFontNames = new();
        bool overwrite = false;
        bool verbose = false;
        bool recursive = false;
        bool includeUnrenderedHtml = false;

        for (int i = 0; i < args.Length; i++)
        {
            string arg = args[i];

            if (!arg.StartsWith("-", StringComparison.Ordinal))
            {
                positional.Add(arg);
                continue;
            }

            switch (arg.ToLowerInvariant())
            {
                case "--title":
                    title = RequireValue(args, ref i, arg);
                    break;

                case "--lang":
                    language = RequireValue(args, ref i, arg);
                    break;

                case "--page-size":
                    pageSize = NormalizePageSize(RequireValue(args, ref i, arg));
                    break;

                case "--orientation":
                    orientation = NormalizeOrientation(RequireValue(args, ref i, arg));
                    break;

                case "--margin":
                    string marginText = RequireValue(args, ref i, arg);
                    if (!double.TryParse(marginText, NumberStyles.Float, CultureInfo.InvariantCulture, out margin) || margin < 18.0 || margin > 144.0)
                    {
                        throw new CommandLineException("--margin must be a number between 18 and 144 points.");
                    }

                    break;

                case "--font-family":
                    fontFamily = NormalizeFontFamily(RequireValue(args, ref i, arg));
                    break;

                case "--heading-font-family":
                    headingFontFamily = NormalizeFontFamily(RequireValue(args, ref i, arg));
                    break;

                case "--code-font-family":
                    codeFontFamily = NormalizeFontFamily(RequireValue(args, ref i, arg));
                    break;

                case "--cjk-font-family":
                    cjkFontFamily = NormalizeFontFamilyOrName(RequireValue(args, ref i, arg));
                    fallbackFontNames.Add(cjkFontFamily);
                    break;

                case "--fallback-font-family":
                    fallbackFontNames.Add(NormalizeFontFamilyOrName(RequireValue(args, ref i, arg)));
                    break;

                case "--fallback-fonts":
                    foreach (string fontName in RequireValue(args, ref i, arg).Split(',', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries))
                    {
                        fallbackFontNames.Add(NormalizeFontFamilyOrName(fontName));
                    }

                    break;

                case "--overwrite":
                    overwrite = true;
                    break;

                case "--verbose":
                    verbose = true;
                    break;

                case "--recursive":
                    recursive = true;
                    break;

                case "--include-unrendered-html":
                case "--include-raw-html":
                    includeUnrenderedHtml = true;
                    break;

                default:
                    throw new CommandLineException($"Unknown option: {arg}");
            }
        }

        bool useDefaultPaths = positional.Count == 0;

        if (positional.Count != 0 && positional.Count != 2)
        {
            throw new CommandLineException("Expected exactly two positional arguments: input.md output.pdf");
        }

        return new ConversionOptions
        {
            InputPath = useDefaultPaths ? "sample.md" : positional[0],
            OutputPath = useDefaultPaths ? "output.pdf" : positional[1],
            Title = title,
            Language = string.IsNullOrWhiteSpace(language) ? "en-US" : language,
            PageSize = pageSize,
            Orientation = orientation,
            MarginPoints = margin,
            FontFamily = fontFamily,
            HeadingFontFamily = headingFontFamily ?? fontFamily,
            CodeFontFamily = codeFontFamily,
            CjkFontFamily = cjkFontFamily,
            FallbackFontNames = BuildFallbackFontList(cjkFontFamily, fallbackFontNames),
            Overwrite = useDefaultPaths || overwrite,
            Verbose = verbose,
            Recursive = recursive,
            IncludeUnrenderedHtml = includeUnrenderedHtml
        };
    }

    private static string RequireValue(string[] args, ref int index, string optionName)
    {
        if (index + 1 >= args.Length)
        {
            throw new CommandLineException($"{optionName} requires a value.");
        }

        index++;
        return args[index];
    }

    private static string NormalizePageSize(string value)
    {
        string trimmed = value.Trim();

        foreach (string name in PdfTheme.NamedPageSizes.Keys)
        {
            if (string.Equals(trimmed, name, StringComparison.OrdinalIgnoreCase))
            {
                return name;
            }
        }

        Match match = PdfTheme.CustomPageSizeRegex.Match(trimmed);
        if (match.Success &&
            double.TryParse(match.Groups["width"].Value, NumberStyles.Float, CultureInfo.InvariantCulture, out double width) &&
            double.TryParse(match.Groups["height"].Value, NumberStyles.Float, CultureInfo.InvariantCulture, out double height) &&
            width >= 144.0 && width <= 2880.0 &&
            height >= 144.0 && height <= 2880.0)
        {
            return $"{width.ToString("0.###", CultureInfo.InvariantCulture)}x{height.ToString("0.###", CultureInfo.InvariantCulture)}";
        }

        throw new CommandLineException(
            $"--page-size must be {string.Join(", ", PdfTheme.NamedPageSizes.Keys)}, " +
            "or custom WIDTHxHEIGHT in PDF points, such as 612x792.");
    }

    private static string NormalizeOrientation(string value)
    {
        string trimmed = value.Trim();

        if (string.Equals(trimmed, "Auto", StringComparison.OrdinalIgnoreCase))
        {
            return "Auto";
        }

        if (string.Equals(trimmed, "Portrait", StringComparison.OrdinalIgnoreCase))
        {
            return "Portrait";
        }

        if (string.Equals(trimmed, "Landscape", StringComparison.OrdinalIgnoreCase))
        {
            return "Landscape";
        }

        throw new CommandLineException("--orientation must be Auto, Portrait, or Landscape.");
    }

    private static string NormalizeFontFamily(string value)
    {
        return PdfFontCatalog.NormalizeFamily(value);
    }

    private static string NormalizeFontFamilyOrName(string value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            throw new CommandLineException("Font name cannot be blank.");
        }

        if (PdfFontCatalog.TryNormalizeFamily(value, out string? normalized))
        {
            return normalized;
        }

        // Fallback fonts may be exact installed font names that are not in the sample's family catalog.
        return value.Trim();
    }

    private static IReadOnlyList<string> BuildFallbackFontList(string? cjkFontFamily, IReadOnlyList<string> requestedFallbacks)
    {
        List<string> fonts = new();

        if (!string.IsNullOrWhiteSpace(cjkFontFamily))
        {
            fonts.Add(cjkFontFamily);
        }

        fonts.AddRange(requestedFallbacks.Where(font => !string.IsNullOrWhiteSpace(font)));

        // Common Windows/macOS/Linux Unicode fallback candidates. They are tried lazily and skipped if APDFL cannot create them.
        // These improve Cyrillic/Greek/general Unicode text when the selected body font is one of the
        // PDF base families that may not contain those glyphs.
        fonts.Add("Arial");
        fonts.Add("Times New Roman");
        fonts.Add("Calibri");
        fonts.Add("Segoe UI");
        fonts.Add("DejaVu Sans");
        fonts.Add("Noto Sans");

        // Common CJK fallback candidates.
        fonts.Add("Microsoft YaHei");
        fonts.Add("SimSun");
        fonts.Add("Microsoft JhengHei");
        fonts.Add("Malgun Gothic");
        fonts.Add("Yu Gothic");
        fonts.Add("Noto Sans CJK SC");
        fonts.Add("Noto Sans CJK JP");
        fonts.Add("Noto Sans CJK KR");
        fonts.Add("Arial Unicode MS");

        return fonts
            .Select(font => font.Trim())
            .Select(font => PdfFontCatalog.TryNormalizeFamily(font, out string? normalized) ? normalized : font)
            .Where(font => font.Length > 0)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();
    }
}

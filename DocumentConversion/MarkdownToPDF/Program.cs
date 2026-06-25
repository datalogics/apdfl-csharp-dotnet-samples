using System.Diagnostics.CodeAnalysis;
using System.Globalization;
using System.Net;
using System.Text;
using System.Text.RegularExpressions;
using Datalogics.PDFL;

namespace MarkdownToPdf;

internal static class Program
{
    public static int Main(string[] args)
    {
        try
        {
            if (args.Length == 1 && string.Equals(args[0], "--self-test", StringComparison.OrdinalIgnoreCase))
            {
                return SelfTests.Run();
            }

            if (args.Length == 1 && string.Equals(args[0], "--list-font-families", StringComparison.OrdinalIgnoreCase))
            {
                PrintSupportedFontFamilies();
                return 0;
            }

            if (args.Length == 0 || args.Any(IsHelpArgument))
            {
                PrintUsage();
                return args.Length == 0 ? 2 : 0;
            }

            ConversionOptions options = ConversionOptions.Parse(args);
            bool inputIsDirectory = Directory.Exists(options.InputPath);
            bool inputIsFile = File.Exists(options.InputPath);

            if (!inputIsDirectory && !inputIsFile)
            {
                Console.Error.WriteLine($"Input Markdown file or directory was not found: {options.InputPath}");
                return 3;
            }

            string? licenseKey = Environment.GetEnvironmentVariable("APDFL_LICENSE_KEY");
            if (!string.IsNullOrWhiteSpace(licenseKey))
            {
                Library.LicenseKey = licenseKey;
            }

            using (Library library = new Library())
            {
                _ = library;

                if (inputIsDirectory)
                {
                    return ConvertDirectory(options);
                }

                ConvertSingleFile(options);
                return 0;
            }
        }
        catch (Exception ex) when (IsApdflException(ex))
        {
            Console.Error.WriteLine("APDFL error:");
            Console.Error.WriteLine(ex.Message);
            Console.Error.WriteLine("Confirm that APDFL is licensed and that APDFL_LICENSE_KEY is set if your environment requires it.");
            return 10;
        }
        catch (CommandLineException ex)
        {
            Console.Error.WriteLine(ex.Message);
            Console.Error.WriteLine();
            PrintUsage();
            return 2;
        }
        catch (IOException ex)
        {
            Console.Error.WriteLine("File I/O error:");
            Console.Error.WriteLine(ex.Message);
            return 11;
        }
        catch (UnauthorizedAccessException ex)
        {
            Console.Error.WriteLine("Permission error:");
            Console.Error.WriteLine(ex.Message);
            return 12;
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine("Unexpected error:");
            Console.Error.WriteLine(ex);
            return 99;
        }
    }

    private static int ConvertDirectory(ConversionOptions options)
    {
        string inputDirectory = System.IO.Path.GetFullPath(options.InputPath);
        string outputDirectory = System.IO.Path.GetFullPath(options.OutputPath);

        if (File.Exists(outputDirectory))
        {
            Console.Error.WriteLine($"When the input path is a directory, the output path must be a directory, not a file: {outputDirectory}");
            return 6;
        }

        Directory.CreateDirectory(outputDirectory);

        SearchOption searchOption = options.Recursive
            ? SearchOption.AllDirectories
            : SearchOption.TopDirectoryOnly;

        List<string> markdownFiles = Directory
            .EnumerateFiles(inputDirectory, "*", searchOption)
            .Where(IsMarkdownFile)
            .OrderBy(path => path, StringComparer.OrdinalIgnoreCase)
            .ToList();

        if (markdownFiles.Count == 0)
        {
            Console.Error.WriteLine($"No .md files were found in: {inputDirectory}");
            return 5;
        }

        int succeeded = 0;
        int failed = 0;

        foreach (string inputFile in markdownFiles)
        {
            string relativePath = System.IO.Path.GetRelativePath(inputDirectory, inputFile);
            string outputRelativePath = System.IO.Path.ChangeExtension(relativePath, ".pdf");
            string outputFile = System.IO.Path.Combine(outputDirectory, outputRelativePath);
            ConversionOptions fileOptions = options.WithPaths(inputFile, outputFile);

            try
            {
                ConvertSingleFile(fileOptions);
                succeeded++;
            }
            catch (Exception ex) when (IsApdflException(ex) || ex is CommandLineException or IOException or UnauthorizedAccessException)
            {
                failed++;
                Console.Error.WriteLine();
                Console.Error.WriteLine($"Failed: {inputFile}");
                Console.Error.WriteLine(ex.Message);
            }
        }

        Console.WriteLine();
        Console.WriteLine($"Batch complete: {succeeded} succeeded, {failed} failed, {markdownFiles.Count} total.");

        return failed == 0 ? 0 : 20;
    }

    private static void ConvertSingleFile(ConversionOptions options)
    {
        string fullInputPath = System.IO.Path.GetFullPath(options.InputPath);
        string fullOutputPath = System.IO.Path.GetFullPath(options.OutputPath);
        string? outputDirectory = System.IO.Path.GetDirectoryName(fullOutputPath);

        if (!File.Exists(fullInputPath))
        {
            throw new FileNotFoundException("Input Markdown file was not found.", fullInputPath);
        }

        if (!string.IsNullOrWhiteSpace(outputDirectory))
        {
            Directory.CreateDirectory(outputDirectory);
        }

        if (File.Exists(fullOutputPath) && !options.Overwrite)
        {
            throw new CommandLineException($"Output file already exists: {fullOutputPath}. Use --overwrite to replace it.");
        }

        string markdown = File.ReadAllText(
            fullInputPath,
            new UTF8Encoding(encoderShouldEmitUTF8Identifier: false, throwOnInvalidBytes: true));

        MarkdownParser parser = new();
        MarkdownDocument markdownDocument = parser.Parse(markdown, options.IncludeUnrenderedHtml);
        PdfTheme theme = PdfTheme.Create(options.PageSize, options.Orientation, options.MarginPoints);

        Document pdfDocument = new Document
        {
            Title = options.Title ?? System.IO.Path.GetFileNameWithoutExtension(fullInputPath),
            Producer = "MarkdownToPdf tagged sample using Datalogics APDFL"
        };

        PdfMarkdownRenderer renderer = new(pdfDocument, theme, options);
        renderer.Render(markdownDocument);

        // Embed/subset fonts before saving. This is especially important for Unicode content
        // such as Cyrillic and CJK text, where the PDF needs the correct glyph and width data.
        pdfDocument.EmbedFonts(EmbedFlags.None);

        pdfDocument.Save(SaveFlags.Full, fullOutputPath);

        Console.WriteLine($"Created tagged PDF: {fullOutputPath}");

        if (options.Verbose)
        {
            Console.WriteLine($"Blocks parsed: {markdownDocument.Blocks.Count}");
            Console.WriteLine($"Language: {options.Language}");
            Console.WriteLine($"Page size: {options.PageSize}");
            Console.WriteLine($"Orientation: {options.Orientation}");
            Console.WriteLine($"Resolved page: {theme.PageWidth.ToString(CultureInfo.InvariantCulture)} x {theme.PageHeight.ToString(CultureInfo.InvariantCulture)} pt");
            Console.WriteLine($"Margin: {options.MarginPoints.ToString(CultureInfo.InvariantCulture)} pt");
            Console.WriteLine($"Body font family: {options.FontFamily}");
            Console.WriteLine($"Heading font family: {options.HeadingFontFamily}");
            Console.WriteLine($"Code font family: {options.CodeFontFamily}");
            Console.WriteLine($"CJK font family: {options.CjkFontFamily ?? "auto"}");
            Console.WriteLine($"Fallback fonts: {(options.FallbackFontNames.Count == 0 ? "none" : string.Join(", ", options.FallbackFontNames))}");
            Console.WriteLine($"Include unrendered HTML: {options.IncludeUnrenderedHtml}");
        }
    }

    private static bool IsMarkdownFile(string path)
    {
        return string.Equals(System.IO.Path.GetExtension(path), ".md", StringComparison.OrdinalIgnoreCase);
    }

    private static bool IsApdflException(Exception ex)
    {
        // APDFL can report expected runtime issues such as licensing, activation,
        // locale initialization, and missing fonts through either exception type.
        return ex is LibraryException or ApplicationException;
    }

    private static bool IsHelpArgument(string arg)
    {
        return string.Equals(arg, "--help", StringComparison.OrdinalIgnoreCase) ||
               string.Equals(arg, "-h", StringComparison.OrdinalIgnoreCase) ||
               string.Equals(arg, "/?", StringComparison.OrdinalIgnoreCase);
    }

    private static void PrintUsage()
    {
        Console.WriteLine("Usage:");
        Console.WriteLine("  MarkdownToPdf.exe input.md output.pdf [options]");
        Console.WriteLine("  MarkdownToPdf.exe input-folder output-folder [options]");
        Console.WriteLine();
        Console.WriteLine("Input/output:");
        Console.WriteLine("  input.md output.pdf             Convert one Markdown file to one PDF.");
        Console.WriteLine("  input-folder output-folder      Convert every .md file in a folder.");
        Console.WriteLine("  --recursive                     In folder mode, include subfolders and preserve relative paths.");
        Console.WriteLine("  --overwrite                     Replace existing output PDF files.");
        Console.WriteLine("  --verbose                       Print conversion settings and summary information.");
        Console.WriteLine();
        Console.WriteLine("Document metadata:");
        Console.WriteLine("  --title <text>                  PDF document title. Defaults to the input file name.");
        Console.WriteLine("  --lang <tag>                    Document language tag. Defaults to en-US.");
        Console.WriteLine();
        Console.WriteLine("Page setup:");
        Console.WriteLine("  --page-size <value>             Letter, Legal, Ledger, A3, A4, A5, Tabloid, or WIDTHxHEIGHT in points.");
        Console.WriteLine("  --orientation <value>           Auto, Portrait, or Landscape. Defaults to Auto.");
        Console.WriteLine("  --margin <points>               Margin on all sides in PDF points. Defaults to 72.");
        Console.WriteLine();
        Console.WriteLine("Fonts:");
        Console.WriteLine("  --font-family <value>           Body font family. Defaults to Times.");
        Console.WriteLine("  --heading-font-family <value>   Heading font family. Defaults to the body font family.");
        Console.WriteLine("  --code-font-family <value>      Monospace/code font family. Defaults to Courier.");
        Console.WriteLine("  --cjk-font-family <value>       Preferred CJK font family/name for Chinese/Japanese/Korean text.");
        Console.WriteLine("  --fallback-font-family <value>  Add a fallback font name. Can be repeated.");
        Console.WriteLine("  --fallback-fonts <csv>          Add comma-separated fallback font names.");
        Console.WriteLine("  --list-font-families            Print recognized font-family names and aliases.");
        Console.WriteLine();
        Console.WriteLine("HTML handling:");
        Console.WriteLine("  --include-unrendered-html       Render unsupported/raw HTML tags as visible text instead of stripping them.");
        Console.WriteLine("  --include-raw-html              Alias for --include-unrendered-html.");
        Console.WriteLine();
        Console.WriteLine("Diagnostics/help:");
        Console.WriteLine("  --self-test                     Run parser/inline self-tests without creating a PDF.");
        Console.WriteLine("  --help, -h, /?                  Show this help.");
        Console.WriteLine();
        Console.WriteLine("Examples:");
        Console.WriteLine("  MarkdownToPdf.exe sample.md output.pdf --overwrite");
        Console.WriteLine("  MarkdownToPdf.exe samples sample-pdfs --recursive --overwrite --page-size A4 --orientation landscape --font-family Helvetica");
        Console.WriteLine("  MarkdownToPdf.exe multilingual.md multilingual.pdf --font-family Arial --cjk-font-family \"Microsoft YaHei\"");
        Console.WriteLine("  MarkdownToPdf.exe config.md config.pdf --include-unrendered-html --overwrite");
        Console.WriteLine();
        Console.WriteLine("License:");
        Console.WriteLine("  Set APDFL_LICENSE_KEY to provide a Datalogics APDFL activation key before Library initialization.");
    }

    private static void PrintSupportedFontFamilies()
    {
        Console.WriteLine("Recognized --font-family values:");
        foreach (string name in PdfFontCatalog.SupportedFamilyNames)
        {
            Console.WriteLine($"  {name}");
        }

        Console.WriteLine();
        Console.WriteLine("The core PDF families Times, Helvetica, and Courier are the most portable choices.");
        Console.WriteLine("Other families depend on fonts available to APDFL on the machine running the sample.");
    }
}

internal sealed class ConversionOptions
{
    private static readonly Regex CustomPageSizeRegex = new(
        @"^(?<width>\d+(?:\.\d+)?)\s*[xX]\s*(?<height>\d+(?:\.\d+)?)$",
        RegexOptions.Compiled);

    public string InputPath { get; init; } = string.Empty;

    public string OutputPath { get; init; } = string.Empty;

    public string? Title { get; init; }

    public string Language { get; init; } = "en-US";

    public string PageSize { get; init; } = "Letter";

    public string Orientation { get; init; } = "Auto";

    public double MarginPoints { get; init; } = 72.0;

    public string FontFamily { get; init; } = "Times";

    public string HeadingFontFamily { get; init; } = "Times";

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
        string fontFamily = "Times";
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

        if (positional.Count != 2)
        {
            throw new CommandLineException("Expected exactly two positional arguments: input.md output.pdf");
        }

        return new ConversionOptions
        {
            InputPath = positional[0],
            OutputPath = positional[1],
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
            Overwrite = overwrite,
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

        foreach (string name in new[] { "Letter", "Legal", "Ledger", "A3", "A4", "A5", "Tabloid" })
        {
            if (string.Equals(trimmed, name, StringComparison.OrdinalIgnoreCase))
            {
                return name;
            }
        }

        Match match = CustomPageSizeRegex.Match(trimmed);
        if (match.Success &&
            double.TryParse(match.Groups["width"].Value, NumberStyles.Float, CultureInfo.InvariantCulture, out double width) &&
            double.TryParse(match.Groups["height"].Value, NumberStyles.Float, CultureInfo.InvariantCulture, out double height) &&
            width >= 144.0 && width <= 2880.0 &&
            height >= 144.0 && height <= 2880.0)
        {
            return $"{width.ToString("0.###", CultureInfo.InvariantCulture)}x{height.ToString("0.###", CultureInfo.InvariantCulture)}";
        }

        throw new CommandLineException("--page-size must be Letter, Legal, Ledger, A3, A4, A5, Tabloid, or custom WIDTHxHEIGHT in PDF points, such as 612x792.");
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
            .Where(font => font.Length > 0)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();
    }
}

internal sealed class CommandLineException : Exception
{
    public CommandLineException(string message) : base(message)
    {
    }

    public CommandLineException(string message, Exception innerException) : base(message, innerException)
    {
    }
}

internal static class MarkdownHtmlNormalizer
{
    private static readonly Regex HtmlCommentRegex = new(@"<!--.*?-->", RegexOptions.Singleline | RegexOptions.Compiled);
    private static readonly Regex BrRegex = new(@"<\s*br\s*/?\s*>", RegexOptions.IgnoreCase | RegexOptions.Compiled);
    private static readonly Regex ImgRegex = new(@"<\s*img\b(?<attrs>[^>]*)>", RegexOptions.IgnoreCase | RegexOptions.Singleline | RegexOptions.Compiled);
    private static readonly Regex AnchorRegex = new(@"<\s*a\b[^>]*\bhref\s*=\s*(?:""(?<url>[^""]*)""|'(?<url>[^']*)'|(?<url>[^\s>]+))[^>]*>(?<label>.*?)<\s*/\s*a\s*>", RegexOptions.IgnoreCase | RegexOptions.Singleline | RegexOptions.Compiled);
    private static readonly Regex HeadingTagRegex = new(@"<\s*h(?<level>[1-6])\b[^>]*>(?<text>.*?)<\s*/\s*h[1-6]\s*>", RegexOptions.IgnoreCase | RegexOptions.Singleline | RegexOptions.Compiled);
    private static readonly Regex BlockTagRegex = new(@"<\s*/?\s*(?:div|p|section|article|header|footer|main|center|nav|aside)\b[^>]*>", RegexOptions.IgnoreCase | RegexOptions.Compiled);
    private static readonly Regex InlineTagRegex = new(@"<\s*/?\s*(?:span|font|small|u|mark|sup|sub)\b[^>]*>", RegexOptions.IgnoreCase | RegexOptions.Compiled);
    private static readonly Regex RemainingHtmlTagRegex = new(@"<\s*/?\s*[A-Za-z][A-Za-z0-9-]*(?:\s+[^<>]*)?\s*/?>", RegexOptions.IgnoreCase | RegexOptions.Compiled);

    public static string Normalize(string markdown, bool includeUnrenderedHtml = false)
    {
        string normalizedInput = markdown.Replace("\r\n", "\n", StringComparison.Ordinal).Replace('\r', '\n');
        string[] lines = normalizedInput.Split('\n');
        StringBuilder output = new();

        bool inFence = false;
        string activeFence = string.Empty;

        foreach (string line in lines)
        {
            string trimmed = line.TrimStart();

            if (IsFenceLine(trimmed, out string fence))
            {
                if (!inFence)
                {
                    inFence = true;
                    activeFence = fence;
                }
                else if (trimmed.StartsWith(activeFence, StringComparison.Ordinal))
                {
                    inFence = false;
                    activeFence = string.Empty;
                }

                output.Append(line).Append('\n');
                continue;
            }

            if (inFence)
            {
                output.Append(line).Append('\n');
                continue;
            }

            string normalizedLine = NormalizeLine(line, includeUnrenderedHtml);
            output.Append(normalizedLine).Append('\n');
        }

        return output.ToString();
    }

    private static string NormalizeLine(string line, bool includeUnrenderedHtml)
    {
        string result = HtmlCommentRegex.Replace(line, string.Empty);

        result = HeadingTagRegex.Replace(result, match =>
        {
            int level = int.Parse(match.Groups["level"].Value, CultureInfo.InvariantCulture);
            string text = CleanHtmlText(match.Groups["text"].Value);
            return "\n" + new string('#', level) + " " + text + "\n";
        });

        result = AnchorRegex.Replace(result, match =>
        {
            string label = CleanHtmlText(match.Groups["label"].Value);
            string url = WebUtility.HtmlDecode(match.Groups["url"].Value.Trim());
            return string.IsNullOrWhiteSpace(url) ? label : $"[{label}]({url})";
        });

        result = ImgRegex.Replace(result, match =>
        {
            string attrs = match.Groups["attrs"].Value;
            string alt = ReadAttribute(attrs, "alt");
            string src = ReadAttribute(attrs, "src");
            string label = !string.IsNullOrWhiteSpace(alt) ? alt : src;
            return string.IsNullOrWhiteSpace(label) ? "[Image omitted]" : $"[Image omitted: {CleanHtmlText(label)}]";
        });

        result = ReplacePairedTags(result, "strong|b", "**", "**");
        result = ReplacePairedTags(result, "em|i", "*", "*");
        result = ReplacePairedTags(result, "code|kbd|samp", "`", "`");
        result = ReplacePairedTags(result, "del|s|strike", "~~", "~~");

        // Treat common HTML line-break tags as a paragraph break in this sample renderer.
        result = BrRegex.Replace(result, "\n\n");

        // Ignore common block/container tags such as <div align="center"> and </div>, but keep their inner Markdown.
        result = BlockTagRegex.Replace(result, "\n");

        // Drop simple inline presentational tags while keeping their text.
        result = InlineTagRegex.Replace(result, string.Empty);

        // By default, strip remaining unsupported/raw HTML tags. With --include-unrendered-html,
        // leave them visible as literal text, which is useful for Markdown that documents XML/HTML-like
        // configuration syntax such as <field name="...">. This deliberately does not alter Markdown
        // autolinks such as <https://example.com>.
        if (!includeUnrenderedHtml)
        {
            result = RemainingHtmlTagRegex.Replace(result, string.Empty);
        }

        return WebUtility.HtmlDecode(result);
    }

    private static string ReplacePairedTags(string input, string tagAlternatives, string openMarker, string closeMarker)
    {
        Regex regex = new(
            $@"<\s*(?:{tagAlternatives})\b[^>]*>(?<text>.*?)<\s*/\s*(?:{tagAlternatives})\s*>",
            RegexOptions.IgnoreCase | RegexOptions.Singleline);

        string previous;
        string current = input;

        do
        {
            previous = current;
            current = regex.Replace(current, match => openMarker + CleanHtmlText(match.Groups["text"].Value) + closeMarker);
        }
        while (!string.Equals(previous, current, StringComparison.Ordinal));

        return current;
    }

    private static string CleanHtmlText(string value)
    {
        string withoutTags = RemainingHtmlTagRegex.Replace(value, string.Empty);
        return WebUtility.HtmlDecode(withoutTags).Trim();
    }

    private static string ReadAttribute(string attributes, string name)
    {
        string pattern = $@"\b{Regex.Escape(name)}\s*=\s*(?:""(?<value>[^""]*)""|'(?<value>[^']*)'|(?<value>[^\s>]+))";
        Match match = Regex.Match(attributes, pattern, RegexOptions.IgnoreCase | RegexOptions.Singleline);
        return match.Success ? WebUtility.HtmlDecode(match.Groups["value"].Value.Trim()) : string.Empty;
    }

    private static bool IsFenceLine(string trimmedLine, out string fence)
    {
        if (trimmedLine.StartsWith("```", StringComparison.Ordinal))
        {
            fence = "```";
            return true;
        }

        if (trimmedLine.StartsWith("~~~", StringComparison.Ordinal))
        {
            fence = "~~~";
            return true;
        }

        fence = string.Empty;
        return false;
    }
}

internal sealed class MarkdownParser
{
    private static readonly Regex HeadingRegex = new(@"^(#{1,6})(?:\s+|$)(.*)$", RegexOptions.Compiled);
    private static readonly Regex ListItemRegex = new(@"^(\s*)([-+*]|\d+[.)])\s+(.*)$", RegexOptions.Compiled);
    private static readonly Regex HorizontalRuleRegex = new(@"^\s{0,3}([-*_])(?:\s*\1){2,}\s*$", RegexOptions.Compiled);
    private static readonly Regex ReferenceDefinitionRegex = new(@"^\s{0,3}\[([^\]]+)\]:\s*(\S+)(?:\s+['""(].*['"")])?\s*$", RegexOptions.Compiled);
    private static readonly Regex SetextHeadingRegex = new(@"^\s{0,3}(=+|-+)\s*$", RegexOptions.Compiled);

    private Dictionary<string, string> _references = new(StringComparer.OrdinalIgnoreCase);

    public MarkdownDocument Parse(string markdown, bool includeUnrenderedHtml = false)
    {
        markdown = MarkdownHtmlNormalizer.Normalize(markdown, includeUnrenderedHtml);

        string[] lines = markdown.Replace("\r\n", "\n", StringComparison.Ordinal).Replace('\r', '\n').Split('\n');
        _references = ExtractReferenceDefinitions(lines);

        List<MarkdownBlock> blocks = new();

        int i = 0;
        while (i < lines.Length)
        {
            string line = lines[i];

            if (IsBlank(line))
            {
                i++;
                continue;
            }

            if (TryGetFenceStart(line, out string fenceMarker, out string language))
            {
                blocks.Add(ParseCodeBlock(lines, ref i, fenceMarker, language));
                continue;
            }

            if (TryParseHeading(line, out HeadingBlock? heading))
            {
                blocks.Add(heading);
                i++;
                continue;
            }

            if (TryParseSetextHeading(lines, i, out HeadingBlock? setextHeading))
            {
                blocks.Add(setextHeading);
                i += 2;
                continue;
            }

            if (IsHorizontalRule(line))
            {
                blocks.Add(new HorizontalRuleBlock());
                i++;
                continue;
            }

            if (IsBlockQuoteLine(line))
            {
                blocks.Add(ParseBlockQuote(lines, ref i));
                continue;
            }

            if (TryParseTable(lines, i, out TableBlock? table, out int nextIndex))
            {
                blocks.Add(table);
                i = nextIndex;
                continue;
            }

            if (TryParseListItemLine(line, out _))
            {
                blocks.Add(ParseListBlock(lines, ref i));
                continue;
            }

            blocks.Add(ParseParagraph(lines, ref i));
        }

        return new MarkdownDocument(blocks);
    }

    private Dictionary<string, string> ExtractReferenceDefinitions(string[] lines)
    {
        Dictionary<string, string> references = new(StringComparer.OrdinalIgnoreCase);

        for (int i = 0; i < lines.Length; i++)
        {
            Match match = ReferenceDefinitionRegex.Match(lines[i]);
            if (!match.Success)
            {
                continue;
            }

            string label = NormalizeReferenceLabel(match.Groups[1].Value);
            string url = match.Groups[2].Value.Trim();

            if (label.Length > 0 && url.Length > 0 && !references.ContainsKey(label))
            {
                references[label] = url;
                lines[i] = string.Empty;
            }
        }

        return references;
    }

    private static string NormalizeReferenceLabel(string value)
    {
        return Regex.Replace(value.Trim(), @"\s+", " ");
    }

    private CodeBlock ParseCodeBlock(string[] lines, ref int index, string fenceMarker, string language)
    {
        index++;

        List<string> codeLines = new();
        while (index < lines.Length)
        {
            string line = lines[index];
            string trimmed = line.TrimStart();

            if (trimmed.StartsWith(fenceMarker, StringComparison.Ordinal))
            {
                index++;
                return new CodeBlock(language, codeLines);
            }

            codeLines.Add(ExpandTabs(line));
            index++;
        }

        return new CodeBlock(language, codeLines);
    }

    private BlockQuoteBlock ParseBlockQuote(string[] lines, ref int index)
    {
        StringBuilder builder = new();

        while (index < lines.Length && IsBlockQuoteLine(lines[index]))
        {
            string trimmedStart = lines[index].TrimStart();
            string content = trimmedStart.Length > 0 && trimmedStart[0] == '>'
                ? trimmedStart[1..]
                : trimmedStart;

            if (content.StartsWith(' '))
            {
                content = content[1..];
            }

            if (builder.Length > 0)
            {
                builder.Append(' ');
            }

            builder.Append(content.Trim());
            index++;
        }

        return new BlockQuoteBlock(InlineParser.Parse(builder.ToString(), _references));
    }

    private ListBlock ParseListBlock(string[] lines, ref int index)
    {
        List<ListItemBlock> items = new();

        if (!TryParseListItemLine(lines[index], out ParsedListItem firstItem))
        {
            throw new InvalidOperationException("ParseListBlock was called when the current line was not a list item.");
        }

        bool ordered = firstItem.Ordered;

        while (index < lines.Length)
        {
            if (IsBlank(lines[index]))
            {
                break;
            }

            if (!TryParseListItemLine(lines[index], out ParsedListItem item))
            {
                break;
            }

            if (item.Ordered != ordered)
            {
                break;
            }

            string itemTextValue = item.Text.TrimEnd();
            bool? taskChecked = TryExtractTaskState(ref itemTextValue);
            StringBuilder itemText = new(itemTextValue);
            index++;

            while (index < lines.Length)
            {
                string continuation = lines[index];

                if (IsBlank(continuation))
                {
                    break;
                }

                if (TryParseListItemLine(continuation, out _))
                {
                    break;
                }

                if (StartsNewBlock(continuation))
                {
                    break;
                }

                if (CountLeadingSpaces(ExpandTabs(continuation)) > item.IndentSpaces)
                {
                    itemText.Append(' ');
                    itemText.Append(continuation.Trim());
                    index++;
                    continue;
                }

                break;
            }

            items.Add(new ListItemBlock(
                Inlines: InlineParser.Parse(itemText.ToString().Trim(), _references),
                Level: item.Level,
                Number: item.Number,
                TaskChecked: taskChecked));
        }

        return new ListBlock(ordered, items);
    }

    private ParagraphBlock ParseParagraph(string[] lines, ref int index)
    {
        StringBuilder builder = new();

        while (index < lines.Length)
        {
            string line = lines[index];

            if (IsBlank(line) || StartsNewBlock(line))
            {
                break;
            }

            if (builder.Length > 0)
            {
                builder.Append(' ');
            }

            builder.Append(line.Trim());
            index++;
        }

        return new ParagraphBlock(InlineParser.Parse(builder.ToString(), _references));
    }

    private bool StartsNewBlock(string line)
    {
        if (IsBlank(line))
        {
            return true;
        }

        if (TryGetFenceStart(line, out _, out _))
        {
            return true;
        }

        if (TryParseHeading(line, out _))
        {
            return true;
        }

        if (IsHorizontalRule(line))
        {
            return true;
        }

        if (IsBlockQuoteLine(line))
        {
            return true;
        }

        if (TryParseListItemLine(line, out _))
        {
            return true;
        }

        return false;
    }

    private bool TryParseSetextHeading(string[] lines, int index, [NotNullWhen(true)] out HeadingBlock? heading)
    {
        heading = null;

        if (index + 1 >= lines.Length)
        {
            return false;
        }

        string textLine = lines[index];
        string underline = lines[index + 1];

        if (IsBlank(textLine) || IsBlank(underline))
        {
            return false;
        }

        Match match = SetextHeadingRegex.Match(underline);
        if (!match.Success)
        {
            return false;
        }

        if (TryGetFenceStart(textLine, out _, out _) || TryParseHeading(textLine, out _) || IsHorizontalRule(textLine) || IsBlockQuoteLine(textLine) || TryParseListItemLine(textLine, out _) || ContainsUnescapedPipe(textLine))
        {
            return false;
        }

        int level = match.Groups[1].Value[0] == '=' ? 1 : 2;
        heading = new HeadingBlock(level, InlineParser.Parse(textLine.Trim(), _references));
        return true;
    }

    private bool TryParseTable(string[] lines, int index, [NotNullWhen(true)] out TableBlock? table, out int nextIndex)
    {
        table = null;
        nextIndex = index;

        if (index + 1 >= lines.Length)
        {
            return false;
        }

        string headerLine = lines[index];
        string separatorLine = lines[index + 1];

        if (!ContainsUnescapedPipe(headerLine) || !IsTableSeparatorLine(separatorLine, out List<TableColumnAlignment> alignments))
        {
            return false;
        }

        List<string> headerCells = SplitTableCells(headerLine);
        int columnCount = Math.Max(headerCells.Count, alignments.Count);
        if (columnCount == 0)
        {
            return false;
        }

        PadList(headerCells, columnCount, string.Empty);
        while (alignments.Count < columnCount)
        {
            alignments.Add(TableColumnAlignment.Left);
        }

        List<TableCell> header = headerCells
            .Take(columnCount)
            .Select(cell => new TableCell(InlineParser.Parse(cell.Trim(), _references)))
            .ToList();

        List<TableRow> bodyRows = new();
        int i = index + 2;

        while (i < lines.Length)
        {
            string line = lines[i];

            if (IsBlank(line) || TryGetFenceStart(line, out _, out _) || TryParseHeading(line, out _) || IsHorizontalRule(line) || IsBlockQuoteLine(line))
            {
                break;
            }

            if (!ContainsUnescapedPipe(line))
            {
                break;
            }

            List<string> rawCells = SplitTableCells(line);
            PadList(rawCells, columnCount, string.Empty);

            List<TableCell> cells = rawCells
                .Take(columnCount)
                .Select(cell => new TableCell(InlineParser.Parse(cell.Trim(), _references)))
                .ToList();

            bodyRows.Add(new TableRow(cells));
            i++;
        }

        table = new TableBlock(header, bodyRows, alignments.Take(columnCount).ToList());
        nextIndex = i;
        return true;
    }

    private bool TryParseHeading(string line, [NotNullWhen(true)] out HeadingBlock? heading)
    {
        Match match = HeadingRegex.Match(line);
        if (!match.Success)
        {
            heading = null;
            return false;
        }

        int level = match.Groups[1].Value.Length;
        string content = match.Groups[2].Value.Trim();

        while (content.EndsWith('#') && content.Length > 0)
        {
            content = content[..^1].TrimEnd();
        }

        heading = new HeadingBlock(level, InlineParser.Parse(content, _references));
        return true;
    }

    private static bool TryGetFenceStart(string line, out string marker, out string language)
    {
        string trimmed = line.TrimStart();

        if (trimmed.StartsWith("```", StringComparison.Ordinal))
        {
            marker = "```";
            language = trimmed.Length > 3 ? trimmed[3..].Trim() : string.Empty;
            return true;
        }

        if (trimmed.StartsWith("~~~", StringComparison.Ordinal))
        {
            marker = "~~~";
            language = trimmed.Length > 3 ? trimmed[3..].Trim() : string.Empty;
            return true;
        }

        marker = string.Empty;
        language = string.Empty;
        return false;
    }

    private static bool TryParseListItemLine(string line, out ParsedListItem item)
    {
        string expanded = ExpandTabs(line);
        Match match = ListItemRegex.Match(expanded);
        if (!match.Success)
        {
            item = default;
            return false;
        }

        string indent = match.Groups[1].Value;
        string marker = match.Groups[2].Value;
        string text = match.Groups[3].Value;

        bool ordered = char.IsDigit(marker[0]);
        int number = 0;

        if (ordered)
        {
            string numberText = marker.TrimEnd('.', ')');
            _ = int.TryParse(numberText, NumberStyles.Integer, CultureInfo.InvariantCulture, out number);
        }

        int indentSpaces = CountLeadingSpaces(indent);
        int level = Math.Min(6, indentSpaces / 2);

        item = new ParsedListItem(
            Ordered: ordered,
            Number: number,
            Level: level,
            IndentSpaces: indentSpaces,
            Text: text);

        return true;
    }

    private static bool? TryExtractTaskState(ref string itemText)
    {
        string trimmed = itemText.TrimStart();
        int leadingTrimCount = itemText.Length - trimmed.Length;

        if (trimmed.Length >= 3 && trimmed[0] == '[' && trimmed[2] == ']' && (trimmed[1] == ' ' || trimmed[1] == 'x' || trimmed[1] == 'X'))
        {
            bool checkedState = trimmed[1] == 'x' || trimmed[1] == 'X';
            string remainder = trimmed.Length > 3 ? trimmed[3..].TrimStart() : string.Empty;
            itemText = itemText[..leadingTrimCount] + remainder;
            return checkedState;
        }

        return null;
    }

    private static bool IsHorizontalRule(string line)
    {
        string trimmed = line.Trim();

        if (trimmed.Length < 3)
        {
            return false;
        }

        return HorizontalRuleRegex.IsMatch(line);
    }

    private static bool IsBlockQuoteLine(string line)
    {
        return line.TrimStart().StartsWith('>');
    }

    private static bool IsBlank(string line)
    {
        return string.IsNullOrWhiteSpace(line);
    }

    private static string ExpandTabs(string value)
    {
        return value.Replace("\t", "    ", StringComparison.Ordinal);
    }

    private static int CountLeadingSpaces(string value)
    {
        int count = 0;
        while (count < value.Length && value[count] == ' ')
        {
            count++;
        }

        return count;
    }

    private static bool ContainsUnescapedPipe(string line)
    {
        bool escaped = false;

        foreach (char c in line)
        {
            if (escaped)
            {
                escaped = false;
                continue;
            }

            if (c == '\\')
            {
                escaped = true;
                continue;
            }

            if (c == '|')
            {
                return true;
            }
        }

        return false;
    }

    private static bool IsTableSeparatorLine(string line, out List<TableColumnAlignment> alignments)
    {
        alignments = new List<TableColumnAlignment>();

        if (!ContainsUnescapedPipe(line))
        {
            return false;
        }

        List<string> cells = SplitTableCells(line);
        if (cells.Count == 0)
        {
            return false;
        }

        foreach (string raw in cells)
        {
            string cell = raw.Trim();
            if (cell.Length < 3)
            {
                return false;
            }

            bool leftColon = cell.StartsWith(':');
            bool rightColon = cell.EndsWith(':');
            string dashes = cell.Trim(':').Trim();

            if (dashes.Length < 3 || dashes.Any(ch => ch != '-'))
            {
                return false;
            }

            alignments.Add((leftColon, rightColon) switch
            {
                (true, true) => TableColumnAlignment.Center,
                (false, true) => TableColumnAlignment.Right,
                _ => TableColumnAlignment.Left
            });
        }

        return true;
    }

    private static List<string> SplitTableCells(string line)
    {
        string work = line.Trim();

        if (work.StartsWith('|'))
        {
            work = work[1..];
        }

        if (work.EndsWith('|'))
        {
            work = work[..^1];
        }

        List<string> cells = new();
        StringBuilder current = new();
        bool escaped = false;

        foreach (char c in work)
        {
            if (escaped)
            {
                current.Append(c);
                escaped = false;
                continue;
            }

            if (c == '\\')
            {
                escaped = true;
                continue;
            }

            if (c == '|')
            {
                cells.Add(current.ToString());
                current.Clear();
                continue;
            }

            current.Append(c);
        }

        cells.Add(current.ToString());
        return cells;
    }

    private static void PadList<T>(List<T> values, int desiredCount, T padValue)
    {
        while (values.Count < desiredCount)
        {
            values.Add(padValue);
        }
    }

    private readonly record struct ParsedListItem(
        bool Ordered,
        int Number,
        int Level,
        int IndentSpaces,
        string Text);
}

internal static class InlineParser
{
    private static readonly Regex EmailRegex = new(@"^[^@\s<>]+@[^@\s<>]+\.[^@\s<>]+$", RegexOptions.Compiled);

    public static List<InlineRun> Parse(string text, IReadOnlyDictionary<string, string>? references = null)
    {
        List<InlineRun> runs = new();
        ParseInto(text, new InlineStyle(), runs, references ?? new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase));
        return runs;
    }

    private static void ParseInto(string text, InlineStyle style, List<InlineRun> runs, IReadOnlyDictionary<string, string> references)
    {
        int i = 0;

        while (i < text.Length)
        {
            if (text[i] == '\\' && i + 1 < text.Length)
            {
                AddRun(runs, text[i + 1].ToString(), style);
                i += 2;
                continue;
            }

            if (text[i] == '`')
            {
                int end = text.IndexOf('`', i + 1);
                if (end > i)
                {
                    string code = text.Substring(i + 1, end - i - 1);
                    AddRun(runs, code, style with { Code = true });
                    i = end + 1;
                    continue;
                }

                AddRun(runs, "`", style);
                i++;
                continue;
            }

            if (StartsWith(text, i, "~~"))
            {
                if (TryReadStrikethrough(text, i, style, runs, references, out int next))
                {
                    i = next;
                    continue;
                }

                AddRun(runs, "~", style);
                i++;
                continue;
            }

            if (i + 1 < text.Length && text[i] == '!' && text[i + 1] == '[')
            {
                if (TryReadBracketLink(text, i, image: true, out string alt, out string url, out int next))
                {
                    string cleanAlt = string.IsNullOrWhiteSpace(alt) ? url : alt.Trim();
                    string rendered = string.IsNullOrWhiteSpace(cleanAlt)
                        ? "[Image omitted]"
                        : $"[Image omitted: {cleanAlt}]";

                    AddRun(runs, rendered, style with { Italic = true });
                    i = next;
                    continue;
                }

                AddRun(runs, "!", style);
                i++;
                continue;
            }

            if (text[i] == '[')
            {
                if (TryReadBracketLink(text, i, image: false, out string label, out string url, out int next))
                {
                    AddLinkRun(runs, label, url, style);
                    i = next;
                    continue;
                }

                if (TryReadReferenceLink(text, i, references, out label, out url, out next))
                {
                    AddLinkRun(runs, label, url, style);
                    i = next;
                    continue;
                }

                AddRun(runs, "[", style);
                i++;
                continue;
            }

            if (text[i] == '<')
            {
                if (TryReadAutoLink(text, i, out string label, out string url, out int next))
                {
                    AddLinkRun(runs, label, url, style);
                    i = next;
                    continue;
                }

                AddRun(runs, "<", style);
                i++;
                continue;
            }

            if (StartsWith(text, i, "http://") || StartsWith(text, i, "https://"))
            {
                if (TryReadBareUrl(text, i, out string url, out int next))
                {
                    AddLinkRun(runs, url, url, style);
                    i = next;
                    continue;
                }
            }

            if (text[i] == '*' || text[i] == '_')
            {
                if (TryReadEmphasis(text, i, style, runs, references, out int next))
                {
                    i = next;
                    continue;
                }

                AddRun(runs, text[i].ToString(), style);
                i++;
                continue;
            }

            int nextSpecial = FindNextSpecial(text, i);
            string plain = text.Substring(i, nextSpecial - i);
            AddRun(runs, plain, style);
            i = nextSpecial;
        }
    }

    private static bool TryReadBracketLink(
        string text,
        int start,
        bool image,
        out string label,
        out string url,
        out int nextIndex)
    {
        int labelStart = start + (image ? 2 : 1);
        int closeBracket = text.IndexOf(']', labelStart);

        if (closeBracket < 0 || closeBracket + 1 >= text.Length || text[closeBracket + 1] != '(')
        {
            label = string.Empty;
            url = string.Empty;
            nextIndex = start;
            return false;
        }

        int urlStart = closeBracket + 2;
        int closeParen = text.IndexOf(')', urlStart);

        if (closeParen < 0)
        {
            label = string.Empty;
            url = string.Empty;
            nextIndex = start;
            return false;
        }

        label = text.Substring(labelStart, closeBracket - labelStart);
        url = text.Substring(urlStart, closeParen - urlStart).Trim();
        nextIndex = closeParen + 1;
        return true;
    }

    private static bool TryReadReferenceLink(
        string text,
        int start,
        IReadOnlyDictionary<string, string> references,
        out string label,
        out string url,
        out int nextIndex)
    {
        label = string.Empty;
        url = string.Empty;
        nextIndex = start;

        int closeLabel = text.IndexOf(']', start + 1);
        if (closeLabel < 0)
        {
            return false;
        }

        string firstLabel = text.Substring(start + 1, closeLabel - start - 1);

        if (closeLabel + 1 < text.Length && text[closeLabel + 1] == '[')
        {
            int closeRef = text.IndexOf(']', closeLabel + 2);
            if (closeRef < 0)
            {
                return false;
            }

            string referenceLabel = text.Substring(closeLabel + 2, closeRef - closeLabel - 2);
            if (referenceLabel.Length == 0)
            {
                referenceLabel = firstLabel;
            }

            if (references.TryGetValue(NormalizeReferenceLabel(referenceLabel), out string? foundUrl))
            {
                label = firstLabel;
                url = foundUrl;
                nextIndex = closeRef + 1;
                return true;
            }
        }

        if (references.TryGetValue(NormalizeReferenceLabel(firstLabel), out string? shortcutUrl))
        {
            label = firstLabel;
            url = shortcutUrl;
            nextIndex = closeLabel + 1;
            return true;
        }

        return false;
    }

    private static bool TryReadAutoLink(string text, int start, out string label, out string url, out int nextIndex)
    {
        label = string.Empty;
        url = string.Empty;
        nextIndex = start;

        int close = text.IndexOf('>', start + 1);
        if (close < 0)
        {
            return false;
        }

        string candidate = text.Substring(start + 1, close - start - 1).Trim();

        if (candidate.StartsWith("http://", StringComparison.OrdinalIgnoreCase) || candidate.StartsWith("https://", StringComparison.OrdinalIgnoreCase))
        {
            label = candidate;
            url = candidate;
            nextIndex = close + 1;
            return true;
        }

        if (EmailRegex.IsMatch(candidate))
        {
            label = candidate;
            url = "mailto:" + candidate;
            nextIndex = close + 1;
            return true;
        }

        return false;
    }

    private static bool TryReadBareUrl(string text, int start, out string url, out int nextIndex)
    {
        int index = start;
        while (index < text.Length && !char.IsWhiteSpace(text[index]) && text[index] != '<' && text[index] != '>')
        {
            index++;
        }

        url = text.Substring(start, index - start).TrimEnd('.', ',', ';', ':', ')', ']');
        nextIndex = start + url.Length;
        return url.Length > 0;
    }

    private static bool TryReadStrikethrough(
        string text,
        int start,
        InlineStyle style,
        List<InlineRun> runs,
        IReadOnlyDictionary<string, string> references,
        out int nextIndex)
    {
        nextIndex = start;

        if (!CanOpenSimpleDelimiter(text, start, 2))
        {
            return false;
        }

        int index = start + 2;
        while (index < text.Length)
        {
            int found = text.IndexOf("~~", index, StringComparison.Ordinal);
            if (found < 0)
            {
                return false;
            }

            if ((found == 0 || text[found - 1] != '\\') && CanCloseSimpleDelimiter(text, found, 2))
            {
                string inner = text.Substring(start + 2, found - start - 2);
                ParseInto(inner, style with { Strike = true }, runs, references);
                nextIndex = found + 2;
                return true;
            }

            index = found + 2;
        }

        return false;
    }

    private static bool TryReadEmphasis(
        string text,
        int start,
        InlineStyle style,
        List<InlineRun> runs,
        IReadOnlyDictionary<string, string> references,
        out int nextIndex)
    {
        nextIndex = start;
        char delimiter = text[start];
        int runLength = CountRepeated(text, start, delimiter);
        int maxLength = Math.Min(3, runLength);

        for (int length = maxLength; length >= 1; length--)
        {
            if (!CanOpenEmphasis(text, start, length, delimiter))
            {
                continue;
            }

            int close = FindClosingEmphasis(text, delimiter, length, start + length);
            if (close < 0)
            {
                continue;
            }

            string inner = text.Substring(start + length, close - start - length);
            InlineStyle nestedStyle = length switch
            {
                3 => style with { Bold = true, Italic = true },
                2 => style with { Bold = true },
                _ => style with { Italic = true }
            };

            ParseInto(inner, nestedStyle, runs, references);
            nextIndex = close + length;
            return true;
        }

        return false;
    }

    private static int FindClosingEmphasis(string text, char delimiter, int length, int startIndex)
    {
        string marker = new(delimiter, length);
        int index = startIndex;

        while (index < text.Length)
        {
            int found = text.IndexOf(marker, index, StringComparison.Ordinal);
            if (found < 0)
            {
                return -1;
            }

            if ((found == 0 || text[found - 1] != '\\') && CanCloseEmphasis(text, found, length, delimiter))
            {
                return found;
            }

            index = found + length;
        }

        return -1;
    }

    private static bool CanOpenSimpleDelimiter(string text, int start, int length)
    {
        char after = CharAtOrNull(text, start + length);
        return after != '\0' && !char.IsWhiteSpace(after);
    }

    private static bool CanCloseSimpleDelimiter(string text, int start, int length)
    {
        char before = CharAtOrNull(text, start - 1);
        return before != '\0' && !char.IsWhiteSpace(before);
    }

    private static bool CanOpenEmphasis(string text, int start, int length, char delimiter)
    {
        (bool leftFlanking, bool rightFlanking, char before, _) = GetFlanking(text, start, length);

        if (delimiter == '_')
        {
            return leftFlanking && (!rightFlanking || IsPunctuation(before));
        }

        return leftFlanking;
    }

    private static bool CanCloseEmphasis(string text, int start, int length, char delimiter)
    {
        (bool leftFlanking, bool rightFlanking, _, char after) = GetFlanking(text, start, length);

        if (delimiter == '_')
        {
            return rightFlanking && (!leftFlanking || IsPunctuation(after));
        }

        return rightFlanking;
    }

    private static (bool LeftFlanking, bool RightFlanking, char Before, char After) GetFlanking(string text, int start, int length)
    {
        char before = CharAtOrNull(text, start - 1);
        char after = CharAtOrNull(text, start + length);

        bool beforeWhitespace = before == '\0' || char.IsWhiteSpace(before);
        bool afterWhitespace = after == '\0' || char.IsWhiteSpace(after);
        bool beforePunctuation = before != '\0' && IsPunctuation(before);
        bool afterPunctuation = after != '\0' && IsPunctuation(after);

        bool leftFlanking = !afterWhitespace && (!afterPunctuation || beforeWhitespace || beforePunctuation);
        bool rightFlanking = !beforeWhitespace && (!beforePunctuation || afterWhitespace || afterPunctuation);

        return (leftFlanking, rightFlanking, before, after);
    }

    private static char CharAtOrNull(string text, int index)
    {
        return index >= 0 && index < text.Length ? text[index] : '\0';
    }

    private static bool IsPunctuation(char c)
    {
        if (c == '\0')
        {
            return false;
        }

        UnicodeCategory category = char.GetUnicodeCategory(c);
        return category is UnicodeCategory.ConnectorPunctuation
            or UnicodeCategory.DashPunctuation
            or UnicodeCategory.OpenPunctuation
            or UnicodeCategory.ClosePunctuation
            or UnicodeCategory.InitialQuotePunctuation
            or UnicodeCategory.FinalQuotePunctuation
            or UnicodeCategory.OtherPunctuation;
    }

    private static int CountRepeated(string text, int start, char c)
    {
        int count = 0;
        while (start + count < text.Length && text[start + count] == c)
        {
            count++;
        }

        return count;
    }

    private static bool StartsWith(string text, int index, string value)
    {
        return index + value.Length <= text.Length &&
               string.CompareOrdinal(text, index, value, 0, value.Length) == 0;
    }

    private static int FindNextSpecial(string text, int start)
    {
        int i = start;

        while (i < text.Length)
        {
            char c = text[i];

            if (c is '\\' or '`' or '*' or '_' or '[' or '<')
            {
                return i;
            }

            if (c == '~' && i + 1 < text.Length && text[i + 1] == '~')
            {
                return i;
            }

            if (c == '!' && i + 1 < text.Length && text[i + 1] == '[')
            {
                return i;
            }

            if ((c == 'h' || c == 'H') &&
                (StartsWith(text, i, "http://") || StartsWith(text, i, "https://")))
            {
                return i;
            }

            i++;
        }

        return text.Length;
    }

    private static void AddRun(List<InlineRun> runs, string text, InlineStyle style)
    {
        if (string.IsNullOrEmpty(text))
        {
            return;
        }

        AddSpecialRun(runs, new InlineRun(
            Text: WebUtility.HtmlDecode(text),
            Bold: style.Bold,
            Italic: style.Italic,
            Code: style.Code,
            Strike: style.Strike,
            Link: false,
            Url: null));
    }

    private static void AddLinkRun(List<InlineRun> runs, string label, string url, InlineStyle style)
    {
        string cleanUrl = WebUtility.HtmlDecode(url.Trim()) ?? string.Empty;
        string cleanLabel = WebUtility.HtmlDecode(label.Trim()) ?? string.Empty;

        // Markdown links render only their anchor text. The destination URL is stored
        // separately on the InlineRun and is used later to create the clickable PDF
        // URI annotation. For bare URLs and autolinks, the label and URL are the same,
        // so the URL remains visible because it is the intended anchor text.
        string visibleText = string.IsNullOrWhiteSpace(cleanLabel) ? cleanUrl : cleanLabel;

        AddSpecialRun(runs, new InlineRun(
            Text: visibleText,
            Bold: style.Bold,
            Italic: style.Italic,
            Code: style.Code,
            Strike: style.Strike,
            Link: true,
            Url: cleanUrl));
    }

    private static void AddSpecialRun(List<InlineRun> runs, InlineRun run)
    {
        if (string.IsNullOrEmpty(run.Text))
        {
            return;
        }

        if (runs.Count > 0 && runs[^1].HasSameStyle(run))
        {
            InlineRun previous = runs[^1];
            runs[^1] = previous with { Text = previous.Text + run.Text };
        }
        else
        {
            runs.Add(run);
        }
    }

    private static string NormalizeReferenceLabel(string value)
    {
        return Regex.Replace(value.Trim(), @"\s+", " ");
    }

    private readonly record struct InlineStyle(
        bool Bold = false,
        bool Italic = false,
        bool Code = false,
        bool Strike = false);
}

internal sealed class PdfMarkdownRenderer
{
    private readonly Document _document;
    private readonly PdfTheme _theme;
    private readonly PdfFontSet _fonts;
    private readonly PdfTaggingContext _tags;

    public PdfMarkdownRenderer(Document document, PdfTheme theme, ConversionOptions options)
    {
        _document = document;
        _theme = theme;
        _fonts = new PdfFontSet(options.FontFamily, options.HeadingFontFamily, options.CodeFontFamily, options.FallbackFontNames);
        _tags = new PdfTaggingContext(document, options.Language);
    }

    public void Render(MarkdownDocument markdownDocument)
    {
        PdfLayoutContext context = new(_document, _theme);

        foreach (MarkdownBlock block in markdownDocument.Blocks)
        {
            switch (block)
            {
                case HeadingBlock heading:
                    RenderHeading(context, heading);
                    break;

                case ParagraphBlock paragraph:
                    RenderParagraph(context, paragraph);
                    break;

                case ListBlock list:
                    RenderList(context, list);
                    break;

                case CodeBlock code:
                    RenderCodeBlock(context, code);
                    break;

                case BlockQuoteBlock quote:
                    RenderBlockQuote(context, quote);
                    break;

                case TableBlock table:
                    RenderTable(context, table);
                    break;

                case HorizontalRuleBlock:
                    RenderHorizontalRule(context);
                    break;
            }
        }

        context.Finish();
        _tags.Finish();
    }

    private void RenderHeading(PdfLayoutContext context, HeadingBlock heading)
    {
        int level = Math.Clamp(heading.Level, 1, 6);
        double fontSize = _theme.HeadingFontSizes[level - 1];
        double lineHeight = fontSize * 1.25;

        PdfTaggedElement headingTag = _tags.CreateElement($"H{level}", _tags.DocumentElement);

        AddSpaceBefore(context, _theme.HeadingSpaceBefore[level - 1]);

        List<InlineRun> headingRuns = heading.Inlines
            .Select(run => run.Code ? run : run with { Bold = true })
            .ToList();

        DrawWrappedInline(
            context,
            headingRuns,
            x: _theme.MarginLeft,
            maxWidth: _theme.ContentWidth,
            fontSize: fontSize,
            lineHeight: lineHeight,
            ownerTag: headingTag,
            forceItalic: false,
            fontRole: PdfFontRole.Heading);

        context.MoveDown(_theme.HeadingSpaceAfter[level - 1]);
    }

    private void RenderParagraph(PdfLayoutContext context, ParagraphBlock paragraph)
    {
        PdfTaggedElement paragraphTag = _tags.CreateElement("P", _tags.DocumentElement);

        DrawWrappedInline(
            context,
            paragraph.Inlines,
            x: _theme.MarginLeft,
            maxWidth: _theme.ContentWidth,
            fontSize: _theme.BodyFontSize,
            lineHeight: _theme.BodyLineHeight,
            ownerTag: paragraphTag,
            forceItalic: false);

        context.MoveDown(_theme.ParagraphSpaceAfter);
    }

    private void RenderList(PdfLayoutContext context, ListBlock list)
    {
        PdfTaggedElement listTag = _tags.CreateElement("L", _tags.DocumentElement);

        foreach (ListItemBlock item in list.Items)
        {
            PdfTaggedElement listItemTag = _tags.CreateElement("LI", listTag);
            PdfTaggedElement labelTag = _tags.CreateElement("Lbl", listItemTag);
            PdfTaggedElement bodyTag = _tags.CreateElement("LBody", listItemTag);

            int level = Math.Clamp(item.Level, 0, 6);

            double markerX = _theme.MarginLeft + level * _theme.ListIndent;
            double textX = markerX + _theme.ListMarkerWidth;
            double maxWidth = _theme.PageWidth - _theme.MarginRight - textX;

            string marker = item.TaskChecked.HasValue
                ? (item.TaskChecked.Value ? "[x]" : "[ ]")
                : (list.Ordered ? $"{Math.Max(1, item.Number)}." : "\u2022");

            List<List<InlineRun>> wrappedLines = WrapInlineRuns(
                item.Inlines,
                maxWidth,
                _theme.BodyFontSize,
                forceItalic: false);

            for (int lineIndex = 0; lineIndex < wrappedLines.Count; lineIndex++)
            {
                double baseline = context.BeginLine(_theme.BodyLineHeight, _theme.BodyFontSize);

                if (lineIndex == 0)
                {
                    DrawSingleTextRun(
                        context,
                        marker,
                        _fonts.Regular,
                        _theme.BodyFontSize,
                        markerX,
                        baseline,
                        link: false,
                        url: null,
                        ownerTag: labelTag);
                }

                DrawInlineRunsAt(
                    context,
                    wrappedLines[lineIndex],
                    textX,
                    baseline,
                    _theme.BodyFontSize,
                    ownerTag: bodyTag,
                    forceItalic: false);

                context.EndLine(_theme.BodyLineHeight);
            }

            context.MoveDown(_theme.ListItemSpaceAfter);
        }

        context.MoveDown(_theme.ListSpaceAfter);
    }

    private void RenderCodeBlock(PdfLayoutContext context, CodeBlock code)
    {
        PdfTaggedElement codeTag = _tags.CreateElement("Code", _tags.DocumentElement);

        AddSpaceBefore(context, _theme.CodeBlockSpaceBefore);

        double blockLeft = _theme.MarginLeft;
        double blockRight = _theme.PageWidth - _theme.MarginRight;
        double textX = blockLeft + _theme.CodeBlockPadding;
        double maxWidth = Math.Max(12.0, blockRight - blockLeft - 2.0 * _theme.CodeBlockPadding);

        string languageLabel = NormalizeCodeLanguageLabel(code.Language);
        if (!string.IsNullOrWhiteSpace(languageLabel))
        {
            double baseline = context.BeginLine(_theme.CodeTitleLineHeight, _theme.CodeTitleFontSize);
            double rowTop = context.CursorY;
            double rowBottom = rowTop - _theme.CodeTitleLineHeight;
            DrawFilledRectangleArtifact(context, blockLeft, rowBottom, blockRight, rowTop, _theme.CodeBlockBackgroundGray);

            DrawSingleTextRun(
                context,
                "</> " + languageLabel,
                _fonts.Bold,
                _theme.CodeTitleFontSize,
                textX,
                baseline,
                link: false,
                url: null,
                ownerTag: null);

            context.EndLine(_theme.CodeTitleLineHeight);
        }

        if (code.Lines.Count == 0)
        {
            double baseline = context.BeginLine(_theme.CodeLineHeight, _theme.CodeFontSize);
            double rowTop = context.CursorY;
            double rowBottom = rowTop - _theme.CodeLineHeight;
            DrawFilledRectangleArtifact(context, blockLeft, rowBottom, blockRight, rowTop, _theme.CodeBlockBackgroundGray);
            _ = baseline;
            context.EndLine(_theme.CodeLineHeight);
        }

        foreach (string rawLine in code.Lines)
        {
            List<string> wrapped = WrapPreformattedLine(rawLine, maxWidth, _theme.CodeFontSize);

            if (wrapped.Count == 0)
            {
                wrapped.Add(string.Empty);
            }

            foreach (string visualLine in wrapped)
            {
                double baseline = context.BeginLine(_theme.CodeLineHeight, _theme.CodeFontSize);
                double rowTop = context.CursorY;
                double rowBottom = rowTop - _theme.CodeLineHeight;
                DrawFilledRectangleArtifact(context, blockLeft, rowBottom, blockRight, rowTop, _theme.CodeBlockBackgroundGray);

                if (visualLine.Length > 0)
                {
                    DrawSingleTextRun(
                        context,
                        visualLine,
                        _fonts.Code,
                        _theme.CodeFontSize,
                        textX,
                        baseline,
                        link: false,
                        url: null,
                        ownerTag: codeTag);
                }

                context.EndLine(_theme.CodeLineHeight);
            }
        }

        context.MoveDown(_theme.CodeBlockSpaceAfter);
    }

    private static string NormalizeCodeLanguageLabel(string language)
    {
        string value = language.Trim();

        if (value.StartsWith('[') && value.EndsWith(']') && value.Length > 2)
        {
            value = value[1..^1].Trim();
        }

        return value.ToLowerInvariant() switch
        {
            "cmd" or "commandline" or "command-line" or "shell" => "Command line",
            "ps" or "pwsh" or "powershell" => "PowerShell",
            "py" or "python" => "Python",
            "cs" or "csharp" => "C#",
            "js" or "javascript" => "JavaScript",
            "ts" or "typescript" => "TypeScript",
            "json" => "JSON",
            "xml" => "XML",
            "html" => "HTML",
            "css" => "CSS",
            "bash" => "Bash",
            _ => value
        };
    }

    private void RenderBlockQuote(PdfLayoutContext context, BlockQuoteBlock quote)
    {
        PdfTaggedElement quoteTag = _tags.CreateElement("BlockQuote", _tags.DocumentElement);

        AddSpaceBefore(context, _theme.BlockQuoteSpaceBefore);

        double markerX = _theme.MarginLeft;
        double textX = _theme.MarginLeft + _theme.BlockQuoteIndent;
        double maxWidth = _theme.PageWidth - _theme.MarginRight - textX;

        List<List<InlineRun>> wrappedLines = WrapInlineRuns(
            quote.Inlines,
            maxWidth,
            _theme.BodyFontSize,
            forceItalic: true);

        for (int lineIndex = 0; lineIndex < wrappedLines.Count; lineIndex++)
        {
            double baseline = context.BeginLine(_theme.BodyLineHeight, _theme.BodyFontSize);

            if (lineIndex == 0)
            {
                DrawSingleTextRun(
                    context,
                    ">",
                    _fonts.Italic,
                    _theme.BodyFontSize,
                    markerX,
                    baseline,
                    link: false,
                    url: null,
                    ownerTag: null);
            }

            DrawInlineRunsAt(
                context,
                wrappedLines[lineIndex],
                textX,
                baseline,
                _theme.BodyFontSize,
                ownerTag: quoteTag,
                forceItalic: true);

            context.EndLine(_theme.BodyLineHeight);
        }

        context.MoveDown(_theme.BlockQuoteSpaceAfter);
    }

    private void RenderTable(PdfLayoutContext context, TableBlock table)
    {
        if (table.HeaderCells.Count == 0)
        {
            return;
        }

        AddSpaceBefore(context, _theme.TableSpaceBefore);

        PdfTaggedElement tableTag = _tags.CreateElement("Table", _tags.DocumentElement);

        int columnCount = table.HeaderCells.Count;
        double tableX = _theme.MarginLeft;
        double tableWidth = _theme.ContentWidth;
        double columnWidth = tableWidth / columnCount;
        List<double> columnWidths = Enumerable.Repeat(columnWidth, columnCount).ToList();

        RenderTableRow(
            context,
            tableTag,
            table.HeaderCells,
            columnWidths,
            table.Alignments,
            isHeader: true);

        foreach (TableRow row in table.Rows)
        {
            List<TableCell> cells = row.Cells.ToList();
            while (cells.Count < columnCount)
            {
                cells.Add(new TableCell(new List<InlineRun>()));
            }

            RenderTableRow(
                context,
                tableTag,
                cells.Take(columnCount).ToList(),
                columnWidths,
                table.Alignments,
                isHeader: false);
        }

        context.MoveDown(_theme.TableSpaceAfter);
    }

    private void RenderTableRow(
        PdfLayoutContext context,
        PdfTaggedElement tableTag,
        IReadOnlyList<TableCell> cells,
        IReadOnlyList<double> columnWidths,
        IReadOnlyList<TableColumnAlignment> alignments,
        bool isHeader)
    {
        PdfTaggedElement rowTag = _tags.CreateElement("TR", tableTag);
        List<CellLayout> cellLayouts = new();

        for (int cellIndex = 0; cellIndex < columnWidths.Count; cellIndex++)
        {
            IReadOnlyList<InlineRun> sourceRuns = cellIndex < cells.Count ? cells[cellIndex].Inlines : Array.Empty<InlineRun>();
            List<InlineRun> styledRuns = isHeader
                ? sourceRuns.Select(run => run.Code ? run : run with { Bold = true }).ToList()
                : sourceRuns.ToList();

            double innerWidth = Math.Max(12.0, columnWidths[cellIndex] - 2.0 * _theme.TableCellPadding);
            List<List<InlineRun>> lines = WrapInlineRuns(styledRuns, innerWidth, _theme.TableFontSize, forceItalic: false);
            PdfTaggedElement cellTag = _tags.CreateElement(isHeader ? "TH" : "TD", rowTag);
            cellLayouts.Add(new CellLayout(lines, cellTag));
        }

        int totalLines = Math.Max(1, cellLayouts.Max(cell => cell.Lines.Count));
        int maxLinesPerChunk = Math.Max(
            1,
            (int)Math.Floor((_theme.PageHeight - _theme.MarginTop - _theme.MarginBottom - 2.0 * _theme.TableCellPadding) / _theme.TableLineHeight));

        int startLine = 0;
        while (startLine < totalLines)
        {
            int lineCount = Math.Min(maxLinesPerChunk, totalLines - startLine);
            double rowHeight = lineCount * _theme.TableLineHeight + 2.0 * _theme.TableCellPadding;

            context.EnsureSpace(rowHeight);
            double rowTop = context.CursorY;
            double rowBottom = rowTop - rowHeight;

            DrawTableGrid(context, _theme.MarginLeft, rowTop, rowBottom, columnWidths);

            double cellX = _theme.MarginLeft;
            for (int cellIndex = 0; cellIndex < columnWidths.Count; cellIndex++)
            {
                CellLayout cell = cellLayouts[cellIndex];
                double textX = cellX + _theme.TableCellPadding;
                double innerWidth = Math.Max(12.0, columnWidths[cellIndex] - 2.0 * _theme.TableCellPadding);
                double baseline = rowTop - _theme.TableCellPadding - _theme.TableFontSize;

                for (int lineOffset = 0; lineOffset < lineCount; lineOffset++)
                {
                    int visualLineIndex = startLine + lineOffset;
                    if (visualLineIndex < cell.Lines.Count)
                    {
                        IReadOnlyList<InlineRun> visualLine = cell.Lines[visualLineIndex];
                        double alignedX = AlignTableLine(textX, innerWidth, visualLine, _theme.TableFontSize, alignments[cellIndex]);

                        DrawInlineRunsAt(
                            context,
                            visualLine,
                            alignedX,
                            baseline - lineOffset * _theme.TableLineHeight,
                            _theme.TableFontSize,
                            cell.Tag,
                            forceItalic: false);
                    }
                }

                cellX += columnWidths[cellIndex];
            }

            context.MoveDown(rowHeight);
            startLine += lineCount;
        }
    }

    private double AlignTableLine(
        double leftX,
        double innerWidth,
        IReadOnlyList<InlineRun> line,
        double fontSize,
        TableColumnAlignment alignment)
    {
        double lineWidth = line.Sum(run => MeasureRun(run, fontSize));

        return alignment switch
        {
            TableColumnAlignment.Right => leftX + Math.Max(0.0, innerWidth - lineWidth),
            TableColumnAlignment.Center => leftX + Math.Max(0.0, (innerWidth - lineWidth) / 2.0),
            _ => leftX
        };
    }

    private void DrawTableGrid(PdfLayoutContext context, double tableX, double rowTop, double rowBottom, IReadOnlyList<double> columnWidths)
    {
        double tableRight = tableX + columnWidths.Sum();

        DrawLineArtifact(context, tableX, rowTop, tableRight, rowTop, _theme.TableBorderWidth);
        DrawLineArtifact(context, tableX, rowBottom, tableRight, rowBottom, _theme.TableBorderWidth);

        double x = tableX;
        DrawLineArtifact(context, x, rowBottom, x, rowTop, _theme.TableBorderWidth);

        foreach (double width in columnWidths)
        {
            x += width;
            DrawLineArtifact(context, x, rowBottom, x, rowTop, _theme.TableBorderWidth);
        }
    }

    private void RenderHorizontalRule(PdfLayoutContext context)
    {
        AddSpaceBefore(context, _theme.HorizontalRuleSpaceBefore);

        context.EnsureSpace(_theme.HorizontalRuleSpaceAfter + 8.0);
        double y = context.CursorY - 4.0;

        DrawLineArtifact(context, _theme.MarginLeft, y, _theme.PageWidth - _theme.MarginRight, y, 0.75);

        context.MoveDown(_theme.HorizontalRuleSpaceAfter + 8.0);
    }

    private void DrawLineArtifact(PdfLayoutContext context, double x1, double y1, double x2, double y2, double width)
    {
        Datalogics.PDFL.Path line = new Datalogics.PDFL.Path();
        line.PaintOp = PathPaintOpFlags.Stroke;

        GraphicState graphicState = line.GraphicState;
        graphicState.Width = width;
        graphicState.StrokeColor = new Color(0.0);
        line.GraphicState = graphicState;

        line.MoveTo(new Datalogics.PDFL.Point(x1, y1));
        line.AddLine(new Datalogics.PDFL.Point(x2, y2));

        _tags.AddArtifactElement(context, line);
    }

    private void DrawFilledRectangleArtifact(PdfLayoutContext context, double left, double bottom, double right, double top, double gray)
    {
        if (right <= left || top <= bottom)
        {
            return;
        }

        Datalogics.PDFL.Path rectangle = new Datalogics.PDFL.Path();
        rectangle.PaintOp = PathPaintOpFlags.EoFill;

        GraphicState graphicState = rectangle.GraphicState;
        graphicState.FillColor = new Color(gray);
        rectangle.GraphicState = graphicState;

        rectangle.MoveTo(new Datalogics.PDFL.Point(left, bottom));
        rectangle.AddLine(new Datalogics.PDFL.Point(right, bottom));
        rectangle.AddLine(new Datalogics.PDFL.Point(right, top));
        rectangle.AddLine(new Datalogics.PDFL.Point(left, top));
        rectangle.ClosePath();

        _tags.AddArtifactElement(context, rectangle);
    }

    private void DrawWrappedInline(
        PdfLayoutContext context,
        IReadOnlyList<InlineRun> runs,
        double x,
        double maxWidth,
        double fontSize,
        double lineHeight,
        PdfTaggedElement ownerTag,
        bool forceItalic,
        PdfFontRole fontRole = PdfFontRole.Body)
    {
        List<List<InlineRun>> wrappedLines = WrapInlineRuns(runs, maxWidth, fontSize, forceItalic, fontRole);

        foreach (List<InlineRun> line in wrappedLines)
        {
            double baseline = context.BeginLine(lineHeight, fontSize);

            DrawInlineRunsAt(
                context,
                line,
                x,
                baseline,
                fontSize,
                ownerTag,
                forceItalic,
                fontRole);

            context.EndLine(lineHeight);
        }
    }

    private void DrawInlineRunsAt(
        PdfLayoutContext context,
        IReadOnlyList<InlineRun> runs,
        double x,
        double baselineY,
        double fontSize,
        PdfTaggedElement ownerTag,
        bool forceItalic,
        PdfFontRole fontRole = PdfFontRole.Body)
    {
        double currentX = x;

        foreach (InlineRun originalRun in runs)
        {
            InlineRun run = ApplyForcedStyle(originalRun, forceItalic);

            if (string.IsNullOrEmpty(run.Text))
            {
                continue;
            }

            PdfTaggedElement runTag = ownerTag;

            if (run.Link && !string.Equals(ownerTag.TagName, "Link", StringComparison.Ordinal))
            {
                runTag = _tags.CreateElement("Link", ownerTag);
                _tags.SetActualText(runTag, run.Text);
            }
            else if (run.Code && !string.Equals(ownerTag.TagName, "Code", StringComparison.Ordinal))
            {
                runTag = _tags.CreateElement("Code", ownerTag);
            }
            else if (run.Strike && !string.Equals(ownerTag.TagName, "Span", StringComparison.Ordinal))
            {
                runTag = _tags.CreateElement("Span", ownerTag);
            }

            foreach (PdfTextSegment segment in SplitRunForFontFallback(run, fontRole))
            {
                InlineRun segmentRun = segment.Run;
                Font font = segment.Font;
                double width = MeasureText(font, segmentRun.Text, fontSize);

                if (segmentRun.Code)
                {
                    double left = currentX - _theme.InlineCodeHorizontalPadding;
                    double right = currentX + width + _theme.InlineCodeHorizontalPadding;
                    double bottom = baselineY - fontSize * 0.28;
                    double top = baselineY + fontSize * 0.92;
                    DrawFilledRectangleArtifact(context, left, bottom, right, top, _theme.InlineCodeBackgroundGray);
                }

                DrawSingleTextRun(
                    context,
                    segmentRun.Text,
                    font,
                    fontSize,
                    currentX,
                    baselineY,
                    segmentRun.Link,
                    segmentRun.Url,
                    runTag);

                if (segmentRun.Strike)
                {
                    DrawLineArtifact(context, currentX, baselineY + fontSize * 0.35, currentX + width, baselineY + fontSize * 0.35, 0.5);
                }

                currentX += width;
            }
        }
    }

    private void DrawSingleTextRun(
        PdfLayoutContext context,
        string text,
        Font font,
        double fontSize,
        double x,
        double baselineY,
        bool link,
        string? url,
        PdfTaggedElement? ownerTag)
    {
        GraphicState graphicState = new GraphicState
        {
            FillColor = link ? new Color(0.0, 0.0, 1.0) : new Color(0.0)
        };

        TextState textState = new TextState();
        Matrix matrix = new Matrix(fontSize, 0.0, 0.0, fontSize, x, baselineY);

        TextRun textRun = new TextRun(text, font, graphicState, textState, matrix);
        Text textElement = new Text();
        textElement.AddRun(textRun);

        if (ownerTag is null)
        {
            _tags.AddArtifactElement(context, textElement);
        }
        else
        {
            _tags.AddTaggedElement(context, textElement, ownerTag);
        }

        if (link && ownerTag is not null && !string.IsNullOrWhiteSpace(url))
        {
            double width = MeasureText(font, text, fontSize);
            if (width > 0.0)
            {
                Rect linkRect = new Rect(x, baselineY - fontSize * 0.25, x + width, baselineY + fontSize);
                _tags.AddLinkAnnotation(context, url, linkRect, ownerTag, text);
            }
        }
    }

    private List<List<InlineRun>> WrapInlineRuns(
        IReadOnlyList<InlineRun> sourceRuns,
        double maxWidth,
        double fontSize,
        bool forceItalic,
        PdfFontRole fontRole = PdfFontRole.Body)
    {
        List<List<InlineRun>> lines = new();
        List<InlineRun> currentLine = new();
        double currentWidth = 0.0;

        InlineRun? pendingSpace = null;

        foreach (StyledToken token in Tokenize(sourceRuns))
        {
            if (token.IsWhitespace)
            {
                pendingSpace = token.Run with { Text = " " };
                continue;
            }

            InlineRun word = token.Run;
            word = ApplyForcedStyle(word, forceItalic);

            double wordWidth = MeasureRun(word, fontSize, fontRole);

            if (wordWidth > maxWidth)
            {
                if (currentLine.Count > 0)
                {
                    lines.Add(currentLine);
                    currentLine = new List<InlineRun>();
                    currentWidth = 0.0;
                }

                foreach (InlineRun piece in SplitRunToWidth(word, maxWidth, fontSize, fontRole))
                {
                    lines.Add(new List<InlineRun> { piece });
                }

                pendingSpace = null;
                continue;
            }

            bool addSpace = pendingSpace is not null && currentLine.Count > 0;
            double spaceWidth = addSpace
                ? MeasureRun(ApplyForcedStyle(pendingSpace!, forceItalic), fontSize, fontRole)
                : 0.0;

            if (currentLine.Count > 0 && currentWidth + spaceWidth + wordWidth > maxWidth)
            {
                lines.Add(currentLine);
                currentLine = new List<InlineRun>();
                currentWidth = 0.0;
                addSpace = false;
                spaceWidth = 0.0;
            }

            if (addSpace)
            {
                InlineRun space = ApplyForcedStyle(pendingSpace!, forceItalic);
                AddRunToLine(currentLine, space);
                currentWidth += spaceWidth;
            }

            AddRunToLine(currentLine, word);
            currentWidth += wordWidth;
            pendingSpace = null;
        }

        if (currentLine.Count > 0)
        {
            lines.Add(currentLine);
        }

        if (lines.Count == 0)
        {
            lines.Add(new List<InlineRun>());
        }

        return lines;
    }

    private List<string> WrapPreformattedLine(string text, double maxWidth, double fontSize)
    {
        List<string> lines = new();

        if (text.Length == 0)
        {
            lines.Add(string.Empty);
            return lines;
        }

        int index = 0;
        while (index < text.Length)
        {
            int count = FindMaxPrefixCount(new InlineRun(text, Code: true), index, maxWidth, fontSize, PdfFontRole.Code);
            lines.Add(text.Substring(index, count));
            index += count;
        }

        return lines;
    }

    private IEnumerable<InlineRun> SplitRunToWidth(InlineRun run, double maxWidth, double fontSize, PdfFontRole fontRole)
    {
        int index = 0;

        while (index < run.Text.Length)
        {
            int count = FindMaxPrefixCount(run, index, maxWidth, fontSize, fontRole);
            yield return run with { Text = run.Text.Substring(index, count) };
            index += count;
        }
    }

    private int FindMaxPrefixCount(InlineRun run, int startIndex, double maxWidth, double fontSize, PdfFontRole fontRole)
    {
        int remaining = run.Text.Length - startIndex;

        if (remaining <= 0)
        {
            return 0;
        }

        int low = 1;
        int high = remaining;
        int best = 1;

        while (low <= high)
        {
            int mid = low + ((high - low) / 2);
            InlineRun candidate = run with { Text = run.Text.Substring(startIndex, mid) };
            double width = MeasureRun(candidate, fontSize, fontRole);

            if (width <= maxWidth || mid == 1)
            {
                best = mid;
                low = mid + 1;
            }
            else
            {
                high = mid - 1;
            }
        }

        return Math.Max(1, best);
    }

    private IEnumerable<StyledToken> Tokenize(IReadOnlyList<InlineRun> runs)
    {
        foreach (InlineRun run in runs)
        {
            string text = run.Text.Replace('\r', ' ').Replace('\n', ' ');
            int index = 0;

            while (index < text.Length)
            {
                if (char.IsWhiteSpace(text[index]))
                {
                    while (index < text.Length && char.IsWhiteSpace(text[index]))
                    {
                        index++;
                    }

                    yield return new StyledToken(run with { Text = " " }, IsWhitespace: true);
                    continue;
                }

                if (IsCjkCharacter(text[index]))
                {
                    yield return new StyledToken(run with { Text = text[index].ToString() }, IsWhitespace: false);
                    index++;
                    continue;
                }

                int start = index;
                while (index < text.Length && !char.IsWhiteSpace(text[index]) && !IsCjkCharacter(text[index]))
                {
                    index++;
                }

                yield return new StyledToken(run with { Text = text.Substring(start, index - start) }, IsWhitespace: false);
            }
        }
    }

    private double MeasureRun(InlineRun run, double fontSize, PdfFontRole fontRole = PdfFontRole.Body)
    {
        double width = 0.0;

        foreach (PdfTextSegment segment in SplitRunForFontFallback(run, fontRole))
        {
            width += MeasureText(segment.Font, segment.Run.Text, fontSize);
        }

        return width;
    }

    private IReadOnlyList<PdfTextSegment> SplitRunForFontFallback(InlineRun run, PdfFontRole fontRole)
    {
        List<PdfTextSegment> segments = new();
        StringBuilder builder = new();
        Font? currentFont = null;

        foreach (char c in run.Text)
        {
            Font font = _fonts.Resolve(run, fontRole, c);

            if (currentFont is not null && !ReferenceEquals(currentFont, font))
            {
                segments.Add(new PdfTextSegment(run with { Text = builder.ToString() }, currentFont));
                builder.Clear();
            }

            currentFont = font;
            builder.Append(c);
        }

        if (builder.Length > 0 && currentFont is not null)
        {
            segments.Add(new PdfTextSegment(run with { Text = builder.ToString() }, currentFont));
        }

        return segments;
    }

    private static bool IsCjkCharacter(char c)
    {
        return (c >= '\u2E80' && c <= '\u2EFF') ||
               (c >= '\u2F00' && c <= '\u2FDF') ||
               (c >= '\u3000' && c <= '\u303F') ||
               (c >= '\u3100' && c <= '\u312F') ||
               (c >= '\u31C0' && c <= '\u31EF') ||
               (c >= '\u3400' && c <= '\u4DBF') ||
               (c >= '\u4E00' && c <= '\u9FFF') ||
               (c >= '\uF900' && c <= '\uFAFF') ||
               (c >= '\uFF00' && c <= '\uFFEF') ||
               (c >= '\u3040' && c <= '\u30FF') ||
               (c >= '\uAC00' && c <= '\uD7AF');
    }

    private static bool IsCyrillicOrGreekCharacter(char c)
    {
        return (c >= '\u0370' && c <= '\u03FF') ||
               (c >= '\u0400' && c <= '\u052F');
    }

    private static double EstimateCyrillicOrGreekAdvance(char c, double fontSize)
    {
        // Approximate proportional-font advances. The values are used only as an
        // upper cap when APDFL reports an obviously too-large width for these
        // scripts; if APDFL returns a smaller/accurate width, that value wins.
        if (char.IsUpper(c))
        {
            return fontSize * 0.70;
        }

        return c switch
        {
            'ж' or 'Ж' or 'м' or 'М' or 'ш' or 'Ш' or 'щ' or 'Щ' or 'ю' or 'Ю' or 'ы' or 'Ы' => fontSize * 0.74,
            'і' or 'І' or 'ї' or 'Ї' or 'ј' or 'Ј' or 'ί' or 'ι' or 'Ι' => fontSize * 0.32,
            _ => fontSize * 0.60
        };
    }

    private static double MeasureText(Font font, string text, double fontSize)
    {
        if (string.IsNullOrEmpty(text))
        {
            return 0.0;
        }

        if (NeedsAdvanceCorrection(text))
        {
            return MeasureTextWithAdvanceCorrections(font, text, fontSize);
        }

        return MeasurePlainText(font, text, fontSize);
    }

    private static bool NeedsAdvanceCorrection(string text)
    {
        foreach (char c in text)
        {
            if (c == '•' || IsCjkCharacter(c) || IsCyrillicOrGreekCharacter(c))
            {
                return true;
            }
        }

        return false;
    }

    private static double MeasureTextWithAdvanceCorrections(Font font, string text, double fontSize)
    {
        double width = 0.0;
        StringBuilder chunk = new();

        foreach (char c in text)
        {
            if (c == '•' || IsCjkCharacter(c) || IsCyrillicOrGreekCharacter(c))
            {
                if (chunk.Length > 0)
                {
                    width += MeasurePlainText(font, chunk.ToString(), fontSize);
                    chunk.Clear();
                }

                if (c == '•')
                {
                    // U+2022 can be reported with a large advance in some font setups. Keep
                    // inline separators compact while preserving source spaces around them.
                    width += Math.Min(MeasurePlainText(font, c.ToString(), fontSize), fontSize * 0.35);
                }
                else if (IsCjkCharacter(c))
                {
                    // CJK ideographs are generally full-width glyphs. Some named-font
                    // combinations report conservative advances that make lines wrap far too
                    // early. Cap each CJK character at roughly one em so wrapping matches the
                    // visual glyph width while still measuring punctuation/spaces normally.
                    width += Math.Min(MeasurePlainText(font, c.ToString(), fontSize), fontSize);
                }
                else
                {
                    // In some APDFL/named-font combinations, Cyrillic/Greek glyph advances can
                    // be reported much wider than they render, which spreads words across the
                    // line. Use a conservative per-character cap only for these scripts. Spaces
                    // and punctuation remain measured normally because they are handled in the
                    // plain-text chunks outside this branch.
                    width += Math.Min(
                        MeasurePlainText(font, c.ToString(), fontSize),
                        EstimateCyrillicOrGreekAdvance(c, fontSize));
                }
            }
            else
            {
                chunk.Append(c);
            }
        }

        if (chunk.Length > 0)
        {
            width += MeasurePlainText(font, chunk.ToString(), fontSize);
        }

        return width;
    }

    private static double MeasurePlainText(Font font, string text, double fontSize)
    {
        if (string.IsNullOrEmpty(text))
        {
            return 0.0;
        }

        try
        {
            return font.MeasureTextWidth(text, fontSize);
        }
        catch
        {
            return text.Length * fontSize * 0.55;
        }
    }

    private static InlineRun ApplyForcedStyle(InlineRun run, bool forceItalic)
    {
        if (!forceItalic || run.Code)
        {
            return run;
        }

        return run with { Italic = true };
    }

    private static void AddRunToLine(List<InlineRun> line, InlineRun run)
    {
        if (string.IsNullOrEmpty(run.Text))
        {
            return;
        }

        if (line.Count > 0 && line[^1].HasSameStyle(run))
        {
            InlineRun previous = line[^1];
            line[^1] = previous with { Text = previous.Text + run.Text };
        }
        else
        {
            line.Add(run);
        }
    }

    private static void AddSpaceBefore(PdfLayoutContext context, double points)
    {
        if (!context.IsAtTop)
        {
            context.MoveDown(points);
        }
    }

    private sealed record CellLayout(List<List<InlineRun>> Lines, PdfTaggedElement Tag);

    private readonly record struct PdfTextSegment(InlineRun Run, Font Font);

    private readonly record struct StyledToken(InlineRun Run, bool IsWhitespace);
}

internal sealed class PdfLayoutContext
{
    private readonly Document _document;
    private readonly PdfTheme _theme;
    private int _lastPageIndex = -1;
    private bool _currentPageHasContent;

    public PdfLayoutContext(Document document, PdfTheme theme)
    {
        _document = document;
        _theme = theme;
        CurrentPage = CreateNextPage();
    }

    public Page CurrentPage { get; private set; }

    public double CursorY { get; private set; }

    public bool IsAtTop => Math.Abs(CursorY - TopY) < 0.01;

    private double TopY => _theme.PageHeight - _theme.MarginTop;

    public double BeginLine(double lineHeight, double fontSize)
    {
        EnsureSpace(lineHeight);
        return CursorY - fontSize;
    }

    public void EndLine(double lineHeight)
    {
        CursorY -= lineHeight;
    }

    public void MoveDown(double points)
    {
        if (points <= 0)
        {
            return;
        }

        if (!IsAtTop && CursorY - points < _theme.MarginBottom)
        {
            NewPage();
            return;
        }

        CursorY -= points;
    }

    public void EnsureSpace(double points)
    {
        if (CursorY - points < _theme.MarginBottom)
        {
            NewPage();
        }
    }

    public void MarkPageDirty()
    {
        _currentPageHasContent = true;
    }

    public void Finish()
    {
        if (_currentPageHasContent)
        {
            CurrentPage.UpdateContent();
            _currentPageHasContent = false;
        }
    }

    private void NewPage()
    {
        Finish();
        CurrentPage = CreateNextPage();
    }

    private Page CreateNextPage()
    {
        int insertionPoint = _lastPageIndex < 0
            ? Document.BeforeFirstPage
            : _lastPageIndex;

        Rect pageRect = new Rect(0.0, 0.0, _theme.PageWidth, _theme.PageHeight);
        Page page = _document.CreatePage(insertionPoint, pageRect);

        _lastPageIndex++;
        CursorY = TopY;
        _currentPageHasContent = false;

        return page;
    }
}

internal sealed class PdfTaggingContext
{
    private readonly Document _document;
    private readonly PDFDict _structTreeRoot;
    private readonly PDFArray _rootKids;
    private readonly NumberTree _parentTree;
    private readonly Dictionary<int, PageTagState> _pageStates = new();

    private int _nextStructParentKey;

    public PdfTaggingContext(Document document, string language)
    {
        _document = document;

        PDFDict markInfo = new PDFDict(_document, false);
        markInfo.Put("Marked", Bool(true));
        markInfo.Put("Suspects", Bool(false));
        _document.Root.Put("MarkInfo", markInfo);

        if (!string.IsNullOrWhiteSpace(language))
        {
            _document.Root.Put("Lang", Str(language));
        }

        _structTreeRoot = new PDFDict(_document, true);
        _structTreeRoot.Put("Type", Name("StructTreeRoot"));

        _rootKids = new PDFArray(_document, false);
        _structTreeRoot.Put("K", _rootKids);

        _parentTree = new NumberTree(_document);
        _structTreeRoot.Put("ParentTree", _parentTree.PDFDict);
        _structTreeRoot.Put("ParentTreeNextKey", Int(0));

        _document.Root.Put("StructTreeRoot", _structTreeRoot);

        DocumentElement = CreateElement("Document", parent: null);
    }

    public PdfTaggedElement DocumentElement { get; }

    public PdfTaggedElement CreateElement(string tagName, PdfTaggedElement? parent)
    {
        PDFDict element = new PDFDict(_document, true);
        PDFArray kids = new PDFArray(_document, false);

        element.Put("Type", Name("StructElem"));
        element.Put("S", Name(tagName));
        element.Put("P", parent?.Dictionary ?? _structTreeRoot);
        element.Put("K", kids);

        if (parent is null)
        {
            _rootKids.Add(element);
        }
        else
        {
            parent.Kids.Add(element);
        }

        return new PdfTaggedElement(tagName, element, kids);
    }

    public void SetAltText(PdfTaggedElement element, string altText)
    {
        if (!string.IsNullOrWhiteSpace(altText))
        {
            element.Dictionary.Put("Alt", Str(altText));
        }
    }

    public void SetActualText(PdfTaggedElement element, string actualText)
    {
        if (!string.IsNullOrWhiteSpace(actualText))
        {
            element.Dictionary.Put("ActualText", Str(actualText));
        }
    }

    public void AddTaggedElement(PdfLayoutContext layout, Element element, PdfTaggedElement owner)
    {
        Page page = layout.CurrentPage;
        PageTagState pageState = GetOrCreatePageState(page);

        int mcid = pageState.NextMcid++;

        PDFDict propertyList = new PDFDict(_document, false);
        propertyList.Put("MCID", Int(mcid));

        Container container = new Container(owner.TagName, propertyList, isInline: true)
        {
            Content = new Content(element)
        };

        page.Content.AddElement(container);

        PDFDict markedContentReference = new PDFDict(_document, false);
        markedContentReference.Put("Type", Name("MCR"));
        markedContentReference.Put("Pg", page.PDFDict);
        markedContentReference.Put("MCID", Int(mcid));

        owner.Kids.Add(markedContentReference);

        pageState.ParentArray.Add(owner.Dictionary);

        layout.MarkPageDirty();
    }

    public void AddArtifactElement(PdfLayoutContext layout, Element element)
    {
        PDFDict artifactProperties = new PDFDict(_document, false);
        artifactProperties.Put("Type", Name("Layout"));

        Container artifact = new Container("Artifact", artifactProperties, isInline: true)
        {
            Content = new Content(element)
        };

        layout.CurrentPage.Content.AddElement(artifact);
        layout.MarkPageDirty();
    }

    public void AddLinkAnnotation(PdfLayoutContext layout, string uri, Rect rect, PdfTaggedElement owner, string contents)
    {
        LinkAnnotation linkAnnotation = new LinkAnnotation(layout.CurrentPage, rect)
        {
            Action = new URIAction(uri, false),
            BorderStyleWidth = 0.0,
            Highlight = HighlightStyle.Invert,
            Contents = contents
        };

        AddObjectReference(linkAnnotation.PDFDict, owner);
    }

    public void Finish()
    {
        _structTreeRoot.Put("ParentTreeNextKey", Int(_nextStructParentKey));
    }

    private void AddObjectReference(PDFDict objectDictionary, PdfTaggedElement owner)
    {
        int structParentKey = _nextStructParentKey++;
        objectDictionary.Put("StructParent", Int(structParentKey));

        PDFDict objectReference = new PDFDict(_document, false);
        objectReference.Put("Type", Name("OBJR"));
        objectReference.Put("Obj", objectDictionary);

        owner.Kids.Add(objectReference);
        _parentTree.Put(structParentKey, owner.Dictionary);
    }

    private PageTagState GetOrCreatePageState(Page page)
    {
        int pageNumber = page.PageNumber;

        if (_pageStates.TryGetValue(pageNumber, out PageTagState? existing))
        {
            return existing;
        }

        int structParentsKey = _nextStructParentKey++;
        PDFArray parentArray = new PDFArray(_document, true);

        page.PDFDict.Put("StructParents", Int(structParentsKey));
        page.PDFDict.Put("Tabs", Name("S"));

        _parentTree.Put(structParentsKey, parentArray);

        PageTagState created = new PageTagState(structParentsKey, parentArray);
        _pageStates.Add(pageNumber, created);

        return created;
    }

    private PDFName Name(string value)
    {
        return new PDFName(value, _document, false);
    }

    private PDFInteger Int(int value)
    {
        return new PDFInteger(value, _document, false);
    }

    private PDFBoolean Bool(bool value)
    {
        return new PDFBoolean(value, _document, false);
    }

    private PDFString Str(string value)
    {
        return new PDFString(value, _document, false, storedAsHex: false);
    }

    private sealed class PageTagState
    {
        public PageTagState(int structParentsKey, PDFArray parentArray)
        {
            StructParentsKey = structParentsKey;
            ParentArray = parentArray;
        }

        public int StructParentsKey { get; }

        public int NextMcid { get; set; }

        public PDFArray ParentArray { get; }
    }
}

internal sealed record PdfTaggedElement(
    string TagName,
    PDFDict Dictionary,
    PDFArray Kids);

internal enum PdfFontRole
{
    Body,
    Heading,
    Code
}

internal sealed record PdfFontFamilyDefinition(
    string CanonicalName,
    string RegularFontName,
    string BoldFontName,
    string ItalicFontName,
    string BoldItalicFontName);

internal static class PdfFontCatalog
{
    private static readonly Dictionary<string, PdfFontFamilyDefinition> Families = CreateFamilies();

    public static IReadOnlyList<string> SupportedFamilyNames { get; } = Families
        .Values
        .Select(family => family.CanonicalName)
        .Distinct(StringComparer.OrdinalIgnoreCase)
        .OrderBy(name => name, StringComparer.OrdinalIgnoreCase)
        .ToList();

    public static string SupportedFamilyList => string.Join(", ", SupportedFamilyNames);

    public static string NormalizeFamily(string value)
    {
        if (TryNormalizeFamily(value, out string? family))
        {
            return family;
        }

        throw new CommandLineException(
            $"Unknown font family: {value}. Run --list-font-families to see recognized names. " +
            "Only fonts available to APDFL on this machine can be used successfully at runtime.");
    }

    public static bool TryNormalizeFamily(string value, [NotNullWhen(true)] out string? family)
    {
        family = null;

        if (string.IsNullOrWhiteSpace(value))
        {
            return false;
        }

        string key = NormalizeKey(value);
        if (Families.TryGetValue(key, out PdfFontFamilyDefinition? definition))
        {
            family = definition.CanonicalName;
            return true;
        }

        return false;
    }

    public static PdfFontFamilyDefinition Resolve(string family)
    {
        string key = NormalizeKey(family);
        if (Families.TryGetValue(key, out PdfFontFamilyDefinition? definition))
        {
            return definition;
        }

        throw new CommandLineException($"Unknown font family: {family}.");
    }

    private static Dictionary<string, PdfFontFamilyDefinition> CreateFamilies()
    {
        Dictionary<string, PdfFontFamilyDefinition> families = new(StringComparer.OrdinalIgnoreCase);

        AddFamily(families, "Times", "Times-Roman", "Times-Bold", "Times-Italic", "Times-BoldItalic", "timesroman", "serif");
        AddFamily(families, "Helvetica", "Helvetica", "Helvetica-Bold", "Helvetica-Oblique", "Helvetica-BoldOblique", "sans", "sansserif");
        AddFamily(families, "Courier", "Courier", "Courier-Bold", "Courier-Oblique", "Courier-BoldOblique", "mono", "monospace");

        AddFamily(families, "Andale Mono", "Andale Mono", "Andale Mono", "Andale Mono", "Andale Mono", "andalemono");
        AddFamily(families, "Arial", "Arial", "Arial Bold", "Arial Italic", "Arial Bold Italic", "arial", "arialregular", "arialbold", "arialitalic", "arialbolditalic");
        AddFamily(families, "Arial Black", "Arial Black", "Arial Black", "Arial Black", "Arial Black", "arialblack");
        AddFamily(families, "Calibri", "Calibri", "Calibri Bold", "Calibri Italic", "Calibri Bold Italic", "calibri", "calibriregular", "calibribold", "calibriitalic", "calibribolditalic");
        AddFamily(families, "Cambria", "Cambria", "Cambria Bold", "Cambria Italic", "Cambria Bold Italic", "cambria", "cambriabold", "cambriaitalic", "cambriabolditalic");
        AddFamily(families, "Candara", "Candara", "Candara Bold", "Candara Italic", "Candara Bold Italic", "candara", "candarabold", "candaraitalic", "candarabolditalic");
        AddFamily(families, "Cantarell", "Cantarell Regular", "Cantarell Bold", "Cantarell Oblique", "Cantarell Bold Oblique", "cantarell", "cantarellregular", "cantarellbold", "cantarelloblique", "cantarellboldoblique");
        AddFamily(families, "Comic Sans", "Comic Sans MS", "Comic Sans MS Bold", "Comic Sans MS", "Comic Sans MS Bold", "comicsans", "comicsansms", "comicsansbold");
        AddFamily(families, "Consolas", "Consolas", "Consolas Bold", "Consolas Italic", "Consolas Bold Italic", "consolas", "consolasbold", "consolasitalic", "consolasbolditalic");
        AddFamily(families, "Constantia", "Constantia", "Constantia Bold", "Constantia Italic", "Constantia Bold Italic", "constantia", "constantiabold", "constantiaitalic", "constantiabolditalic");
        AddFamily(families, "Corbel", "Corbel", "Corbel Bold", "Corbel Italic", "Corbel Bold Italic", "corbel", "corbelbold", "corbelitalic", "corbelbolditalic");
        AddFamily(families, "Courier New", "Courier New", "Courier New Bold", "Courier New Italic", "Courier New Bold Italic", "couriernew", "couriernewregular", "couriernewbold", "couriernewitalic", "couriernewbolditalic");
        AddFamily(families, "DejaVu Sans Mono", "DejaVu Sans Mono", "DejaVu Sans Mono Bold", "DejaVu Sans Mono Oblique", "DejaVu Sans Mono Bold Oblique", "dejavu", "dejavusansmono", "dejavusansmonobold", "dejavusansmonooblique", "dejavusansmonoboldoblique");
        AddFamily(families, "DejaVu Sans", "DejaVu Sans", "DejaVu Sans Bold", "DejaVu Sans Oblique", "DejaVu Sans Bold Oblique", "dejavusans");
        AddFamily(families, "Noto Sans", "Noto Sans", "Noto Sans Bold", "Noto Sans Italic", "Noto Sans Bold Italic", "notosans");
        AddFamily(families, "Segoe UI", "Segoe UI", "Segoe UI Bold", "Segoe UI Italic", "Segoe UI Bold Italic", "segoe", "segoeui");
        AddFamily(families, "Georgia", "Georgia", "Georgia Bold", "Georgia Italic", "Georgia Bold Italic", "georgia", "georgiabold", "georgiaitalic", "georgiabolditalic");
        AddFamily(families, "Impact", "Impact", "Impact", "Impact", "Impact", "impact");
        AddFamily(families, "Tahoma", "Tahoma", "Tahoma Bold", "Tahoma", "Tahoma Bold", "tahoma", "tahomabold");
        AddFamily(families, "Microsoft YaHei", "Microsoft YaHei", "Microsoft YaHei Bold", "Microsoft YaHei", "Microsoft YaHei Bold", "microsoftyahei", "yahei");
        AddFamily(families, "Microsoft JhengHei", "Microsoft JhengHei", "Microsoft JhengHei Bold", "Microsoft JhengHei", "Microsoft JhengHei Bold", "microsoftjhenghei", "jhenghei");
        AddFamily(families, "SimSun", "SimSun", "SimSun", "SimSun", "SimSun", "simsun");
        AddFamily(families, "Malgun Gothic", "Malgun Gothic", "Malgun Gothic Bold", "Malgun Gothic", "Malgun Gothic Bold", "malgungothic");
        AddFamily(families, "Yu Gothic", "Yu Gothic", "Yu Gothic Bold", "Yu Gothic", "Yu Gothic Bold", "yugothic");
        AddFamily(families, "Noto Sans CJK SC", "Noto Sans CJK SC", "Noto Sans CJK SC Bold", "Noto Sans CJK SC", "Noto Sans CJK SC Bold", "notosanscjksc", "noto");
        AddFamily(families, "Times New Roman", "Times New Roman", "Times New Roman Bold", "Times New Roman Italic", "Times New Roman Bold Italic", "timesnewroman", "timesnewromanbold", "timesnewromanitalic", "timesnewromanbolditalic");
        AddFamily(families, "Trebuchet", "Trebuchet MS", "Trebuchet MS Bold", "Trebuchet MS Italic", "Trebuchet MS Bold Italic", "trebuchet", "trebuchetms", "trebuchetbold", "trebuchetitalic", "trebuchetbolditalic");
        AddFamily(families, "Verdana", "Verdana", "Verdana Bold", "Verdana Italic", "Verdana Bold Italic", "verdana", "verdanabold", "verdanaitalic", "verdanabolditalic");
        AddFamily(families, "Webdings", "Webdings", "Webdings", "Webdings", "Webdings", "webdings");

        return families;
    }

    private static void AddFamily(
        Dictionary<string, PdfFontFamilyDefinition> families,
        string canonicalName,
        string regular,
        string bold,
        string italic,
        string boldItalic,
        params string[] aliases)
    {
        PdfFontFamilyDefinition definition = new(canonicalName, regular, bold, italic, boldItalic);
        families[NormalizeKey(canonicalName)] = definition;

        foreach (string alias in aliases)
        {
            families[NormalizeKey(alias)] = definition;
        }
    }

    private static string NormalizeKey(string value)
    {
        StringBuilder builder = new();
        foreach (char c in value)
        {
            if (char.IsLetterOrDigit(c))
            {
                builder.Append(char.ToLowerInvariant(c));
            }
        }

        return builder.ToString();
    }
}

internal sealed class PdfFontSet
{
    private readonly PdfFontFamilySet _body;
    private readonly PdfFontFamilySet _heading;
    private readonly List<string> _fallbackFontNames;
    private readonly Dictionary<string, Font?> _fallbackCache = new(StringComparer.OrdinalIgnoreCase);

    public PdfFontSet(string bodyFamily, string headingFamily, string codeFamily, IReadOnlyList<string> fallbackFontNames)
    {
        _body = PdfFontFamilySet.Create(bodyFamily);
        _heading = PdfFontFamilySet.Create(headingFamily);
        Code = CreateCodeFont(codeFamily);
        _fallbackFontNames = fallbackFontNames.Distinct(StringComparer.OrdinalIgnoreCase).ToList();
    }

    public Font Regular => _body.Regular;

    public Font Bold => _body.Bold;

    public Font Italic => _body.Italic;

    public Font BoldItalic => _body.BoldItalic;

    public Font Code { get; }

    public Font Resolve(InlineRun run, PdfFontRole role = PdfFontRole.Body)
    {
        return Resolve(run, role, null);
    }

    public Font Resolve(InlineRun run, PdfFontRole role, char? character)
    {
        if (character.HasValue && IsCjkCharacter(character.Value))
        {
            Font? fallback = ResolveFirstAvailableFallbackFont(GetCjkFallbackCandidates());
            if (fallback is not null)
            {
                return fallback;
            }
        }

        if (character.HasValue && IsNonLatinFallbackCharacter(character.Value))
        {
            Font? fallback = ResolveFirstAvailableFallbackFont(GetGeneralUnicodeFallbackCandidates());
            if (fallback is not null)
            {
                return fallback;
            }
        }

        if (run.Code || role == PdfFontRole.Code)
        {
            return Code;
        }

        PdfFontFamilySet family = role == PdfFontRole.Heading ? _heading : _body;

        if (run.Bold && run.Italic)
        {
            return family.BoldItalic;
        }

        if (run.Bold)
        {
            return family.Bold;
        }

        if (run.Italic)
        {
            return family.Italic;
        }

        return family.Regular;
    }

    public IReadOnlyList<string> FallbackFontNames => _fallbackFontNames;

    private Font? ResolveFirstAvailableFallbackFont(IEnumerable<string> fontNames)
    {
        foreach (string name in fontNames)
        {
            Font? font = GetOrCreateOptionalFont(name);
            if (font is not null)
            {
                return font;
            }
        }

        return null;
    }

    private IEnumerable<string> GetCjkFallbackCandidates()
    {
        foreach (string name in _fallbackFontNames.Where(IsLikelyCjkFontName))
        {
            yield return name;
        }

        foreach (string name in _fallbackFontNames)
        {
            yield return name;
        }
    }

    private IEnumerable<string> GetGeneralUnicodeFallbackCandidates()
    {
        foreach (string name in new[] { "Arial", "Times New Roman", "Calibri", "Segoe UI", "Verdana", "DejaVu Sans", "Noto Sans" })
        {
            yield return name;
        }

        foreach (string name in _fallbackFontNames)
        {
            yield return name;
        }
    }

    private static bool IsLikelyCjkFontName(string name)
    {
        string lower = name.ToLowerInvariant();
        return lower.Contains("cjk", StringComparison.Ordinal) ||
               lower.Contains("yahei", StringComparison.Ordinal) ||
               lower.Contains("simsun", StringComparison.Ordinal) ||
               lower.Contains("jhenghei", StringComparison.Ordinal) ||
               lower.Contains("malgun", StringComparison.Ordinal) ||
               lower.Contains("gothic", StringComparison.Ordinal) ||
               lower.Contains("japanese", StringComparison.Ordinal) ||
               lower.Contains("korean", StringComparison.Ordinal) ||
               lower.Contains("chinese", StringComparison.Ordinal) ||
               lower.Contains("unicode", StringComparison.Ordinal);
    }

    private Font? GetOrCreateOptionalFont(string familyOrName)
    {
        if (_fallbackCache.TryGetValue(familyOrName, out Font? cached))
        {
            return cached;
        }

        string fontName = familyOrName;
        if (PdfFontCatalog.TryNormalizeFamily(familyOrName, out string? familyName))
        {
            fontName = PdfFontCatalog.Resolve(familyName).RegularFontName;
        }

        try
        {
            Font font = new Font(fontName, FontCreateFlags.Embedded | FontCreateFlags.Subset);
            _fallbackCache[familyOrName] = font;
            return font;
        }
        catch (Exception ex) when (ex is LibraryException or ApplicationException)
        {
            try
            {
                Font font = new Font(fontName, FontCreateFlags.Subset);
                _fallbackCache[familyOrName] = font;
                return font;
            }
            catch (Exception subsetException) when (subsetException is LibraryException or ApplicationException)
            {
                _fallbackCache[familyOrName] = null;
                return null;
            }
        }
    }

    private static Font CreateCodeFont(string family)
    {
        PdfFontFamilyDefinition definition = PdfFontCatalog.Resolve(family);
        return CreateFont(definition.RegularFontName);
    }

    private static Font CreateFont(string name)
    {
        try
        {
            return new Font(name, FontCreateFlags.Embedded | FontCreateFlags.Subset);
        }
        catch (Exception embeddedException) when (embeddedException is LibraryException or ApplicationException)
        {
            try
            {
                return new Font(name, FontCreateFlags.Subset);
            }
            catch (Exception subsetException) when (subsetException is LibraryException or ApplicationException)
            {
                throw new CommandLineException(
                    $"The font \"{name}\" could not be created by APDFL. " +
                    "Use Times, Helvetica, or Courier for the most portable sample behavior, " +
                    "or install/configure the requested font so APDFL can find it.",
                    new AggregateException(embeddedException, subsetException));
            }
        }
    }

    private static bool IsCjkCharacter(char c)
    {
        return (c >= '\u2E80' && c <= '\u2EFF') ||
               (c >= '\u2F00' && c <= '\u2FDF') ||
               (c >= '\u3000' && c <= '\u303F') ||
               (c >= '\u3100' && c <= '\u312F') ||
               (c >= '\u31C0' && c <= '\u31EF') ||
               (c >= '\u3400' && c <= '\u4DBF') ||
               (c >= '\u4E00' && c <= '\u9FFF') ||
               (c >= '\uF900' && c <= '\uFAFF') ||
               (c >= '\uFF00' && c <= '\uFFEF') ||
               (c >= '\u3040' && c <= '\u30FF') ||
               (c >= '\uAC00' && c <= '\uD7AF');
    }

    private static bool IsNonLatinFallbackCharacter(char c)
    {
        return (c >= '\u0400' && c <= '\u052F') || // Cyrillic
               (c >= '\u0370' && c <= '\u03FF');   // Greek
    }

    private sealed class PdfFontFamilySet
    {
        private PdfFontFamilySet(Font regular, Font bold, Font italic, Font boldItalic)
        {
            Regular = regular;
            Bold = bold;
            Italic = italic;
            BoldItalic = boldItalic;
        }

        public Font Regular { get; }

        public Font Bold { get; }

        public Font Italic { get; }

        public Font BoldItalic { get; }

        public static PdfFontFamilySet Create(string family)
        {
            PdfFontFamilyDefinition definition = PdfFontCatalog.Resolve(family);

            return new PdfFontFamilySet(
                CreateFont(definition.RegularFontName),
                CreateFont(definition.BoldFontName),
                CreateFont(definition.ItalicFontName),
                CreateFont(definition.BoldItalicFontName));
        }
    }
}

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

    private static readonly Regex CustomPageSizeRegex = new(
        @"^(?<width>\d+(?:\.\d+)?)\s*[xX]\s*(?<height>\d+(?:\.\d+)?)$",
        RegexOptions.Compiled);

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
        if (string.Equals(pageSize, "Letter", StringComparison.OrdinalIgnoreCase))
        {
            return (612.0, 792.0);
        }

        if (string.Equals(pageSize, "Legal", StringComparison.OrdinalIgnoreCase))
        {
            return (612.0, 1008.0);
        }

        // Ledger is the native 17 x 11 inch landscape size. Use
        // --orientation portrait to force the 11 x 17 form.
        if (string.Equals(pageSize, "Ledger", StringComparison.OrdinalIgnoreCase))
        {
            return (1224.0, 792.0);
        }

        if (string.Equals(pageSize, "A3", StringComparison.OrdinalIgnoreCase))
        {
            return (842.0, 1191.0);
        }

        if (string.Equals(pageSize, "A4", StringComparison.OrdinalIgnoreCase))
        {
            return (595.0, 842.0);
        }

        if (string.Equals(pageSize, "A5", StringComparison.OrdinalIgnoreCase))
        {
            return (420.0, 595.0);
        }

        if (string.Equals(pageSize, "Tabloid", StringComparison.OrdinalIgnoreCase))
        {
            return (792.0, 1224.0);
        }

        Match match = CustomPageSizeRegex.Match(pageSize);
        if (match.Success &&
            double.TryParse(match.Groups["width"].Value, NumberStyles.Float, CultureInfo.InvariantCulture, out double width) &&
            double.TryParse(match.Groups["height"].Value, NumberStyles.Float, CultureInfo.InvariantCulture, out double height))
        {
            return (width, height);
        }

        return (612.0, 792.0);
    }

}

internal static class SelfTests
{
    public static int Run()
    {
        try
        {
            MarkdownParser parser = new();
            MarkdownDocument doc = parser.Parse("""
# Title

A paragraph with **bold**, *italic*, ***bold italic***, ~~strike~~, `inline code`, <https://example.com>, https://example.org/trail, and [reference link][docs].

[docs]: https://docs.example.com

- [x] Done
- [ ] Pending
  - Nested-looking item

1. Ordered first item
2. Ordered second item

> A blockquote with **strong text** and a [quote link](https://example.com/quote).

```PowerShell
$env:TRAIL_MAP_VERSION = "spring"
dotnet run -- sample.md output.pdf --overwrite
```

| Feature | Status |
| :--- | ---: |
| Tables | supported |
| Images | excluded |

---

Setext Heading
---------------
""");

            Require(doc.Blocks.OfType<HeadingBlock>().Any(h => h.Level == 1), "ATX heading parsed");
            Require(doc.Blocks.OfType<ParagraphBlock>().Any(p => p.Inlines.Any(r => r.Bold)), "bold parsed");
            Require(doc.Blocks.OfType<ParagraphBlock>().Any(p => p.Inlines.Any(r => r.Italic)), "italic parsed");
            Require(doc.Blocks.OfType<ParagraphBlock>().Any(p => p.Inlines.Any(r => r.Bold && r.Italic)), "bold italic parsed");
            Require(doc.Blocks.OfType<ParagraphBlock>().Any(p => p.Inlines.Any(r => r.Strike)), "strikethrough parsed");
            Require(doc.Blocks.OfType<ParagraphBlock>().Any(p => p.Inlines.Any(r => r.Code)), "inline code parsed");
            Require(doc.Blocks.OfType<ParagraphBlock>().Any(p => p.Inlines.Any(r => r.Link && string.Equals(r.Url, "https://docs.example.com", StringComparison.Ordinal))), "reference link parsed");
            Require(doc.Blocks.OfType<ParagraphBlock>().Any(p => p.Inlines.Any(r => r.Link && string.Equals(r.Url, "https://example.com", StringComparison.Ordinal))), "autolink parsed");
            Require(doc.Blocks.OfType<ParagraphBlock>().Any(p => p.Inlines.Any(r => r.Link && string.Equals(r.Url, "https://example.org/trail", StringComparison.Ordinal))), "bare URL parsed");
            MarkdownDocument anchorTextDoc = parser.Parse("[Community Guide](https://example.com/guide) and <https://example.com>");
            InlineRun markdownLink = anchorTextDoc.Blocks.OfType<ParagraphBlock>().First().Inlines.First(r => r.Link);
            Require(string.Equals(markdownLink.Text, "Community Guide", StringComparison.Ordinal), "markdown link displays anchor text only");
            Require(string.Equals(markdownLink.Url, "https://example.com/guide", StringComparison.Ordinal), "markdown link keeps URL for annotation");
            Require(doc.Blocks.OfType<ListBlock>().Any(l => l.Items.Any(i => i.TaskChecked == true)), "task list parsed");
            Require(doc.Blocks.OfType<ListBlock>().Any(l => l.Ordered && l.Items.Count == 2), "ordered list parsed");
            Require(doc.Blocks.OfType<BlockQuoteBlock>().Any(q => q.Inlines.Any(r => r.Link)), "blockquote parsed");
            Require(doc.Blocks.OfType<CodeBlock>().Any(c => string.Equals(c.Language, "PowerShell", StringComparison.Ordinal) && c.Lines.Count == 2), "fenced code block parsed");
            Require(doc.Blocks.OfType<TableBlock>().Any(t => t.HeaderCells.Count == 2 && t.Rows.Count == 2 && t.Alignments[0] == TableColumnAlignment.Left && t.Alignments[1] == TableColumnAlignment.Right), "table parsed");
            Require(doc.Blocks.OfType<HorizontalRuleBlock>().Any(), "horizontal rule parsed");
            Require(doc.Blocks.OfType<HeadingBlock>().Any(h => h.Level == 2 && h.Inlines.Any(r => r.Text.Contains("Setext", StringComparison.Ordinal))), "setext heading parsed");

            MarkdownDocument identifierDoc = parser.Parse("Use TRAIL_MAP_VERSION and WEATHER_ALERT_LEVEL without emphasis, but _italic_ and **bold** should still work.");
            ParagraphBlock identifierParagraph = identifierDoc.Blocks.OfType<ParagraphBlock>().First();
            Require(identifierParagraph.Inlines.Any(r => r.Text.Contains("TRAIL_MAP_VERSION", StringComparison.Ordinal) && !r.Italic), "snake-case style identifier preserved");
            Require(identifierParagraph.Inlines.Any(r => string.Equals(r.Text, "italic", StringComparison.Ordinal) && r.Italic), "underscore emphasis still works when delimiters are valid");
            Require(identifierParagraph.Inlines.Any(r => string.Equals(r.Text, "bold", StringComparison.Ordinal) && r.Bold), "asterisk bold still works");

            MarkdownDocument htmlDoc = parser.Parse("""
<div align="center">

![Logo](https://example.com/logo.png)

<strong>Centered</strong> <em>seasonal</em> <code>display_mode</code> <del>draft</del> <a href="https://example.com">Example</a>

</div>

<br/>

<p>HTML &amp; Markdown can mix.</p>

<aside>This unsupported wrapper is stripped by default.</aside>
""");

            Require(htmlDoc.Blocks.OfType<ParagraphBlock>().Any(p => p.Inlines.Any(r => r.Text.Contains("Image omitted", StringComparison.Ordinal))), "html image omitted");
            Require(htmlDoc.Blocks.OfType<ParagraphBlock>().Any(p => p.Inlines.Any(r => r.Bold && r.Text.Contains("Centered", StringComparison.Ordinal))), "html strong converted");
            Require(htmlDoc.Blocks.OfType<ParagraphBlock>().Any(p => p.Inlines.Any(r => r.Italic && r.Text.Contains("seasonal", StringComparison.Ordinal))), "html emphasis converted");
            Require(htmlDoc.Blocks.OfType<ParagraphBlock>().Any(p => p.Inlines.Any(r => r.Code && r.Text.Contains("display_mode", StringComparison.Ordinal))), "html code converted");
            Require(htmlDoc.Blocks.OfType<ParagraphBlock>().Any(p => p.Inlines.Any(r => r.Strike && r.Text.Contains("draft", StringComparison.Ordinal))), "html deleted text converted");
            Require(htmlDoc.Blocks.OfType<ParagraphBlock>().Any(p => p.Inlines.Any(r => r.Link && string.Equals(r.Url, "https://example.com", StringComparison.Ordinal))), "html anchor converted");
            Require(htmlDoc.Blocks.OfType<ParagraphBlock>().Any(p => p.Inlines.Any(r => r.Text.Contains("HTML & Markdown", StringComparison.Ordinal))), "html entity decoded");
            Require(!htmlDoc.Blocks.OfType<ParagraphBlock>().Any(p => p.Inlines.Any(r => r.Text.Contains("<aside>", StringComparison.Ordinal))), "unsupported html stripped by default");

            MarkdownDocument rawHtmlDoc = parser.Parse("""
<setting name="DISPLAY_INTERVAL_SECONDS" type="integer">

Controls a fictional lobby display.

</setting>
""", includeUnrenderedHtml: true);
            Require(rawHtmlDoc.Blocks.OfType<ParagraphBlock>().Any(p => p.Inlines.Any(r => r.Text.Contains("<setting name=", StringComparison.Ordinal))), "raw html opening tag preserved");
            Require(rawHtmlDoc.Blocks.OfType<ParagraphBlock>().Any(p => p.Inlines.Any(r => r.Text.Contains("</setting>", StringComparison.Ordinal))), "raw html closing tag preserved");

            ConversionOptions parsedOptions = ConversionOptions.Parse(new[]
            {
                "input.md",
                "output.pdf",
                "--page-size",
                "A4",
                "--orientation",
                "landscape",
                "--font-family",
                "Helvetica",
                "--heading-font-family",
                "Times",
                "--code-font-family",
                "Courier",
                "--cjk-font-family",
                "Microsoft YaHei",
                "--fallback-font-family",
                "Noto Sans CJK SC",
                "--fallback-fonts",
                "Arial, DejaVu Sans",
                "--margin",
                "54",
                "--include-unrendered-html"
            });

            Require(string.Equals(parsedOptions.PageSize, "A4", StringComparison.Ordinal), "page size option parsed");
            Require(string.Equals(parsedOptions.Orientation, "Landscape", StringComparison.Ordinal), "orientation option parsed");
            Require(string.Equals(parsedOptions.FontFamily, "Helvetica", StringComparison.Ordinal), "body font option parsed");
            Require(string.Equals(parsedOptions.HeadingFontFamily, "Times", StringComparison.Ordinal), "heading font option parsed");
            Require(string.Equals(parsedOptions.CodeFontFamily, "Courier", StringComparison.Ordinal), "code font option parsed");
            Require(string.Equals(parsedOptions.CjkFontFamily, "Microsoft YaHei", StringComparison.Ordinal), "CJK font option parsed");
            Require(parsedOptions.FallbackFontNames.Contains("Noto Sans CJK SC", StringComparer.OrdinalIgnoreCase), "fallback font option parsed");
            Require(parsedOptions.FallbackFontNames.Contains("DejaVu Sans", StringComparer.OrdinalIgnoreCase), "fallback font list parsed");
            Require(Math.Abs(parsedOptions.MarginPoints - 54.0) < 0.001, "margin option parsed");
            Require(parsedOptions.IncludeUnrenderedHtml, "include raw html option parsed");

            PdfTheme landscapeTheme = PdfTheme.Create(parsedOptions.PageSize, parsedOptions.Orientation, parsedOptions.MarginPoints);
            Require(landscapeTheme.PageWidth > landscapeTheme.PageHeight, "landscape orientation resolved");

            ConversionOptions customPageOptions = ConversionOptions.Parse(new[] { "input.md", "output.pdf", "--page-size", "500x700" });
            PdfTheme customTheme = PdfTheme.Create(customPageOptions.PageSize, customPageOptions.Orientation, customPageOptions.MarginPoints);
            Require(Math.Abs(customTheme.PageWidth - 500.0) < 0.001 && Math.Abs(customTheme.PageHeight - 700.0) < 0.001, "custom page size resolved");

            ConversionOptions ledgerOptions = ConversionOptions.Parse(new[] { "input.md", "output.pdf", "--page-size", "Ledger" });
            PdfTheme ledgerTheme = PdfTheme.Create(ledgerOptions.PageSize, ledgerOptions.Orientation, ledgerOptions.MarginPoints);
            Require(ledgerTheme.PageWidth > ledgerTheme.PageHeight, "ledger auto orientation resolved as native landscape");

            ConversionOptions a5Options = ConversionOptions.Parse(new[] { "input.md", "output.pdf", "--page-size", "A5", "--orientation", "portrait" });
            PdfTheme a5Theme = PdfTheme.Create(a5Options.PageSize, a5Options.Orientation, a5Options.MarginPoints);
            Require(a5Theme.PageWidth < a5Theme.PageHeight, "A5 page size resolved");

            ConversionOptions fontAliasOptions = ConversionOptions.Parse(new[] { "input.md", "output.pdf", "--font-family", "timesnewroman", "--code-font-family", "consolas", "--recursive" });
            Require(string.Equals(fontAliasOptions.FontFamily, "Times New Roman", StringComparison.Ordinal), "cloud-style font alias parsed");
            Require(string.Equals(fontAliasOptions.CodeFontFamily, "Consolas", StringComparison.Ordinal), "code font alias parsed");
            Require(fontAliasOptions.Recursive, "recursive option parsed");

            Console.WriteLine("Self-tests passed.");
            return 0;
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine("Self-tests failed:");
            Console.Error.WriteLine(ex.Message);
            return 1;
        }
    }

    private static void Require(bool condition, string name)
    {
        if (!condition)
        {
            throw new InvalidOperationException($"Expected test condition failed: {name}");
        }
    }
}

internal sealed record MarkdownDocument(IReadOnlyList<MarkdownBlock> Blocks);

internal abstract record MarkdownBlock;

internal sealed record HeadingBlock(int Level, IReadOnlyList<InlineRun> Inlines) : MarkdownBlock;

internal sealed record ParagraphBlock(IReadOnlyList<InlineRun> Inlines) : MarkdownBlock;

internal sealed record ListBlock(bool Ordered, IReadOnlyList<ListItemBlock> Items) : MarkdownBlock;

internal sealed record ListItemBlock(IReadOnlyList<InlineRun> Inlines, int Level, int Number, bool? TaskChecked);

internal sealed record CodeBlock(string Language, IReadOnlyList<string> Lines) : MarkdownBlock;

internal sealed record BlockQuoteBlock(IReadOnlyList<InlineRun> Inlines) : MarkdownBlock;

internal sealed record HorizontalRuleBlock : MarkdownBlock;

internal sealed record TableBlock(
    IReadOnlyList<TableCell> HeaderCells,
    IReadOnlyList<TableRow> Rows,
    IReadOnlyList<TableColumnAlignment> Alignments) : MarkdownBlock;

internal sealed record TableRow(IReadOnlyList<TableCell> Cells);

internal sealed record TableCell(IReadOnlyList<InlineRun> Inlines);

internal enum TableColumnAlignment
{
    Left,
    Center,
    Right
}

internal sealed record InlineRun(
    string Text,
    bool Bold = false,
    bool Italic = false,
    bool Code = false,
    bool Strike = false,
    bool Link = false,
    string? Url = null)
{
    public bool HasSameStyle(InlineRun other)
    {
        return Bold == other.Bold &&
               Italic == other.Italic &&
               Code == other.Code &&
               Strike == other.Strike &&
               Link == other.Link &&
               Url == other.Url;
    }
}

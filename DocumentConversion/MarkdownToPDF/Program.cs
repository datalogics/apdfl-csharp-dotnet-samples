using System.Diagnostics.CodeAnalysis;
using System.Globalization;
using System.Net;
using System.Text;
using System.Text.RegularExpressions;
using Datalogics.PDFL;

namespace MarkdownToPDF;

internal static class Program
{
    public static int Main(string[] args)
    {
        try
        {
            if (args.Any(IsHelpArgument))
            {
                PrintUsage();
                return 0;
            }

            bool runSelfTest = args.Length == 1 &&
                string.Equals(args[0], "--self-test", StringComparison.OrdinalIgnoreCase);
            bool listFontFamilies = args.Length == 1 &&
                string.Equals(args[0], "--list-font-families", StringComparison.OrdinalIgnoreCase);

            string? licenseKey = Environment.GetEnvironmentVariable("APDFL_LICENSE_KEY");
            if (!string.IsNullOrWhiteSpace(licenseKey))
            {
                Library.LicenseKey = licenseKey;
            }

            using (Library library = new Library())
            {
                _ = library;

                if (runSelfTest)
                {
                    return SelfTests.Run();
                }

                if (listFontFamilies)
                {
                    PrintSupportedFontFamilies();
                    return 0;
                }

                ConversionOptions options = ConversionOptions.Parse(args);
                bool inputIsDirectory = Directory.Exists(options.InputPath);
                bool inputIsFile = File.Exists(options.InputPath);

                if (!inputIsDirectory && !inputIsFile)
                {
                    Console.Error.WriteLine($"Input Markdown file or directory was not found: {options.InputPath}");
                    return 3;
                }

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

        string markdown;
        try
        {
            markdown = File.ReadAllText(
                fullInputPath,
                new UTF8Encoding(encoderShouldEmitUTF8Identifier: false, throwOnInvalidBytes: true));
        }
        catch (DecoderFallbackException ex)
        {
            throw new CommandLineException(
                $"Input file is not valid UTF-8 text: {fullInputPath}. " +
                "Save the Markdown file as UTF-8 and try again.",
                ex);
        }

        MarkdownParser parser = new();
        MarkdownDocument markdownDocument = parser.Parse(markdown, options.IncludeUnrenderedHtml);
        PdfTheme theme = PdfTheme.Create(options.PageSize, options.Orientation, options.MarginPoints);

        using Document pdfDocument = new Document
        {
            Title = options.Title ?? System.IO.Path.GetFileNameWithoutExtension(fullInputPath),
            Producer = "MarkdownToPDF tagged sample using Datalogics APDFL"
        };

        using PdfMarkdownRenderer renderer = new(pdfDocument, theme, options);
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
            Console.WriteLine($"Body font family: {PdfFontCatalog.GetDisplayFamilyName(options.FontFamily)}");
            Console.WriteLine($"Heading font family: {PdfFontCatalog.GetDisplayFamilyName(options.HeadingFontFamily)}");
            Console.WriteLine($"Code font family: {PdfFontCatalog.GetDisplayFamilyName(options.CodeFontFamily)}");
            Console.WriteLine($"CJK font family: {(options.CjkFontFamily is null ? "auto" : PdfFontCatalog.GetDisplayFamilyName(options.CjkFontFamily))}");
            Console.WriteLine($"Fallback fonts: {(options.FallbackFontNames.Count == 0 ? "none" : string.Join(", ", options.FallbackFontNames.Select(PdfFontCatalog.GetDisplayFamilyName)))}");
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
        Console.WriteLine("  MarkdownToPDF.exe [options]");
        Console.WriteLine("  MarkdownToPDF.exe input.md output.pdf [options]");
        Console.WriteLine("  MarkdownToPDF.exe input-folder output-folder [options]");
        Console.WriteLine();
        Console.WriteLine("Input/output:");
        Console.WriteLine("  no input/output arguments       Convert sample.md to output.pdf.");
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
        Console.WriteLine("  --list-font-families            Print font families available to APDFL on this machine.");
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
        Console.WriteLine("  MarkdownToPDF.exe sample.md output.pdf --overwrite");
        Console.WriteLine("  MarkdownToPDF.exe samples sample-pdfs --recursive --overwrite --page-size A4 --orientation landscape --font-family Helvetica");
        Console.WriteLine("  MarkdownToPDF.exe multilingual.md multilingual.pdf --font-family Arial --cjk-font-family \"Microsoft YaHei\"");
        Console.WriteLine("  MarkdownToPDF.exe config.md config.pdf --include-unrendered-html --overwrite");
        Console.WriteLine();
        Console.WriteLine("License:");
        Console.WriteLine("  Set APDFL_LICENSE_KEY to provide a Datalogics APDFL activation key before Library initialization.");
    }

    private static void PrintSupportedFontFamilies()
    {
        Console.WriteLine("Core PDF fonts:");
        foreach (string name in PdfFontCatalog.CoreFamilyNames)
        {
            Console.WriteLine($"  {PdfFontCatalog.GetDisplayFamilyName(name)}");
        }

        List<string> additionalFamilies = PdfFontCatalog.SupportedFamilyNames
            .Except(PdfFontCatalog.CoreFamilyNames, StringComparer.OrdinalIgnoreCase)
            .ToList();

        if (additionalFamilies.Count > 0)
        {
            Console.WriteLine();
            Console.WriteLine($"Other fonts available to APDFL on this machine ({additionalFamilies.Count}):");
            foreach (string name in additionalFamilies)
            {
                Console.WriteLine($"  {PdfFontCatalog.GetDisplayFamilyName(name)}");
            }
        }

        Console.WriteLine();
        Console.WriteLine("Pass any of these with --font-family / --heading-font-family / --code-font-family.");
        Console.WriteLine("Times, Helvetica, and Courier remain the most portable choices across machines.");
    }
}

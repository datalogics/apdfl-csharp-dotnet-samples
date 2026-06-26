using Datalogics.PDFL;

namespace CreateInvoiceFromStructuredData;

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

            if (args.Any(CommandLineOptions.IsHelpArgument))
            {
                CommandLineOptions.PrintUsage();
                return 0;
            }

            CommandLineOptions options = CommandLineOptions.Parse(args);

            string? licenseKey = Environment.GetEnvironmentVariable("APDFL_LICENSE_KEY");
            if (!string.IsNullOrWhiteSpace(licenseKey))
            {
                Library.LicenseKey = licenseKey;
            }

            using Library library = new();
            _ = library;

            InvoiceInput invoice = InvoiceDataLoader.Load(options.InvoiceJsonPath);
            IReadOnlyList<InvoiceLineItem> lineItems = CsvLineItemReader.Load(options.LineItemsCsvPath);
            InvoiceStyle style = InvoiceStyleLoader.Load(options.StyleJsonPath);

            string baseDirectory = System.IO.Path.GetDirectoryName(System.IO.Path.GetFullPath(options.InvoiceJsonPath)) ?? Directory.GetCurrentDirectory();
            string outputPath = System.IO.Path.GetFullPath(options.OutputPdfPath);
            string? outputDirectory = System.IO.Path.GetDirectoryName(outputPath);

            if (!string.IsNullOrWhiteSpace(outputDirectory))
            {
                Directory.CreateDirectory(outputDirectory);
            }

            InvoiceDocument document = new(invoice, lineItems);
            PdfInvoiceRenderer renderer = new(style);
            renderer.Render(document, baseDirectory, outputPath);

            Console.WriteLine($"Created tagged invoice PDF: {outputPath}");
            return 0;
        }
        catch (CommandLineException ex)
        {
            Console.Error.WriteLine(ex.Message);
            Console.Error.WriteLine();
            CommandLineOptions.PrintUsage();
            return 2;
        }
        catch (Exception ex) when (ex is LibraryException or ApplicationException)
        {
            Console.Error.WriteLine("APDFL error:");
            Console.Error.WriteLine(ex.Message);
            Console.Error.WriteLine("Confirm that APDFL is licensed and that APDFL_LICENSE_KEY is set if your environment requires it.");
            return 10;
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
}

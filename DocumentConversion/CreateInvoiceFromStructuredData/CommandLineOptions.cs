namespace CreateInvoiceFromStructuredData;

internal sealed class CommandLineOptions
{
    public string InvoiceJsonPath { get; init; } = Path.Combine("data", "metadata.json");

    public string LineItemsCsvPath { get; init; } = Path.Combine("data", "line-items.csv");

    public string StyleJsonPath { get; init; } = Path.Combine("data", "style.json");

    public string OutputPdfPath { get; init; } = "CreateInvoiceFromStructuredData-out.pdf";

    public static bool IsHelpArgument(string arg)
    {
        return string.Equals(arg, "--help", StringComparison.OrdinalIgnoreCase) ||
               string.Equals(arg, "-h", StringComparison.OrdinalIgnoreCase) ||
               string.Equals(arg, "/?", StringComparison.OrdinalIgnoreCase);
    }

    public static CommandLineOptions Parse(string[] args)
    {
        string invoiceJsonPath = Path.Combine("data", "metadata.json");
        string lineItemsCsvPath = Path.Combine("data", "line-items.csv");
        string styleJsonPath = Path.Combine("data", "style.json");
        string outputPdfPath = "CreateInvoiceFromStructuredData-out.pdf";

        for (int i = 0; i < args.Length; i++)
        {
            string arg = args[i];

            switch (arg.ToLowerInvariant())
            {
                case "--invoice":
                case "--metadata":
                    invoiceJsonPath = RequireValue(args, ref i, arg);
                    break;

                case "--line-items":
                    lineItemsCsvPath = RequireValue(args, ref i, arg);
                    break;

                case "--style":
                    styleJsonPath = RequireValue(args, ref i, arg);
                    break;

                case "--output":
                    outputPdfPath = RequireValue(args, ref i, arg);
                    break;

                default:
                    throw new CommandLineException($"Unknown option: {arg}");
            }
        }

        return new CommandLineOptions
        {
            InvoiceJsonPath = invoiceJsonPath,
            LineItemsCsvPath = lineItemsCsvPath,
            StyleJsonPath = styleJsonPath,
            OutputPdfPath = outputPdfPath
        };
    }

    public static void PrintUsage()
    {
        Console.WriteLine("Usage:");
        Console.WriteLine("  CreateInvoiceFromStructuredData.exe [options]");
        Console.WriteLine();
        Console.WriteLine("With no options, the sample reads:");
        Console.WriteLine("  data/metadata.json");
        Console.WriteLine("  data/line-items.csv");
        Console.WriteLine("  data/style.json");
        Console.WriteLine("and writes:");
        Console.WriteLine("  CreateInvoiceFromStructuredData-out.pdf");
        Console.WriteLine();
        Console.WriteLine("Options:");
        Console.WriteLine("  --metadata <path>      JSON file with seller, customer, and invoice metadata.");
        Console.WriteLine("  --invoice <path>       Alias for --metadata.");
        Console.WriteLine("  --line-items <path>    CSV file containing invoice line items.");
        Console.WriteLine("  --style <path>         JSON style configuration for fonts, colors, and sizing.");
        Console.WriteLine("  --output <path>        Output PDF path.");
        Console.WriteLine("  --self-test            Validate parsing and totals without creating a PDF.");
        Console.WriteLine("  --help, -h, /?         Show this help.");
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
}

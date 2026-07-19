namespace CreateInvoiceFromStructuredData;

internal static class SelfTests
{
    public static int Run()
    {
        try
        {
            string[] csv = CsvLineItemReader.ParseRow("\"FIELD-102\",\"Quarterly garden planning, includes layout notes\",2,125.50");
            Require(csv.Length == 4, "quoted csv row parsed");
            Require(string.Equals(csv[1], "Quarterly garden planning, includes layout notes", StringComparison.Ordinal), "quoted comma preserved");

            CommandLineOptions defaults = CommandLineOptions.Parse(Array.Empty<string>());
            Require(defaults.InvoiceJsonPath.EndsWith("metadata.json", StringComparison.Ordinal), "default metadata path parsed");
            Require(defaults.LineItemsCsvPath.EndsWith("line-items.csv", StringComparison.Ordinal), "default line items path parsed");
            Require(defaults.StyleJsonPath.EndsWith("style.json", StringComparison.Ordinal), "default style path parsed");
            Require(defaults.OutputPdfPath.EndsWith(".pdf", StringComparison.Ordinal), "default output path parsed");

            InvoiceInput invoice = InvoiceDataLoader.Load(Path.Combine("data", "metadata.json"));
            IReadOnlyList<InvoiceLineItem> lineItems = CsvLineItemReader.Load(Path.Combine("data", "line-items.csv"));
            InvoiceStyle style = InvoiceStyleLoader.Load(Path.Combine("data", "style.json"));
            InvoiceDocument document = new(invoice, lineItems);
            string logoPath = Path.GetFullPath(Path.Combine("data", invoice.LogoPath));

            Require(!string.IsNullOrWhiteSpace(invoice.LogoPath), "logo path loaded");
            Require(invoice.ApplyRestrictionPassword, "restriction password enabled");
            Require(!string.IsNullOrWhiteSpace(invoice.RestrictionPassword), "restriction password loaded");
            Require(File.Exists(logoPath), "logo path resolves to a local image");
            Require(lineItems.Count >= 20, "multipage-oriented line item set loaded");
            Require(document.Subtotal > 0, "subtotal calculated");
            Require(document.Tax > 0, "tax calculated");
            Require(document.Total == document.Subtotal + document.Tax, "total calculated");
            Require(style.PageWidth > 0 && style.PageHeight > 0, "style page size loaded");

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

using System.Globalization;
using Datalogics.PDFL;

namespace CreateInvoiceFromStructuredData;

internal sealed class PdfInvoiceRenderer
{
    private readonly InvoiceStyle _style;
    private readonly PdfColor _primary;
    private readonly PdfColor _accent;
    private readonly PdfColor _text;
    private readonly PdfColor _muted;
    private readonly PdfColor _border;
    private readonly Font _bodyFont;
    private readonly Font _boldFont;

    private Document? _document;
    private PdfTaggingContext? _tags;
    private PdfLayoutContext? _layout;
    private PdfTaggedElement? _documentTag;

    public PdfInvoiceRenderer(InvoiceStyle style)
    {
        _style = style;
        _primary = PdfColor.FromHex(style.PrimaryColor);
        _accent = PdfColor.FromHex(style.AccentColor);
        _text = PdfColor.FromHex(style.TextColor);
        _muted = PdfColor.FromHex(style.MutedTextColor);
        _border = PdfColor.FromHex(style.BorderColor);
        _bodyFont = CreateFont(style.BodyFont);
        _boldFont = CreateFont(style.BoldFont);
    }

    public void Render(InvoiceDocument invoiceDocument, string baseDirectory, string outputPath)
    {
        using Document document = new()
        {
            Title = $"Invoice {invoiceDocument.Invoice.InvoiceNumber}",
            Producer = "CreateInvoiceFromStructuredData sample using Datalogics APDFL"
        };

        _document = document;
        _tags = new PdfTaggingContext(document, "en-US");
        _documentTag = _tags.DocumentElement;

        Rect pageRect = new(0, 0, _style.PageWidth, _style.PageHeight);
        _layout = new PdfLayoutContext(document, pageRect, _style.PageHeight - _style.Margin);

        RenderHeader(invoiceDocument, baseDirectory);
        RenderParties(invoiceDocument.Invoice);
        RenderLineItems(invoiceDocument);
        RenderTotals(invoiceDocument);
        RenderNotes(invoiceDocument.Invoice);

        _layout.CurrentPage.UpdateContent();
        _tags.Finish();
        document.EmbedFonts(EmbedFlags.None);
        document.Save(SaveFlags.Full, outputPath);
    }

    private void RenderHeader(InvoiceDocument invoiceDocument, string baseDirectory)
    {
        InvoiceInput invoice = invoiceDocument.Invoice;
        PdfTaggedElement headerTag = CreateTag("Sect", "Document header");

        string logoPath = System.IO.Path.Combine(baseDirectory, invoice.LogoPath);
        if (File.Exists(logoPath))
        {
            DrawLogo(logoPath, _style.Margin, _style.PageHeight - _style.Margin - _style.LogoMaxHeight, headerTag);
        }

        DrawText("INVOICE", _style.PageWidth - _style.Margin - 140, _style.PageHeight - _style.Margin - 10, _style.HeadingFontSize, _boldFont, _primary, headerTag);
        DrawText($"Invoice {invoice.InvoiceNumber}", _style.PageWidth - _style.Margin - 140, _style.PageHeight - _style.Margin - 34, 11, _bodyFont, _text, headerTag);
        DrawText($"Issued {invoice.IssueDate}", _style.PageWidth - _style.Margin - 140, _style.PageHeight - _style.Margin - 50, 9, _bodyFont, _muted, headerTag);
        DrawText($"Due {invoice.DueDate}", _style.PageWidth - _style.Margin - 140, _style.PageHeight - _style.Margin - 64, 9, _bodyFont, _muted, headerTag);

        DrawRule(_style.Margin, _style.PageHeight - _style.Margin - 82, _style.PageWidth - _style.Margin, _style.PageHeight - _style.Margin - 82);
        Layout.Y = _style.PageHeight - _style.Margin - 112;
    }

    private void RenderParties(InvoiceInput invoice)
    {
        PdfTaggedElement sectionTag = CreateTag("Sect", "Seller and customer information");

        double columnWidth = (_style.PageWidth - (_style.Margin * 2) - 24) / 2;
        double startY = Layout.Y;

        DrawPartyBlock("From", invoice.Seller, _style.Margin, startY, columnWidth, sectionTag);
        DrawPartyBlock("Bill To", invoice.Customer, _style.Margin + columnWidth + 24, startY, columnWidth, sectionTag);

        Layout.Y = startY - 118;
    }

    private void DrawPartyBlock(string label, CompanyInfo company, double x, double y, double width, PdfTaggedElement parentTag)
    {
        PdfTaggedElement blockTag = Tags.CreateElement("P", parentTag);
        DrawText(label, x, y, 9, _boldFont, _primary, blockTag);
        DrawText(company.Name, x, y - 18, 12, _boldFont, _text, blockTag);
        DrawText($"Account {company.AccountNumber}", x, y - 34, 9, _bodyFont, _muted, blockTag);

        double currentY = y - 50;
        foreach (string line in company.AddressLines().Where(line => !string.IsNullOrWhiteSpace(line)))
        {
            DrawText(line, x, currentY, 9, _bodyFont, _text, blockTag);
            currentY -= 13;
        }

        DrawText(company.Email, x, currentY - 2, 9, _bodyFont, _text, blockTag);
        DrawText(company.Phone, x, currentY - 15, 9, _bodyFont, _text, blockTag);

        DrawBox(x - 8, y + 10, width + 16, 108, _accent, _border);
    }

    private void RenderLineItems(InvoiceDocument invoiceDocument)
    {
        EnsureRoom(120);
        PdfTaggedElement tableTag = Tags.CreateElement("Table", DocumentTag);
        double[] widths = { 72, 238, 54, 72, 72 };
        string[] headers = { "Item", "Description", "Qty", "Unit", "Amount" };

        DrawTableHeader(tableTag, headers, widths);

        foreach (InvoiceLineItem item in invoiceDocument.LineItems)
        {
            double rowHeight = Math.Max(26, EstimateWrappedLineCount(item.Description, widths[1] - 12, _style.BodyFontSize) * 12 + 12);
            EnsureRoom(rowHeight + 44);

            if (Layout.Y > _style.PageHeight - _style.Margin - 120)
            {
                DrawTableHeader(tableTag, headers, widths);
            }

            DrawLineItemRow(tableTag, item, widths, rowHeight, invoiceDocument.Invoice.Currency);
        }
    }

    private void DrawTableHeader(PdfTaggedElement tableTag, string[] headers, double[] widths)
    {
        PdfTaggedElement rowTag = Tags.CreateElement("TR", tableTag);
        double x = _style.Margin;
        double y = Layout.Y;
        double height = 22;

        DrawBox(x, y + 5, widths.Sum(), height, _primary, _primary);

        for (int i = 0; i < headers.Length; i++)
        {
            PdfTaggedElement cellTag = Tags.CreateElement("TH", rowTag);
            DrawText(headers[i], x + 6, y - 9, _style.TableHeaderFontSize, _boldFont, new PdfColor(1, 1, 1), cellTag);
            x += widths[i];
        }

        Layout.Y -= height;
    }

    private void DrawLineItemRow(PdfTaggedElement tableTag, InvoiceLineItem item, double[] widths, double rowHeight, string currency)
    {
        PdfTaggedElement rowTag = Tags.CreateElement("TR", tableTag);
        double x = _style.Margin;
        double y = Layout.Y;

        DrawBox(x, y + 5, widths.Sum(), rowHeight, new PdfColor(1, 1, 1), _border);

        DrawCell(rowTag, item.ItemCode, x, y - 10, widths[0] - 12, rowHeight, _bodyFont, alignRight: false);
        x += widths[0];

        DrawCell(rowTag, item.Description, x, y - 10, widths[1] - 12, rowHeight, _bodyFont, alignRight: false);
        x += widths[1];

        DrawCell(rowTag, FormatNumber(item.Quantity), x, y - 10, widths[2] - 12, rowHeight, _bodyFont, alignRight: true);
        x += widths[2];

        DrawCell(rowTag, FormatCurrency(item.UnitPrice, currency), x, y - 10, widths[3] - 12, rowHeight, _bodyFont, alignRight: true);
        x += widths[3];

        DrawCell(rowTag, FormatCurrency(item.Amount, currency), x, y - 10, widths[4] - 12, rowHeight, _boldFont, alignRight: true);

        Layout.Y -= rowHeight;
    }

    private void DrawCell(PdfTaggedElement rowTag, string text, double x, double y, double width, double rowHeight, Font font, bool alignRight)
    {
        PdfTaggedElement cellTag = Tags.CreateElement("TD", rowTag);
        IReadOnlyList<string> lines = WrapText(text, width, font, _style.BodyFontSize);
        double currentY = y;

        foreach (string line in lines)
        {
            double lineX = alignRight ? x + width - font.MeasureTextWidth(line, _style.BodyFontSize) + 6 : x + 6;
            DrawText(line, lineX, currentY, _style.BodyFontSize, font, _text, cellTag);
            currentY -= 12;
        }
    }

    private void RenderTotals(InvoiceDocument invoiceDocument)
    {
        EnsureRoom(110);
        PdfTaggedElement totalsTag = CreateTag("Sect", "Invoice totals");
        double labelX = _style.PageWidth - _style.Margin - 160;
        double valueX = _style.PageWidth - _style.Margin - 72;
        double y = Layout.Y - 8;

        DrawTotalLine("Subtotal", FormatCurrency(invoiceDocument.Subtotal, invoiceDocument.Invoice.Currency), labelX, valueX, y, totalsTag, bold: false);
        y -= 18;
        DrawTotalLine($"Tax ({invoiceDocument.Invoice.TaxRate:P0})", FormatCurrency(invoiceDocument.Tax, invoiceDocument.Invoice.Currency), labelX, valueX, y, totalsTag, bold: false);
        y -= 22;
        DrawRule(labelX, y + 9, _style.PageWidth - _style.Margin, y + 9);
        DrawTotalLine("Total", FormatCurrency(invoiceDocument.Total, invoiceDocument.Invoice.Currency), labelX, valueX, y, totalsTag, bold: true);

        Layout.Y = y - 36;
    }

    private void DrawTotalLine(string label, string value, double labelX, double valueX, double y, PdfTaggedElement parentTag, bool bold)
    {
        PdfTaggedElement lineTag = Tags.CreateElement("P", parentTag);
        Font font = bold ? _boldFont : _bodyFont;
        double size = bold ? 12 : 9.5;
        DrawText(label, labelX, y, size, font, _text, lineTag);
        DrawText(value, valueX, y, size, font, _text, lineTag);
    }

    private void RenderNotes(InvoiceInput invoice)
    {
        EnsureRoom(96);
        PdfTaggedElement notesTag = CreateTag("Sect", "Payment notes");
        DrawText("Payment Terms", _style.Margin, Layout.Y, 11, _boldFont, _primary, notesTag);
        Layout.Y -= 16;
        DrawParagraph(invoice.PaymentTerms, _style.Margin, Layout.Y, _style.PageWidth - (_style.Margin * 2), notesTag);
        Layout.Y -= 10;
        DrawText("Notes", _style.Margin, Layout.Y, 11, _boldFont, _primary, notesTag);
        Layout.Y -= 16;
        DrawParagraph(invoice.Notes, _style.Margin, Layout.Y, _style.PageWidth - (_style.Margin * 2), notesTag);
    }

    private void DrawParagraph(string value, double x, double y, double width, PdfTaggedElement parentTag)
    {
        PdfTaggedElement paragraphTag = Tags.CreateElement("P", parentTag);
        double currentY = y;
        foreach (string line in WrapText(value, width, _bodyFont, _style.BodyFontSize))
        {
            DrawText(line, x, currentY, _style.BodyFontSize, _bodyFont, _text, paragraphTag);
            currentY -= 13;
        }

        Layout.Y = currentY;
    }

    private void EnsureRoom(double neededHeight)
    {
        if (Layout.Y - neededHeight >= _style.Margin)
        {
            return;
        }

        Layout.AddPage(_style.PageHeight - _style.Margin);
        PdfTaggedElement pageHeaderTag = CreateTag("Sect", "Continuation page header");
        DrawText("Invoice continued", _style.Margin, Layout.Y, 11, _boldFont, _primary, pageHeaderTag);
        DrawText($"Page {Layout.PageCount}", _style.PageWidth - _style.Margin - 44, Layout.Y, 9, _bodyFont, _muted, pageHeaderTag);
        DrawRule(_style.Margin, Layout.Y - 12, _style.PageWidth - _style.Margin, Layout.Y - 12);
        Layout.Y -= 36;
    }

    private void DrawLogo(string imagePath, double x, double y, PdfTaggedElement parentTag)
    {
        PdfTaggedElement figureTag = Tags.CreateElement("Figure", parentTag);
        Tags.SetAltText(figureTag, "Seller logo");

        Image logo = new(imagePath, Document);
        double scale = Math.Min(_style.LogoMaxWidth / logo.Matrix.A, _style.LogoMaxHeight / logo.Matrix.D);
        logo.Scale(scale, scale);
        logo.Translate(x, y);
        Tags.AddTaggedElement(Layout, logo, figureTag);
    }

    private void DrawText(string value, double x, double y, double size, Font font, PdfColor color, PdfTaggedElement tag)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return;
        }

        GraphicState graphicState = new()
        {
            FillColor = color.ToPdfColor()
        };

        TextState textState = new();
        Matrix matrix = new(size, 0, 0, size, x, y);
        TextRun run = new(value, font, graphicState, textState, matrix);
        Text textElement = new();
        textElement.AddRun(run);
        Tags.AddTaggedElement(Layout, textElement, tag);
    }

    private void DrawBox(double x, double topY, double width, double height, PdfColor fillColor, PdfColor strokeColor)
    {
        Datalogics.PDFL.Path path = new();
        GraphicState state = new()
        {
            FillColor = fillColor.ToPdfColor(),
            StrokeColor = strokeColor.ToPdfColor(),
            Width = 0.5
        };

        path.GraphicState = state;
        path.PaintOp = PathPaintOpFlags.Fill | PathPaintOpFlags.Stroke;
        path.MoveTo(new Point(x, topY));
        path.AddLine(new Point(x + width, topY));
        path.AddLine(new Point(x + width, topY - height));
        path.AddLine(new Point(x, topY - height));
        path.ClosePath();
        Tags.AddArtifactElement(Layout, path);
    }

    private void DrawRule(double x1, double y1, double x2, double y2)
    {
        Datalogics.PDFL.Path path = new();
        GraphicState state = new()
        {
            StrokeColor = _border.ToPdfColor(),
            Width = 0.75
        };

        path.GraphicState = state;
        path.PaintOp = PathPaintOpFlags.Stroke;
        path.MoveTo(new Point(x1, y1));
        path.AddLine(new Point(x2, y2));
        Tags.AddArtifactElement(Layout, path);
    }

    private PdfTaggedElement CreateTag(string tagName, string actualText)
    {
        PdfTaggedElement tag = Tags.CreateElement(tagName, DocumentTag);
        _ = actualText;
        return tag;
    }

    private IReadOnlyList<string> WrapText(string value, double maxWidth, Font font, double size)
    {
        List<string> lines = new();
        string[] words = value.Split(' ', StringSplitOptions.RemoveEmptyEntries);
        string current = string.Empty;

        foreach (string word in words)
        {
            string candidate = string.IsNullOrEmpty(current) ? word : $"{current} {word}";
            if (font.MeasureTextWidth(candidate, size) <= maxWidth)
            {
                current = candidate;
                continue;
            }

            if (!string.IsNullOrEmpty(current))
            {
                lines.Add(current);
            }

            current = word;
        }

        if (!string.IsNullOrEmpty(current))
        {
            lines.Add(current);
        }

        return lines.Count == 0 ? new[] { string.Empty } : lines;
    }

    private int EstimateWrappedLineCount(string value, double maxWidth, double size)
    {
        return WrapText(value, maxWidth, _bodyFont, size).Count;
    }

    private static string FormatNumber(decimal value)
    {
        return value.ToString("0.##", CultureInfo.InvariantCulture);
    }

    private static string FormatCurrency(decimal value, string currency)
    {
        string symbol = string.Equals(currency, "USD", StringComparison.OrdinalIgnoreCase) ? "$" : $"{currency} ";
        return symbol + value.ToString("#,##0.00", CultureInfo.InvariantCulture);
    }

    private static Font CreateFont(string name)
    {
        try
        {
            return new Font(name, FontCreateFlags.Embedded | FontCreateFlags.Subset);
        }
        catch (Exception ex) when (ex is LibraryException or ApplicationException)
        {
            return new Font(name, FontCreateFlags.Subset);
        }
    }

    private Document Document => _document ?? throw new InvalidOperationException("Renderer has not been initialized.");

    private PdfTaggingContext Tags => _tags ?? throw new InvalidOperationException("Renderer has not been initialized.");

    private PdfLayoutContext Layout => _layout ?? throw new InvalidOperationException("Renderer has not been initialized.");

    private PdfTaggedElement DocumentTag => _documentTag ?? throw new InvalidOperationException("Renderer has not been initialized.");
}

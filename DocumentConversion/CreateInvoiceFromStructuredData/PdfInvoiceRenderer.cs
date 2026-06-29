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
    private InvoiceInput? _invoice;
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
        _invoice = invoiceDocument.Invoice;
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
        ApplyRestrictionPassword(invoiceDocument.Invoice);
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

        PdfTaggedElement titleTag = Tags.CreateElement("H1", headerTag);
        double titleRightX = _style.PageWidth - _style.Margin;
        DrawRightAlignedText("INVOICE", titleRightX, _style.PageHeight - _style.Margin - 10, _style.HeadingFontSize, _boldFont, _primary, titleTag);
        DrawRightAlignedText($"Invoice {invoice.InvoiceNumber}", titleRightX, _style.PageHeight - _style.Margin - 34, 11, _bodyFont, _text, headerTag);
        DrawRightAlignedText($"Issued {invoice.IssueDate}", titleRightX, _style.PageHeight - _style.Margin - 50, 9, _bodyFont, _muted, headerTag);
        DrawRightAlignedText($"Due {invoice.DueDate}", titleRightX, _style.PageHeight - _style.Margin - 64, 9, _bodyFont, _muted, headerTag);

        DrawRule(_style.Margin, _style.PageHeight - _style.Margin - 82, _style.PageWidth - _style.Margin, _style.PageHeight - _style.Margin - 82);
        DrawFooter(1);
        Layout.Y = _style.PageHeight - _style.Margin - 112;
    }

    private void RenderParties(InvoiceInput invoice)
    {
        PdfTaggedElement sectionTag = CreateTag("Sect", "Seller and customer information");

        const double cardGutter = 18;
        const double partyBlockHeight = 170;
        double columnWidth = (_style.PageWidth - (_style.Margin * 2) - cardGutter) / 2;
        double startY = Layout.Y + 6;

        DrawPartyBlock("From", invoice.Seller, _style.Margin, startY, columnWidth, partyBlockHeight, sectionTag);
        DrawPartyBlock("Bill To", invoice.Customer, _style.Margin + columnWidth + cardGutter, startY, columnWidth, partyBlockHeight, sectionTag);

        Layout.Y = startY - partyBlockHeight - 24;
    }

    private void DrawPartyBlock(string label, CompanyInfo company, double x, double topY, double width, double height, PdfTaggedElement parentTag)
    {
        PdfTaggedElement blockTag = Tags.CreateElement("P", parentTag);
        const double textInset = 14;

        DrawBox(x, topY, width, height, _accent, _border);
        DrawText(label, x + textInset, topY - 18, 9, _boldFont, _primary, blockTag);
        DrawText(company.Name, x + textInset, topY - 42, 12, _boldFont, _text, blockTag);
        if (!string.IsNullOrWhiteSpace(company.TaxId))
        {
            DrawText(company.TaxId, x + textInset, topY - 62, 9, _bodyFont, _muted, blockTag);
        }

        double currentY = topY - 86;
        foreach (string line in company.AddressLines().Where(line => !string.IsNullOrWhiteSpace(line)))
        {
            DrawText(line, x + textInset, currentY, 9, _bodyFont, _text, blockTag);
            currentY -= 13;
        }

        DrawText(company.Email, x + textInset, currentY - 4, 9, _bodyFont, _text, blockTag);
        DrawText(company.Phone, x + textInset, currentY - 21, 9, _bodyFont, _text, blockTag);
    }

    private void RenderLineItems(InvoiceDocument invoiceDocument)
    {
        EnsureRoom(120);
        PdfTaggedElement tableTag = Tags.CreateElement("Table", DocumentTag);
        double[] widths = { 72, 234, 54, 72, 72 };
        string[] headers = { "Item", "Description", "Qty", "Unit Price", "Amount" };

        DrawTableHeader(tableTag, headers, widths);

        foreach (InvoiceLineItem item in invoiceDocument.LineItems)
        {
            double rowHeight = Math.Max(26, EstimateWrappedLineCount(item.Description, widths[1] - 12, _style.BodyFontSize) * 12 + 12);
            if (EnsureRoom(rowHeight + 44))
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
        EnsureRoom(132);
        PdfTaggedElement totalsTag = CreateTag("Sect", "Invoice totals");
        const double totalsWidth = 214;
        const double totalsHeight = 92;
        const double inset = 14;
        double boxX = _style.PageWidth - _style.Margin - totalsWidth;
        double boxTopY = Layout.Y - 6;
        double labelX = boxX + inset;
        double valueRightX = boxX + totalsWidth - inset;
        double y = boxTopY - 24;

        DrawBox(boxX, boxTopY, totalsWidth, totalsHeight, _accent, _border);
        DrawTotalLine("Subtotal", FormatCurrency(invoiceDocument.Subtotal, invoiceDocument.Invoice.Currency), labelX, valueRightX, y, totalsTag, bold: false);
        y -= 20;
        DrawTotalLine($"Tax ({FormatPercent(invoiceDocument.Invoice.TaxRate)})", FormatCurrency(invoiceDocument.Tax, invoiceDocument.Invoice.Currency), labelX, valueRightX, y, totalsTag, bold: false);
        y -= 16;
        DrawRule(boxX + inset, y, boxX + totalsWidth - inset, y);
        y -= 20;
        DrawTotalLine("Total", FormatCurrency(invoiceDocument.Total, invoiceDocument.Invoice.Currency), labelX, valueRightX, y, totalsTag, bold: true);

        Layout.Y = boxTopY - totalsHeight - 28;
    }

    private void DrawTotalLine(string label, string value, double labelX, double valueRightX, double y, PdfTaggedElement parentTag, bool bold)
    {
        PdfTaggedElement lineTag = Tags.CreateElement("P", parentTag);
        Font font = bold ? _boldFont : _bodyFont;
        double size = bold ? 12 : 9.5;
        DrawText(label, labelX, y, size, font, _text, lineTag);
        DrawRightAlignedText(value, valueRightX, y, size, font, _text, lineTag);
    }

    private void RenderNotes(InvoiceInput invoice)
    {
        EnsureRoom(96);
        PdfTaggedElement notesTag = CreateTag("Sect", "Payment notes");
        PdfTaggedElement paymentHeadingTag = Tags.CreateElement("H2", notesTag);
        DrawText("Payment Terms", _style.Margin, Layout.Y, 11, _boldFont, _primary, paymentHeadingTag);
        Layout.Y -= 16;
        DrawParagraph(invoice.PaymentTerms, _style.Margin, Layout.Y, _style.PageWidth - (_style.Margin * 2), notesTag);
        Layout.Y -= 10;
        PdfTaggedElement notesHeadingTag = Tags.CreateElement("H2", notesTag);
        DrawText("Notes", _style.Margin, Layout.Y, 11, _boldFont, _primary, notesHeadingTag);
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

    private bool EnsureRoom(double neededHeight)
    {
        if (Layout.Y - neededHeight >= _style.Margin)
        {
            return false;
        }

        Layout.AddPage(_style.PageHeight - _style.Margin);
        PdfTaggedElement pageHeaderTag = CreateTag("Sect", "Continuation page header");
        DrawText("Invoice continued", _style.Margin, Layout.Y, 11, _boldFont, _primary, pageHeaderTag);
        DrawRule(_style.Margin, Layout.Y - 12, _style.PageWidth - _style.Margin, Layout.Y - 12);
        DrawFooter(Layout.PageCount);
        Layout.Y -= 36;
        return true;
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

    private void DrawRightAlignedText(string value, double rightX, double y, double size, Font font, PdfColor color, PdfTaggedElement tag)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return;
        }

        double x = rightX - font.MeasureTextWidth(value, size);
        DrawText(value, x, y, size, font, color, tag);
    }

    private void DrawFooter(int pageNumber)
    {
        if (_invoice is null)
        {
            return;
        }

        double lineY = _style.Margin - 14;
        double baselineY = _style.Margin - 31;

        DrawRule(_style.Margin, lineY, _style.PageWidth - _style.Margin, lineY);

        string leftText = _invoice.Seller.Name;
        string middleText = $"{_invoice.Seller.Email} | {_invoice.Seller.Phone}";
        string rightText = $"Page {pageNumber}";

        DrawArtifactText(leftText, _style.Margin, baselineY, 7.2, _bodyFont, _muted);
        double middleWidth = _bodyFont.MeasureTextWidth(middleText, 7.2);
        DrawArtifactText(middleText, (_style.PageWidth - middleWidth) / 2, baselineY, 7.2, _bodyFont, _muted);
        double rightWidth = _bodyFont.MeasureTextWidth(rightText, 7.2);
        DrawArtifactText(rightText, _style.PageWidth - _style.Margin - rightWidth, baselineY, 7.2, _bodyFont, _muted);
    }

    private void DrawArtifactText(string value, double x, double y, double size, Font font, PdfColor color)
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
        Tags.AddArtifactElement(Layout, textElement);
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

    private static string FormatPercent(decimal value)
    {
        return (value * 100m).ToString("0.##", CultureInfo.InvariantCulture) + "%";
    }

    private void ApplyRestrictionPassword(InvoiceInput invoice)
    {
        if (!invoice.ApplyRestrictionPassword)
        {
            return;
        }

        PermissionFlags allowedUserActions =
            PermissionFlags.Open |
            PermissionFlags.Print |
            PermissionFlags.HighPrint |
            PermissionFlags.Copy |
            PermissionFlags.Accessible |
            PermissionFlags.SaveAs;

        Document.Secure(
            allowedUserActions,
            invoice.RestrictionPassword,
            null,
            EncryptionType.AES256_AcroX,
            encryptMetadata: true);
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

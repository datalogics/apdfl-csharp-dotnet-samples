using System.Diagnostics.CodeAnalysis;
using System.Globalization;
using System.Net;
using System.Text;
using System.Text.RegularExpressions;
using Datalogics.PDFL;

namespace MarkdownToPdf;

internal sealed class PdfMarkdownRenderer : IDisposable
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

    public void Dispose()
    {
        _fonts.Dispose();
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


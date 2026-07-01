using System.Diagnostics.CodeAnalysis;
using System.Globalization;
using System.Net;
using System.Text;
using System.Text.RegularExpressions;
using Datalogics.PDFL;

namespace MarkdownToPdf;


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


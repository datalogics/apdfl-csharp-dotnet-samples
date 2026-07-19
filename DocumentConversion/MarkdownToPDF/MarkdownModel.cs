using System.Diagnostics.CodeAnalysis;
using System.Globalization;
using System.Net;
using System.Text;
using System.Text.RegularExpressions;
using Datalogics.PDFL;

namespace MarkdownToPDF;

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

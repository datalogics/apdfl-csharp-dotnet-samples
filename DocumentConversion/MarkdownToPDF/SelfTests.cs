using System.Diagnostics.CodeAnalysis;
using System.Globalization;
using System.Net;
using System.Text;
using System.Text.RegularExpressions;
using Datalogics.PDFL;

namespace MarkdownToPdf;

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

            ConversionOptions defaultOptions = ConversionOptions.Parse(Array.Empty<string>());
            Require(string.Equals(defaultOptions.InputPath, "sample.md", StringComparison.Ordinal), "default input path parsed");
            Require(string.Equals(defaultOptions.OutputPath, "output.pdf", StringComparison.Ordinal), "default output path parsed");
            Require(defaultOptions.Overwrite, "default sample output can be replaced");

            ConversionOptions defaultVerboseOptions = ConversionOptions.Parse(new[] { "--verbose" });
            Require(string.Equals(defaultVerboseOptions.InputPath, "sample.md", StringComparison.Ordinal), "default input path parsed with option");
            Require(defaultVerboseOptions.Verbose, "default options can include switches");

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

            ConversionOptions fontAliasOptions = ConversionOptions.Parse(new[] { "input.md", "output.pdf", "--font-family", "serif", "--code-font-family", "mono", "--recursive" });
            Require(string.Equals(fontAliasOptions.FontFamily, "Times", StringComparison.Ordinal), "base serif alias parsed");
            Require(string.Equals(fontAliasOptions.CodeFontFamily, "Courier", StringComparison.Ordinal), "base mono alias parsed");
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

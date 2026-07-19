# MarkdownToPDF Sample Documents and Feature Guide

This folder contains Markdown files that exercise the public APDFL Markdown-to-tagged-PDF sample.

## Running all samples

From the project root:

```powershell
dotnet run -- samples sample-pdfs --overwrite --recursive --verbose
```

Use A4 landscape and a sans-serif font:

```powershell
dotnet run -- samples sample-pdfs --overwrite --recursive --verbose --page-size A4 --orientation landscape --font-family Helvetica --heading-font-family Helvetica --code-font-family Courier
```

Use Windows fonts and a CJK fallback font:

```powershell
dotnet run -- samples sample-pdfs --overwrite --recursive --verbose --font-family Arial --heading-font-family Arial --code-font-family Consolas --cjk-font-family "Microsoft YaHei"
```

## Included sample files

- `html-lite-newsletter.md` demonstrates lightweight HTML wrappers, links, tables, bullets used as inline separators, and image omission placeholders.
- `long.md` demonstrates pagination with many sections.
- `unsupported-images.md` demonstrates the current image exclusion behavior.
- `multilingual-cjk-cyrillic.md` demonstrates Chinese and Russian text handling.
- `code-and-identifiers.md` demonstrates code block styling, inline code backgrounds, and underscore-safe identifier parsing.
- `raw-html-config.md` demonstrates optional literal rendering of unsupported/raw HTML tags.

## Supported command-line forms

```text
MarkdownToPDF.exe [options]
MarkdownToPDF.exe input.md output.pdf [options]
MarkdownToPDF.exe input-folder output-folder [options]
```

When no input or output arguments are supplied, the sample converts `sample.md` to `output.pdf` and replaces `output.pdf` if it already exists.

When the input is a folder, the output must be a folder. The tool scans for `.md` files. With `--recursive`, subfolders are included and the relative folder structure is preserved in the output folder.

## Options

Input/output:

```text
no input/output arguments        Convert sample.md to output.pdf.
--recursive                     In folder mode, include subfolders and preserve relative paths.
--overwrite                     Replace existing output PDF files.
--verbose                       Print conversion settings and summary information.
```

Metadata:

```text
--title <text>                  PDF document title. Defaults to the input file name.
--lang <tag>                    Document language tag. Defaults to en-US.
```

Page setup:

```text
--page-size <value>             Letter, Legal, Ledger, A3, A4, A5, Tabloid, or WIDTHxHEIGHT in points.
--orientation <value>           Auto, Portrait, or Landscape. Defaults to Auto.
--margin <points>               Margin on all sides in PDF points. Defaults to 72.
```

Fonts:

```text
--font-family <value>           Body font family. Defaults to Times.
--heading-font-family <value>   Heading font family. Defaults to the body font family.
--code-font-family <value>      Monospace/code font family. Defaults to Courier.
--cjk-font-family <value>       Preferred CJK font family/name for Chinese/Japanese/Korean text.
--fallback-font-family <value>  Add a fallback font name. Can be repeated.
--fallback-fonts <csv>          Add comma-separated fallback font names.
--list-font-families            Print recognized font-family names and aliases.
```

HTML handling:

```text
--include-unrendered-html       Render unsupported/raw HTML tags as visible text instead of stripping them.
--include-raw-html              Alias for --include-unrendered-html.
```

Help/tests:

```text
--self-test                     Run parser/inline self-tests without creating a PDF.
--help, -h, /?                  Show help.
```

## Supported Markdown subset

Block-level Markdown:

- ATX headings: `#` through `######`
- Setext headings: text followed by `===` or `---`
- Paragraphs separated by blank lines
- Fenced code blocks with triple backticks or tildes
- Optional fenced-code language labels, for example ```` ```PowerShell ````
- Unordered lists using `-`, `*`, or `+`
- Ordered lists using `1.`, `2.`, etc.
- Task list markers: `- [x]` and `- [ ]`
- Nested-looking list indentation
- Blockquotes using `>`
- Horizontal rules using `---`, `***`, or `___`
- Pipe tables with alignment separators: `---`, `---:`, and `:---:`

Inline Markdown:

- Bold: `**text**` and `__text__`
- Italic: `*text*` and `_text_`
- Bold italic: `***text***` and `___text___`
- Strikethrough: `~~text~~`
- Inline code with backticks
- Inline links: `[label](url)`
- Reference links: `[label][id]` plus `[id]: url`
- Shortcut reference links: `[id]`
- Autolinks: `<https://example.com>`
- Bare `http://` and `https://` URLs
- Basic backslash escapes
- Basic HTML entity decoding such as `&amp;`

Lightweight HTML compatibility:

- `<br>`, `<br/>`, and `<br />` become paragraph breaks.
- Container tags such as `<div>`, `<p>`, `<section>`, and `</div>` are ignored while preserving their inner Markdown/text.
- `<strong>` and `<b>` become bold.
- `<em>` and `<i>` become italic.
- `<code>`, `<kbd>`, and `<samp>` become inline code.
- `<del>`, `<s>`, and `<strike>` become strikethrough.
- `<a href="...">label</a>` becomes a Markdown link.
- `<img>` tags are omitted, consistent with Markdown image omission.
- By default, unsupported/raw HTML tags are stripped. With `--include-unrendered-html`, unsupported tags such as `<field name="...">` and `</field>` are rendered as visible text.

## Rendering behavior

The sample directly creates PDF content with APDFL. It does not convert Markdown to HTML and does not use a browser engine.

Layout behavior:

- PDF points are used for all measurements.
- The default page size is Letter.
- Default margin is 72 points.
- Text is laid out top-down while respecting PDF's bottom-left coordinate system.
- Paragraphs, headings, table cells, and list items wrap to the available width.
- Long Latin words are split only when needed.
- CJK text is tokenized so line breaks can occur between CJK characters.
- CJK character measurement is capped to approximately one em to avoid overly conservative wrapping with some named-font combinations.
- Cyrillic/Greek characters use general Unicode fallback fonts when needed so wrapping is based on the font actually used for drawing. The sample also caps overly large Cyrillic/Greek character advances when APDFL reports widths that are wider than the rendered glyphs, preventing Russian text from spreading words across the line.
- Page breaks are inserted before content crosses the bottom margin.
- Tables are split by row chunks when needed; table headers are not repeated.

Code styling:

- Inline code uses the configured code font and a light gray background.
- Fenced code blocks use a light gray background across the code area.
- If a language label is provided, the sample renders a small title row such as `</> PowerShell` at the top of the code block.
- Syntax highlighting is intentionally not implemented.

Link behavior:

- Markdown links visibly render only the anchor text.
- The URL is stored separately and applied as a clickable PDF URI annotation.
- Bare URLs visibly render as URLs because the URL itself is the anchor text.

Image behavior:

- Images are intentionally excluded in this version.
- Markdown image syntax such as `![alt](image.png)` renders as `[Image omitted: alt]`.
- HTML `<img>` tags render as the same kind of omitted-image placeholder.

## Tagged PDF behavior

The sample creates tagged PDF structure while it creates page content. It includes:

- `/MarkInfo`
- `/StructTreeRoot`
- `/ParentTree`
- page `/StructParents`
- page `/Tabs /S`
- MCIDs
- marked-content containers
- artifacts for visual-only content

Common structure tags produced by the sample:

- `Document`
- `H1` through `H6`
- `P`
- `L`, `LI`, `Lbl`, `LBody`
- `BlockQuote`
- `Code`
- `Span` for strikethrough text
- `Link` with URI link annotations
- `Table`, `TR`, `TH`, `TD`

Visual-only content such as table grid lines, horizontal rules, quote markers, strikethrough strokes, and code/inline-code background rectangles are marked as artifacts.

## Font behavior and CJK notes

The body, heading, and code fonts are selected with:

```powershell
--font-family Arial --heading-font-family Georgia --code-font-family Consolas
```

Recognized families include core PDF fonts and common system fonts. Run:

```powershell
dotnet run -- --list-font-families
```

CJK text needs a font that contains Chinese/Japanese/Korean glyphs. On Windows 11, start with:

```powershell
--cjk-font-family "Microsoft YaHei"
```

If that does not work in your APDFL environment, try:

```powershell
--fallback-font-family SimSun
--fallback-font-family "Microsoft JhengHei"
--fallback-font-family "Noto Sans CJK SC"
```

CJK fallback fonts are tried for Chinese/Japanese/Korean characters and punctuation. General Unicode fallback fonts are tried for Cyrillic and Greek characters when needed. If a fallback font cannot be created by APDFL, the sample skips that fallback and tries the next one.

## Current explicit exclusions

This sample does not currently implement:

- image loading/embedding/tagging
- remote image downloads
- SVG rendering
- full HTML layout/rendering; unsupported/raw HTML can optionally be shown as literal text
- full CommonMark/GitHub-Flavored Markdown coverage
- footnotes
- multi-paragraph list items
- complex nested block parsing inside blockquotes/list items
- table row spans or column spans
- repeating table headers across page breaks
- syntax highlighting
- right-to-left shaping for Arabic/Hebrew
- arbitrary font file loading from command-line paths
- headers, footers, page numbers, bookmarks, or table of contents

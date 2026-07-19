# MarkdownToPDF Tagged APDFL Sample

This sample shows how to turn Markdown into a new, tagged PDF by using Datalogics Adobe PDF Library SDK/APDFL directly. It does not convert Markdown to HTML, it does not drive a browser, and it does not rely on a separate Markdown or PDF rendering library. The goal is to show that PDFL can be used on its own to generate structured PDF output from plain text content.

## What this sample does

At a practical level, this is a C#/.NET 8 console program that reads Markdown, interprets a useful subset of Markdown formatting, lays out the content on PDF pages, tags the output for accessibility, creates links, and saves the final PDF.

It is designed as a public sample, not a full Markdown product. That means it is intentionally focused on showing the PDF generation mechanics clearly:

- create a PDF from scratch
- measure and wrap text
- paginate content across pages
- draw tables, lists, code blocks, and blockquotes
- build a tagged PDF structure tree
- add clickable link annotations
- embed fonts for reliable output

## Why this sample exists

This sample helps answer a common question: can PDFL be used for PDF generation, not just PDF manipulation or conversion? The answer is yes. If a customer can produce their content in Markdown, they can use the same PDFL toolkit to generate a polished PDF directly from that source content.

That also makes this sample a good starting point for adjacent document-generation workflows, especially where the source content is already structured or can be transformed into a Markdown-like intermediate format.

## How the sample works

The program reads a Markdown file, parses supported Markdown constructs into an internal document model, then renders that model into PDF content with APDFL. As it renders, it also creates the tagged PDF structure so the output is not just visually correct, but structurally meaningful.

In other words, the sample is doing the document-generation work itself:

- parsing block and inline Markdown
- deciding where lines and page breaks belong
- choosing fonts and fallback fonts
- drawing text and simple shapes
- creating tags and annotations

That is the point of the sample. It demonstrates the PDF generation capabilities of PDFL without introducing extra rendering dependencies.

## AI-assisted development note

This sample was developed iteratively with AI assistance, but not from a single magic prompt. The useful pattern was to combine clear requirements, small reviewable changes, and repeated visual/testing feedback.

If someone wants to extend the sample, a better starting point than "generate a whole app" is a focused prompt such as:

```text
Extend this APDFL Markdown-to-PDF sample without adding new dependencies.
Keep PDFL as the only library for parsing, layout, rendering, tagging, and
link creation. Add support for [feature], update the self-tests, and explain
any tradeoffs or limitations in the README.
```

That framing keeps the constraints clear and makes it easier to evolve the sample in a way that still matches its purpose.

## Prerequisites

- .NET 8 SDK
- Datalogics APDFL .NET package restored from NuGet: `Adobe.PDF.Library.LM.NET`
- APDFL activation through the normal prompt or by setting `APDFL_LICENSE_KEY`

PowerShell example:

```powershell
$env:APDFL_LICENSE_KEY = "your-license-key"
```

## Quick start

```powershell
dotnet restore
dotnet build
dotnet run
start output.pdf
```

Running with no input or output arguments converts `sample.md` to `output.pdf` and replaces `output.pdf` if it already exists.

Run the built-in parser/configuration tests:

```powershell
dotnet run -- --self-test
```

Show full command-line help:

```powershell
dotnet run -- --help
```

List recognized font families:

```powershell
dotnet run -- --list-font-families
```

## Common examples

A4 landscape output with sans-serif body and heading fonts:

```powershell
dotnet run -- sample.md output-a4-landscape.pdf --overwrite --page-size A4 --orientation landscape --font-family Helvetica --heading-font-family Helvetica --code-font-family Courier --verbose
```

Windows/common-font example:

```powershell
dotnet run -- sample.md output-arial.pdf --overwrite --font-family Arial --heading-font-family Georgia --code-font-family Consolas --verbose
```

Chinese/CJK and Cyrillic-focused example on Windows 11:

```powershell
dotnet run -- samples/multilingual-cjk-cyrillic.md multilingual.pdf --overwrite --font-family Arial --cjk-font-family "Microsoft YaHei" --fallback-fonts "Arial,Times New Roman,Microsoft YaHei,SimSun" --verbose
```

Render unsupported/raw HTML tags literally, useful for Markdown that documents XML-like configuration blocks:

```powershell
dotnet run -- samples/raw-html-config.md raw-html-config.pdf --overwrite --include-unrendered-html --verbose
```

Batch-convert a folder of Markdown files:

```powershell
dotnet run -- samples sample-pdfs --overwrite --recursive --verbose
```

Publish a Windows executable:

```powershell
dotnet publish -c Release -r win-x64 --self-contained false
```

## Command line

```text
MarkdownToPDF.exe [options]
MarkdownToPDF.exe input.md output.pdf [options]
MarkdownToPDF.exe input-folder output-folder [options]
```

Input/output options:

```text
no input/output arguments        Convert sample.md to output.pdf.
--recursive                     In folder mode, include subfolders and preserve relative paths.
--overwrite                     Replace existing output PDF files.
--verbose                       Print conversion settings and summary information.
```

Document metadata:

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

Diagnostics/help:

```text
--self-test                     Run parser/inline self-tests without creating a PDF.
--help, -h, /?                  Show help.
```

## Page sizes and orientation

Supported named page sizes are `Letter`, `Legal`, `Ledger`, `A3`, `A4`, `A5`, and `Tabloid`. Custom page sizes use PDF points in `WIDTHxHEIGHT` form, for example `612x792`.

Orientation is applied by creating pages with the resolved media box dimensions, not by rotating page content. `Auto` preserves the named page size's native orientation. For example, `Ledger` resolves as 17 x 11 inches unless `--orientation portrait` is supplied.

## Fonts and multilingual text

The sample recognizes common family aliases such as `Times`, `Helvetica`, `Courier`, `Arial`, `Calibri`, `Cambria`, `Consolas`, `Georgia`, `Times New Roman`, `Verdana`, `Microsoft YaHei`, `SimSun`, `Malgun Gothic`, and `Noto Sans CJK SC`. The core PDF families `Times`, `Helvetica`, and `Courier` are the most portable. Other families require those fonts to be available to APDFL on the machine running the sample.

For multilingual text, the renderer uses script-aware wrapping and fallback fonts. Chinese/Japanese/Korean text uses CJK-aware tokenization and CJK fallback fonts. Cyrillic and Greek text use general Unicode fallback fonts when the selected body font is one of the PDF base families that may not contain those glyphs. You can control this explicitly:

```powershell
dotnet run -- samples/multilingual-cjk-cyrillic.md multilingual.pdf --overwrite --font-family Arial --cjk-font-family "Microsoft YaHei" --fallback-fonts "Arial,Times New Roman,Microsoft YaHei,SimSun" --verbose
```

Fallback fonts are tried lazily. If a named fallback font is not available to APDFL, the sample skips it and tries the next fallback candidate. The generated document calls `Document.EmbedFonts()` before saving so the PDF includes the font/glyph data APDFL can embed. CJK character measurement is capped to approximately one em per character to avoid overly conservative line breaks with some named-font combinations. Cyrillic/Greek character advances also use a narrow upper-bound correction when APDFL reports widths that are much larger than the glyphs render, preventing Russian text from spreading words across a line.

## Markdown support

See `samples/README.md` for the full supported Markdown list, unsupported items, and examples. In summary, this sample supports headings, paragraphs, inline emphasis, links, code, fenced code blocks, lists, task list markers, blockquotes, horizontal rules, reference links, autolinks, pipe tables, lightweight HTML compatibility, tagged output, folder processing, configurable page size/orientation/margins, configurable fonts, CJK/Cyrillic-aware font fallback and wrapping, and optional literal rendering of unsupported/raw HTML tags.

Images remain intentionally excluded in this version. Markdown images and simple HTML `<img>` tags render as omitted placeholders instead of loading or embedding image files.

## How to extend or modify it

For most follow-on work, the easiest path is to keep the current separation of responsibilities:

- `MarkdownParser`: block parsing, reference definitions, tables, lists, and fenced code blocks
- `InlineParser`: inline styles, links, autolinks, image exclusion behavior, and CommonMark-style delimiter checks for emphasis
- `PdfMarkdownRenderer`: APDFL content creation, line wrapping, CJK-aware tokenization, table layout, code shading, link annotations, and pagination
- `PdfTaggingContext`: marked content, MCIDs, parent tree, structure tree, artifacts, and link annotation object references
- `PdfTheme`: page size, orientation-resolved dimensions, margins, font sizes, spacing, code backgrounds, and table settings
- `PdfFontSet`: font-family mapping plus CJK and general Unicode fallback font resolution

If you are modifying the sample, it is worth testing both parser behavior and output behavior. The built-in `--self-test` mode is there to make small changes easier to validate before doing a full PDF review.

## Limitations

This sample is intentionally not a full CommonMark or GitHub-Flavored Markdown implementation. It is a focused demonstration of PDF generation with PDFL. That means some omissions are by design, not by accident. The biggest current exclusions are images, full HTML rendering, advanced nested structures, repeating table headers across page breaks, syntax highlighting, and right-to-left shaping.

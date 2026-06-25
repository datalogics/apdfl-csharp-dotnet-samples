# MarkdownToPdf Tagged APDFL Sample

This is a C#/.NET 8 console sample that converts a practical Markdown subset into a newly created, tagged PDF using Datalogics Adobe PDF Library SDK/APDFL. It does not use HTML-to-PDF conversion, a browser engine, or a third-party Markdown/PDF renderer. The sample creates pages, measures text, wraps lines, paginates content, adds tagged marked-content containers, builds a structure tree, creates link annotations, and saves the PDF with APDFL.

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
dotnet run -- sample.md output.pdf --overwrite --title "MarkdownToPdf APDFL Sample" --verbose
start output.pdf
```

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
MarkdownToPdf.exe input.md output.pdf [options]
MarkdownToPdf.exe input-folder output-folder [options]
```

Input/output options:

```text
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

## Main code areas

- `MarkdownParser`: block parsing, reference definitions, tables, lists, and fenced code blocks
- `InlineParser`: inline styles, links, autolinks, image exclusion behavior, and CommonMark-style delimiter checks for emphasis
- `PdfMarkdownRenderer`: APDFL content creation, line wrapping, CJK-aware tokenization, table layout, code shading, link annotations, and pagination
- `PdfTaggingContext`: marked content, MCIDs, parent tree, structure tree, artifacts, and link annotation object references
- `PdfTheme`: page size, orientation-resolved dimensions, margins, font sizes, spacing, code backgrounds, and table settings
- `PdfFontSet`: font-family mapping plus CJK and general Unicode fallback font resolution

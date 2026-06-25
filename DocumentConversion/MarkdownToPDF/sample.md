# Markdown to Tagged PDF with APDFL

This sample demonstrates **bold text**, *italic text*, ***bold italic text***, ~~strikethrough text~~, `inline code`, a bare URL https://example.com, an autolink <https://docs.example.com>, and a [reference link][guide].

[guide]: https://example.com/guide

## Task Lists

- [x] Parse a useful Markdown subset directly in C#.
- [x] Measure text with APDFL fonts and wrap lines.
- [x] Create tagged PDF structure while adding content.
- [ ] Add image embedding in a future iteration.

## Ordered and Unordered Lists

1. First ordered item with enough text to demonstrate word wrapping across multiple PDF lines inside the same list item.
2. Second ordered item with `inline code` and **bold text**.

- First unordered item.
  - Nested-looking item rendered with deeper indentation.
  - Another nested-looking item.

## Blockquote

> This is a blockquote. It is indented, styled, and tagged while the visual quote marker is treated as layout artifact content.

## Table

| Feature | Markdown Example | Status |
| --- | --- | ---: |
| Headings | `# Title` | Supported |
| Tables | pipe table syntax | Supported |
| Task lists | `- [x] item` | Supported |
| Reference links | `[label][id]` | Supported |
| Images | `![alt](file.png)` | Excluded for now |

## Code

```csharp
using System;

public static class Demo
{
    public static void Main()
    {
        Console.WriteLine("Code blocks preserve indentation and wrap long lines when needed.");
    }
}
```

---

Image syntax is intentionally not embedded by this sample. This markup is rendered as text instead: ![Architecture diagram](diagram.png)

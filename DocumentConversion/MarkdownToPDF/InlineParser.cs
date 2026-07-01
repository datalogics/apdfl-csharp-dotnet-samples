using System.Diagnostics.CodeAnalysis;
using System.Globalization;
using System.Net;
using System.Text;
using System.Text.RegularExpressions;
using Datalogics.PDFL;

namespace MarkdownToPDF;

internal static class InlineParser
{
    private static readonly Regex EmailRegex = new(@"^[^@\s<>]+@[^@\s<>]+\.[^@\s<>]+$", RegexOptions.Compiled);

    public static List<InlineRun> Parse(string text, IReadOnlyDictionary<string, string>? references = null)
    {
        List<InlineRun> runs = new();
        ParseInto(text, new InlineStyle(), runs, references ?? new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase));
        return runs;
    }

    private static void ParseInto(string text, InlineStyle style, List<InlineRun> runs, IReadOnlyDictionary<string, string> references)
    {
        int i = 0;

        while (i < text.Length)
        {
            if (text[i] == '\\' && i + 1 < text.Length)
            {
                AddRun(runs, text[i + 1].ToString(), style);
                i += 2;
                continue;
            }

            if (text[i] == '`')
            {
                int end = text.IndexOf('`', i + 1);
                if (end > i)
                {
                    string code = text.Substring(i + 1, end - i - 1);
                    AddRun(runs, code, style with { Code = true });
                    i = end + 1;
                    continue;
                }

                AddRun(runs, "`", style);
                i++;
                continue;
            }

            if (StartsWith(text, i, "~~"))
            {
                if (TryReadStrikethrough(text, i, style, runs, references, out int next))
                {
                    i = next;
                    continue;
                }

                AddRun(runs, "~", style);
                i++;
                continue;
            }

            if (i + 1 < text.Length && text[i] == '!' && text[i + 1] == '[')
            {
                if (TryReadBracketLink(text, i, image: true, out string alt, out string url, out int next))
                {
                    string cleanAlt = string.IsNullOrWhiteSpace(alt) ? url : alt.Trim();
                    string rendered = string.IsNullOrWhiteSpace(cleanAlt)
                        ? "[Image omitted]"
                        : $"[Image omitted: {cleanAlt}]";

                    AddRun(runs, rendered, style with { Italic = true });
                    i = next;
                    continue;
                }

                AddRun(runs, "!", style);
                i++;
                continue;
            }

            if (text[i] == '[')
            {
                if (TryReadBracketLink(text, i, image: false, out string label, out string url, out int next))
                {
                    AddLinkRun(runs, label, url, style);
                    i = next;
                    continue;
                }

                if (TryReadReferenceLink(text, i, references, out label, out url, out next))
                {
                    AddLinkRun(runs, label, url, style);
                    i = next;
                    continue;
                }

                AddRun(runs, "[", style);
                i++;
                continue;
            }

            if (text[i] == '<')
            {
                if (TryReadAutoLink(text, i, out string label, out string url, out int next))
                {
                    AddLinkRun(runs, label, url, style);
                    i = next;
                    continue;
                }

                AddRun(runs, "<", style);
                i++;
                continue;
            }

            if (StartsWith(text, i, "http://") || StartsWith(text, i, "https://"))
            {
                if (TryReadBareUrl(text, i, out string url, out int next))
                {
                    AddLinkRun(runs, url, url, style);
                    i = next;
                    continue;
                }
            }

            if (text[i] == '*' || text[i] == '_')
            {
                if (TryReadEmphasis(text, i, style, runs, references, out int next))
                {
                    i = next;
                    continue;
                }

                AddRun(runs, text[i].ToString(), style);
                i++;
                continue;
            }

            int nextSpecial = FindNextSpecial(text, i);
            string plain = text.Substring(i, nextSpecial - i);
            AddRun(runs, plain, style);
            i = nextSpecial;
        }
    }

    private static bool TryReadBracketLink(
        string text,
        int start,
        bool image,
        out string label,
        out string url,
        out int nextIndex)
    {
        int labelStart = start + (image ? 2 : 1);
        int closeBracket = text.IndexOf(']', labelStart);

        if (closeBracket < 0 || closeBracket + 1 >= text.Length || text[closeBracket + 1] != '(')
        {
            label = string.Empty;
            url = string.Empty;
            nextIndex = start;
            return false;
        }

        int urlStart = closeBracket + 2;
        int closeParen = text.IndexOf(')', urlStart);

        if (closeParen < 0)
        {
            label = string.Empty;
            url = string.Empty;
            nextIndex = start;
            return false;
        }

        label = text.Substring(labelStart, closeBracket - labelStart);
        url = text.Substring(urlStart, closeParen - urlStart).Trim();
        nextIndex = closeParen + 1;
        return true;
    }

    private static bool TryReadReferenceLink(
        string text,
        int start,
        IReadOnlyDictionary<string, string> references,
        out string label,
        out string url,
        out int nextIndex)
    {
        label = string.Empty;
        url = string.Empty;
        nextIndex = start;

        int closeLabel = text.IndexOf(']', start + 1);
        if (closeLabel < 0)
        {
            return false;
        }

        string firstLabel = text.Substring(start + 1, closeLabel - start - 1);

        if (closeLabel + 1 < text.Length && text[closeLabel + 1] == '[')
        {
            int closeRef = text.IndexOf(']', closeLabel + 2);
            if (closeRef < 0)
            {
                return false;
            }

            string referenceLabel = text.Substring(closeLabel + 2, closeRef - closeLabel - 2);
            if (referenceLabel.Length == 0)
            {
                referenceLabel = firstLabel;
            }

            if (references.TryGetValue(NormalizeReferenceLabel(referenceLabel), out string? foundUrl))
            {
                label = firstLabel;
                url = foundUrl;
                nextIndex = closeRef + 1;
                return true;
            }
        }

        if (references.TryGetValue(NormalizeReferenceLabel(firstLabel), out string? shortcutUrl))
        {
            label = firstLabel;
            url = shortcutUrl;
            nextIndex = closeLabel + 1;
            return true;
        }

        return false;
    }

    private static bool TryReadAutoLink(string text, int start, out string label, out string url, out int nextIndex)
    {
        label = string.Empty;
        url = string.Empty;
        nextIndex = start;

        int close = text.IndexOf('>', start + 1);
        if (close < 0)
        {
            return false;
        }

        string candidate = text.Substring(start + 1, close - start - 1).Trim();

        if (candidate.StartsWith("http://", StringComparison.OrdinalIgnoreCase) || candidate.StartsWith("https://", StringComparison.OrdinalIgnoreCase))
        {
            label = candidate;
            url = candidate;
            nextIndex = close + 1;
            return true;
        }

        if (EmailRegex.IsMatch(candidate))
        {
            label = candidate;
            url = "mailto:" + candidate;
            nextIndex = close + 1;
            return true;
        }

        return false;
    }

    private static bool TryReadBareUrl(string text, int start, out string url, out int nextIndex)
    {
        int index = start;
        while (index < text.Length && !char.IsWhiteSpace(text[index]) && text[index] != '<' && text[index] != '>')
        {
            index++;
        }

        url = text.Substring(start, index - start).TrimEnd('.', ',', ';', ':', ')', ']');
        nextIndex = start + url.Length;
        return url.Length > 0;
    }

    private static bool TryReadStrikethrough(
        string text,
        int start,
        InlineStyle style,
        List<InlineRun> runs,
        IReadOnlyDictionary<string, string> references,
        out int nextIndex)
    {
        nextIndex = start;

        if (!CanOpenSimpleDelimiter(text, start, 2))
        {
            return false;
        }

        int index = start + 2;
        while (index < text.Length)
        {
            int found = text.IndexOf("~~", index, StringComparison.Ordinal);
            if (found < 0)
            {
                return false;
            }

            if ((found == 0 || text[found - 1] != '\\') && CanCloseSimpleDelimiter(text, found, 2))
            {
                string inner = text.Substring(start + 2, found - start - 2);
                ParseInto(inner, style with { Strike = true }, runs, references);
                nextIndex = found + 2;
                return true;
            }

            index = found + 2;
        }

        return false;
    }

    private static bool TryReadEmphasis(
        string text,
        int start,
        InlineStyle style,
        List<InlineRun> runs,
        IReadOnlyDictionary<string, string> references,
        out int nextIndex)
    {
        nextIndex = start;
        char delimiter = text[start];
        int runLength = CountRepeated(text, start, delimiter);
        int maxLength = Math.Min(3, runLength);

        for (int length = maxLength; length >= 1; length--)
        {
            if (!CanOpenEmphasis(text, start, length, delimiter))
            {
                continue;
            }

            int close = FindClosingEmphasis(text, delimiter, length, start + length);
            if (close < 0)
            {
                continue;
            }

            string inner = text.Substring(start + length, close - start - length);
            InlineStyle nestedStyle = length switch
            {
                3 => style with { Bold = true, Italic = true },
                2 => style with { Bold = true },
                _ => style with { Italic = true }
            };

            ParseInto(inner, nestedStyle, runs, references);
            nextIndex = close + length;
            return true;
        }

        return false;
    }

    private static int FindClosingEmphasis(string text, char delimiter, int length, int startIndex)
    {
        string marker = new(delimiter, length);
        int index = startIndex;

        while (index < text.Length)
        {
            int found = text.IndexOf(marker, index, StringComparison.Ordinal);
            if (found < 0)
            {
                return -1;
            }

            if ((found == 0 || text[found - 1] != '\\') && CanCloseEmphasis(text, found, length, delimiter))
            {
                return found;
            }

            index = found + length;
        }

        return -1;
    }

    private static bool CanOpenSimpleDelimiter(string text, int start, int length)
    {
        char after = CharAtOrNull(text, start + length);
        return after != '\0' && !char.IsWhiteSpace(after);
    }

    private static bool CanCloseSimpleDelimiter(string text, int start, int length)
    {
        char before = CharAtOrNull(text, start - 1);
        return before != '\0' && !char.IsWhiteSpace(before);
    }

    private static bool CanOpenEmphasis(string text, int start, int length, char delimiter)
    {
        (bool leftFlanking, bool rightFlanking, char before, _) = GetFlanking(text, start, length);

        if (delimiter == '_')
        {
            return leftFlanking && (!rightFlanking || IsPunctuation(before));
        }

        return leftFlanking;
    }

    private static bool CanCloseEmphasis(string text, int start, int length, char delimiter)
    {
        (bool leftFlanking, bool rightFlanking, _, char after) = GetFlanking(text, start, length);

        if (delimiter == '_')
        {
            return rightFlanking && (!leftFlanking || IsPunctuation(after));
        }

        return rightFlanking;
    }

    private static (bool LeftFlanking, bool RightFlanking, char Before, char After) GetFlanking(string text, int start, int length)
    {
        char before = CharAtOrNull(text, start - 1);
        char after = CharAtOrNull(text, start + length);

        bool beforeWhitespace = before == '\0' || char.IsWhiteSpace(before);
        bool afterWhitespace = after == '\0' || char.IsWhiteSpace(after);
        bool beforePunctuation = before != '\0' && IsPunctuation(before);
        bool afterPunctuation = after != '\0' && IsPunctuation(after);

        bool leftFlanking = !afterWhitespace && (!afterPunctuation || beforeWhitespace || beforePunctuation);
        bool rightFlanking = !beforeWhitespace && (!beforePunctuation || afterWhitespace || afterPunctuation);

        return (leftFlanking, rightFlanking, before, after);
    }

    private static char CharAtOrNull(string text, int index)
    {
        return index >= 0 && index < text.Length ? text[index] : '\0';
    }

    private static bool IsPunctuation(char c)
    {
        if (c == '\0')
        {
            return false;
        }

        UnicodeCategory category = char.GetUnicodeCategory(c);
        return category is UnicodeCategory.ConnectorPunctuation
            or UnicodeCategory.DashPunctuation
            or UnicodeCategory.OpenPunctuation
            or UnicodeCategory.ClosePunctuation
            or UnicodeCategory.InitialQuotePunctuation
            or UnicodeCategory.FinalQuotePunctuation
            or UnicodeCategory.OtherPunctuation;
    }

    private static int CountRepeated(string text, int start, char c)
    {
        int count = 0;
        while (start + count < text.Length && text[start + count] == c)
        {
            count++;
        }

        return count;
    }

    private static bool StartsWith(string text, int index, string value)
    {
        return index + value.Length <= text.Length &&
               string.CompareOrdinal(text, index, value, 0, value.Length) == 0;
    }

    private static int FindNextSpecial(string text, int start)
    {
        int i = start;

        while (i < text.Length)
        {
            char c = text[i];

            if (c is '\\' or '`' or '*' or '_' or '[' or '<')
            {
                return i;
            }

            if (c == '~' && i + 1 < text.Length && text[i + 1] == '~')
            {
                return i;
            }

            if (c == '!' && i + 1 < text.Length && text[i + 1] == '[')
            {
                return i;
            }

            if ((c == 'h' || c == 'H') &&
                (StartsWith(text, i, "http://") || StartsWith(text, i, "https://")))
            {
                return i;
            }

            i++;
        }

        return text.Length;
    }

    private static void AddRun(List<InlineRun> runs, string text, InlineStyle style)
    {
        if (string.IsNullOrEmpty(text))
        {
            return;
        }

        AddSpecialRun(runs, new InlineRun(
            Text: WebUtility.HtmlDecode(text),
            Bold: style.Bold,
            Italic: style.Italic,
            Code: style.Code,
            Strike: style.Strike,
            Link: false,
            Url: null));
    }

    private static void AddLinkRun(List<InlineRun> runs, string label, string url, InlineStyle style)
    {
        string cleanUrl = WebUtility.HtmlDecode(url.Trim()) ?? string.Empty;
        string cleanLabel = WebUtility.HtmlDecode(label.Trim()) ?? string.Empty;

        // Markdown links render only their anchor text. The destination URL is stored
        // separately on the InlineRun and is used later to create the clickable PDF
        // URI annotation. For bare URLs and autolinks, the label and URL are the same,
        // so the URL remains visible because it is the intended anchor text.
        string visibleText = string.IsNullOrWhiteSpace(cleanLabel) ? cleanUrl : cleanLabel;

        AddSpecialRun(runs, new InlineRun(
            Text: visibleText,
            Bold: style.Bold,
            Italic: style.Italic,
            Code: style.Code,
            Strike: style.Strike,
            Link: true,
            Url: cleanUrl));
    }

    private static void AddSpecialRun(List<InlineRun> runs, InlineRun run)
    {
        if (string.IsNullOrEmpty(run.Text))
        {
            return;
        }

        if (runs.Count > 0 && runs[^1].HasSameStyle(run))
        {
            InlineRun previous = runs[^1];
            runs[^1] = previous with { Text = previous.Text + run.Text };
        }
        else
        {
            runs.Add(run);
        }
    }

    private static string NormalizeReferenceLabel(string value)
    {
        return Regex.Replace(value.Trim(), @"\s+", " ");
    }

    private readonly record struct InlineStyle(
        bool Bold = false,
        bool Italic = false,
        bool Code = false,
        bool Strike = false);
}

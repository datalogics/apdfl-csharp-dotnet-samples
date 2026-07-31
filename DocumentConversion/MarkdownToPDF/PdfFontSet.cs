using System.Diagnostics.CodeAnalysis;
using System.Globalization;
using System.Net;
using System.Text;
using System.Text.RegularExpressions;
using Datalogics.PDFL;

namespace MarkdownToPDF;

internal sealed class PdfFontSet : IDisposable
{
    private readonly PdfFontFamilySet _body;
    private readonly PdfFontFamilySet _heading;
    private readonly List<string> _fallbackFontNames;
    private readonly Dictionary<string, Font?> _fallbackCache = new(StringComparer.OrdinalIgnoreCase);

    public PdfFontSet(string bodyFamily, string headingFamily, string codeFamily, IReadOnlyList<string> fallbackFontNames)
    {
        _body = PdfFontFamilySet.Create(bodyFamily);
        _heading = PdfFontFamilySet.Create(headingFamily);
        Code = CreateCodeFont(codeFamily);
        _fallbackFontNames = fallbackFontNames.Distinct(StringComparer.OrdinalIgnoreCase).ToList();
    }

    public void Dispose()
    {
        HashSet<Font> fonts = new();
        _body.CollectInto(fonts);
        _heading.CollectInto(fonts);
        fonts.Add(Code);

        foreach (Font? fallback in _fallbackCache.Values)
        {
            if (fallback is not null)
            {
                fonts.Add(fallback);
            }
        }

        foreach (Font font in fonts)
        {
            try
            {
                font.Dispose();
            }
            catch
            {
                // Ignore disposal failures for shared or built-in font handles.
            }
        }

        _fallbackCache.Clear();
    }

    public Font Regular => _body.Regular;

    public Font Bold => _body.Bold;

    public Font Italic => _body.Italic;

    public Font BoldItalic => _body.BoldItalic;

    public Font Code { get; }

    public Font Resolve(InlineRun run, PdfFontRole role = PdfFontRole.Body)
    {
        return Resolve(run, role, null);
    }

    public Font Resolve(InlineRun run, PdfFontRole role, char? character)
    {
        if (character.HasValue && IsCjkCharacter(character.Value))
        {
            Font? fallback = ResolveFirstAvailableFallbackFont(GetCjkFallbackCandidates());
            if (fallback is not null)
            {
                return fallback;
            }
        }

        if (character.HasValue && IsNonLatinFallbackCharacter(character.Value))
        {
            Font? fallback = ResolveFirstAvailableFallbackFont(GetGeneralUnicodeFallbackCandidates());
            if (fallback is not null)
            {
                return fallback;
            }
        }

        if (run.Code || role == PdfFontRole.Code)
        {
            return Code;
        }

        PdfFontFamilySet family = role == PdfFontRole.Heading ? _heading : _body;

        if (run.Bold && run.Italic)
        {
            return family.BoldItalic;
        }

        if (run.Bold)
        {
            return family.Bold;
        }

        if (run.Italic)
        {
            return family.Italic;
        }

        return family.Regular;
    }

    public IReadOnlyList<string> FallbackFontNames => _fallbackFontNames;

    private Font? ResolveFirstAvailableFallbackFont(IEnumerable<string> fontNames)
    {
        foreach (string name in fontNames)
        {
            Font? font = GetOrCreateOptionalFont(name);
            if (font is not null)
            {
                return font;
            }
        }

        return null;
    }

    private IEnumerable<string> GetCjkFallbackCandidates()
    {
        foreach (string name in _fallbackFontNames.Where(IsLikelyCjkFontName))
        {
            yield return name;
        }

        foreach (string name in _fallbackFontNames)
        {
            yield return name;
        }
    }

    private IEnumerable<string> GetGeneralUnicodeFallbackCandidates()
    {
        foreach (string name in new[] { "Arial", "Times New Roman", "Calibri", "Segoe UI", "Verdana", "DejaVu Sans", "Noto Sans" })
        {
            yield return name;
        }

        foreach (string name in _fallbackFontNames)
        {
            yield return name;
        }
    }

    private static bool IsLikelyCjkFontName(string name)
    {
        string lower = name.ToLowerInvariant();
        return lower.Contains("cjk", StringComparison.Ordinal) ||
               lower.Contains("yahei", StringComparison.Ordinal) ||
               lower.Contains("simsun", StringComparison.Ordinal) ||
               lower.Contains("jhenghei", StringComparison.Ordinal) ||
               lower.Contains("malgun", StringComparison.Ordinal) ||
               lower.Contains("gothic", StringComparison.Ordinal) ||
               lower.Contains("japanese", StringComparison.Ordinal) ||
               lower.Contains("korean", StringComparison.Ordinal) ||
               lower.Contains("chinese", StringComparison.Ordinal) ||
               lower.Contains("unicode", StringComparison.Ordinal);
    }

    private Font? GetOrCreateOptionalFont(string familyOrName)
    {
        if (_fallbackCache.TryGetValue(familyOrName, out Font? cached))
        {
            return cached;
        }

        string fontName = familyOrName;
        if (PdfFontCatalog.TryNormalizeFamily(familyOrName, out string? familyName))
        {
            fontName = PdfFontCatalog.Resolve(familyName).RegularFontName;
        }

        try
        {
            Font font = new Font(fontName, FontCreateFlags.Embedded | FontCreateFlags.Subset);
            _fallbackCache[familyOrName] = font;
            return font;
        }
        catch (Exception ex) when (ex is LibraryException or ApplicationException)
        {
            try
            {
                Font font = new Font(fontName, FontCreateFlags.Subset);
                _fallbackCache[familyOrName] = font;
                return font;
            }
            catch (Exception subsetException) when (subsetException is LibraryException or ApplicationException)
            {
                _fallbackCache[familyOrName] = null;
                return null;
            }
        }
    }

    private static Font CreateCodeFont(string family)
    {
        PdfFontFamilyDefinition definition = PdfFontCatalog.Resolve(family);
        return CreateFont(definition.RegularFontName);
    }

    private static Font CreateFontOrFallback(string name, Font fallback)
    {
        try
        {
            return new Font(name, FontCreateFlags.Embedded | FontCreateFlags.Subset);
        }
        catch (Exception ex) when (ex is LibraryException or ApplicationException)
        {
            try
            {
                return new Font(name, FontCreateFlags.Subset);
            }
            catch (Exception subsetException) when (subsetException is LibraryException or ApplicationException)
            {
                return fallback;
            }
        }
    }

    private static Font CreateFont(string name)
    {
        try
        {
            return new Font(name, FontCreateFlags.Embedded | FontCreateFlags.Subset);
        }
        catch (Exception embeddedException) when (embeddedException is LibraryException or ApplicationException)
        {
            try
            {
                return new Font(name, FontCreateFlags.Subset);
            }
            catch (Exception subsetException) when (subsetException is LibraryException or ApplicationException)
            {
                throw new CommandLineException(
                    $"The font \"{name}\" could not be created by APDFL. " +
                    "Use Times, Helvetica, or Courier for the most portable sample behavior, " +
                    "or install/configure the requested font so APDFL can find it.",
                    new AggregateException(embeddedException, subsetException));
            }
        }
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

    private static bool IsNonLatinFallbackCharacter(char c)
    {
        return (c >= '\u0400' && c <= '\u052F') || // Cyrillic
               (c >= '\u0370' && c <= '\u03FF');   // Greek
    }

    private sealed class PdfFontFamilySet
    {
        private PdfFontFamilySet(Font regular, Font bold, Font italic, Font boldItalic)
        {
            Regular = regular;
            Bold = bold;
            Italic = italic;
            BoldItalic = boldItalic;
        }

        public Font Regular { get; }

        public Font Bold { get; }

        public Font Italic { get; }

        public Font BoldItalic { get; }

        public void CollectInto(HashSet<Font> fonts)
        {
            fonts.Add(Regular);
            fonts.Add(Bold);
            fonts.Add(Italic);
            fonts.Add(BoldItalic);
        }

        public static PdfFontFamilySet Create(string family)
        {
            PdfFontFamilyDefinition definition = PdfFontCatalog.Resolve(family);
            Font regular = CreateFont(definition.RegularFontName);
            Font bold = CreateFontOrFallback(definition.BoldFontName, regular);
            Font italic = CreateFontOrFallback(definition.ItalicFontName, regular);
            Font boldItalic = CreateFontOrFallback(definition.BoldItalicFontName, bold);

            return new PdfFontFamilySet(regular, bold, italic, boldItalic);
        }
    }
}

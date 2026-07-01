using System.Diagnostics.CodeAnalysis;
using System.Globalization;
using System.Net;
using System.Text;
using System.Text.RegularExpressions;
using Datalogics.PDFL;

namespace MarkdownToPDF;

internal enum PdfFontRole
{
    Body,
    Heading,
    Code
}

internal sealed record PdfFontFamilyDefinition(
    string CanonicalName,
    string RegularFontName,
    string BoldFontName,
    string ItalicFontName,
    string BoldItalicFontName);

internal static class PdfFontCatalog
{
    public static IReadOnlyList<string> CoreFamilyNames { get; } = new[]
    {
        "Times",
        "Helvetica",
        "Courier"
    };

    private static readonly Dictionary<string, PdfFontFamilyDefinition> Families = CreateFamilies();

    public static IReadOnlyList<string> SupportedFamilyNames { get; } = Families
        .Values
        .Select(family => family.CanonicalName)
        .Distinct(StringComparer.OrdinalIgnoreCase)
        .OrderBy(name => name, StringComparer.OrdinalIgnoreCase)
        .ToList();

    public static string SupportedFamilyList => string.Join(", ", SupportedFamilyNames);

    public static string NormalizeFamily(string value)
    {
        if (TryNormalizeFamily(value, out string? family))
        {
            return family;
        }

        throw new CommandLineException(
            $"Unknown font family: {value}. Run --list-font-families to see recognized names. " +
            "Only fonts available to APDFL on this machine can be used successfully at runtime.");
    }

    public static bool TryNormalizeFamily(string value, [NotNullWhen(true)] out string? family)
    {
        family = null;

        if (string.IsNullOrWhiteSpace(value))
        {
            return false;
        }

        string key = NormalizeKey(value);
        if (Families.TryGetValue(key, out PdfFontFamilyDefinition? definition))
        {
            family = definition.CanonicalName;
            return true;
        }

        return false;
    }

    public static PdfFontFamilyDefinition Resolve(string family)
    {
        string key = NormalizeKey(family);
        if (Families.TryGetValue(key, out PdfFontFamilyDefinition? definition))
        {
            return definition;
        }

        throw new CommandLineException($"Unknown font family: {family}.");
    }

    public static string GetDisplayFamilyName(string family)
    {
        if (string.IsNullOrWhiteSpace(family))
        {
            return family;
        }

        StringBuilder builder = new();
        for (int i = 0; i < family.Length; i++)
        {
            char current = family[i];
            if (i > 0 && ShouldInsertSpace(family, i))
            {
                builder.Append(' ');
            }

            builder.Append(current);
        }

        return builder.ToString();
    }

    private static Dictionary<string, PdfFontFamilyDefinition> CreateFamilies()
    {
        Dictionary<string, PdfFontFamilyDefinition> families = new(StringComparer.OrdinalIgnoreCase);

        AddFamily(families, "Times", "Times-Roman", "Times-Bold", "Times-Italic", "Times-BoldItalic", "timesroman", "serif");
        AddFamily(families, "Helvetica", "Helvetica", "Helvetica-Bold", "Helvetica-Oblique", "Helvetica-BoldOblique", "sans", "sansserif");
        AddFamily(families, "Courier", "Courier", "Courier-Bold", "Courier-Oblique", "Courier-BoldOblique", "mono", "monospace");
        AddDiscoveredFamilies(families);

        return families;
    }

    private static void AddDiscoveredFamilies(Dictionary<string, PdfFontFamilyDefinition> families)
    {
        Dictionary<string, DiscoveredFontFamilyBuilder> builders = new(StringComparer.OrdinalIgnoreCase);

        foreach (Font fontInfo in Font.FontList)
        {
            string faceName = fontInfo.Name;
            string familyName = FamilyNameFromPostScriptName(faceName);
            if (string.IsNullOrWhiteSpace(familyName))
            {
                continue;
            }

            if (!builders.TryGetValue(familyName, out DiscoveredFontFamilyBuilder? builder))
            {
                builder = new DiscoveredFontFamilyBuilder(familyName);
                builders[familyName] = builder;
            }

            builder.AddFace(faceName);
        }

        foreach (DiscoveredFontFamilyBuilder builder in builders.Values.OrderBy(item => item.CanonicalName, StringComparer.OrdinalIgnoreCase))
        {
            PdfFontFamilyDefinition definition = builder.Build();
            string key = NormalizeKey(definition.CanonicalName);
            if (!families.ContainsKey(key))
            {
                AddFamily(
                    families,
                    definition.CanonicalName,
                    definition.RegularFontName,
                    definition.BoldFontName,
                    definition.ItalicFontName,
                    definition.BoldItalicFontName);
            }
        }
    }

    private static void AddFamily(
        Dictionary<string, PdfFontFamilyDefinition> families,
        string canonicalName,
        string regular,
        string bold,
        string italic,
        string boldItalic,
        params string[] aliases)
    {
        PdfFontFamilyDefinition definition = new(canonicalName, regular, bold, italic, boldItalic);
        families[NormalizeKey(canonicalName)] = definition;

        foreach (string alias in aliases)
        {
            families[NormalizeKey(alias)] = definition;
        }
    }

    private static string NormalizeKey(string value)
    {
        StringBuilder builder = new();
        foreach (char c in value)
        {
            if (char.IsLetterOrDigit(c))
            {
                builder.Append(char.ToLowerInvariant(c));
            }
        }

        return builder.ToString();
    }

    private static string FamilyNameFromPostScriptName(string postScriptName)
    {
        string name = postScriptName.Trim();
        if (string.Equals(name, "Times-Roman", StringComparison.OrdinalIgnoreCase))
        {
            return "Times";
        }

        string[] adornments = { "PSMT", "PS", "MT" };
        string[] styleSuffixes = { "-BoldOblique", "-BoldItalic", "-Oblique", "-Italic", "-Bold", "-Regular" };

        bool changed = true;
        while (changed)
        {
            changed = false;

            foreach (string adornment in adornments)
            {
                if (name.Length > adornment.Length && name.EndsWith(adornment, StringComparison.OrdinalIgnoreCase))
                {
                    name = name[..^adornment.Length];
                    changed = true;
                }
            }

            foreach (string style in styleSuffixes)
            {
                if (name.EndsWith(style, StringComparison.OrdinalIgnoreCase))
                {
                    name = name[..^style.Length];
                    changed = true;
                }
            }
        }

        return name.Replace('-', ' ').Trim();
    }

    private static bool EndsWithAny(string value, params string[] suffixes)
    {
        foreach (string suffix in suffixes)
        {
            if (value.EndsWith(suffix, StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }
        }

        return false;
    }

    private static bool ShouldInsertSpace(string value, int index)
    {
        char previous = value[index - 1];
        char current = value[index];
        char? next = index + 1 < value.Length ? value[index + 1] : null;

        if (char.IsLower(previous) && char.IsUpper(current))
        {
            return true;
        }

        if (char.IsUpper(previous) && char.IsUpper(current) && next.HasValue && char.IsLower(next.Value))
        {
            return true;
        }

        if (char.IsLetter(previous) && char.IsDigit(current))
        {
            return true;
        }

        if (char.IsDigit(previous) && char.IsLetter(current))
        {
            return true;
        }

        return false;
    }

    private sealed class DiscoveredFontFamilyBuilder
    {
        public DiscoveredFontFamilyBuilder(string canonicalName)
        {
            CanonicalName = canonicalName;
        }

        public string CanonicalName { get; }

        private string? RegularFace { get; set; }

        private string? BoldFace { get; set; }

        private string? ItalicFace { get; set; }

        private string? BoldItalicFace { get; set; }

        public void AddFace(string faceName)
        {
            if (EndsWithAny(faceName, "-BoldOblique", "-BoldItalic", " Bold Oblique", " Bold Italic"))
            {
                BoldItalicFace ??= faceName;
                return;
            }

            if (EndsWithAny(faceName, "-Oblique", "-Italic", " Oblique", " Italic"))
            {
                ItalicFace ??= faceName;
                return;
            }

            if (EndsWithAny(faceName, "-Bold", " Bold"))
            {
                BoldFace ??= faceName;
                return;
            }

            RegularFace ??= faceName;
        }

        public PdfFontFamilyDefinition Build()
        {
            string regular = RegularFace ?? BoldFace ?? ItalicFace ?? BoldItalicFace ?? CanonicalName;
            string bold = BoldFace ?? regular;
            string italic = ItalicFace ?? regular;
            string boldItalic = BoldItalicFace ?? bold;

            return new PdfFontFamilyDefinition(CanonicalName, regular, bold, italic, boldItalic);
        }
    }
}

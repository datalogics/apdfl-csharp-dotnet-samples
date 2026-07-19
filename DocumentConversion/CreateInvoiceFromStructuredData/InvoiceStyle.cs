namespace CreateInvoiceFromStructuredData;

internal sealed class InvoiceStyle
{
    public string BodyFont { get; init; } = "Helvetica";

    public string BoldFont { get; init; } = "Helvetica-Bold";

    public double BodyFontSize { get; init; } = 9.5;

    public double SmallFontSize { get; init; } = 8.0;

    public double HeadingFontSize { get; init; } = 20.0;

    public double TableHeaderFontSize { get; init; } = 8.5;

    public double PageWidth { get; init; } = 612.0;

    public double PageHeight { get; init; } = 792.0;

    public double Margin { get; init; } = 54.0;

    public double LogoMaxWidth { get; init; } = 172.0;

    public double LogoMaxHeight { get; init; } = 54.0;

    public string PrimaryColor { get; init; } = "#24536A";

    public string AccentColor { get; init; } = "#E8F1F4";

    public string TextColor { get; init; } = "#222222";

    public string MutedTextColor { get; init; } = "#666666";

    public string BorderColor { get; init; } = "#B8C7CE";
}

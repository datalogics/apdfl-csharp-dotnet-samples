using Datalogics.PDFL;

namespace CreateInvoiceFromStructuredData;

internal sealed class PdfLayoutContext
{
    private readonly Document _document;
    private readonly Rect _pageRect;

    public PdfLayoutContext(Document document, Rect pageRect, double topY)
    {
        _document = document;
        _pageRect = pageRect;
        CurrentPage = _document.CreatePage(Document.BeforeFirstPage, _pageRect);
        Y = topY;
    }

    public Page CurrentPage { get; private set; }

    public double Y { get; set; }

    public int PageCount { get; private set; } = 1;

    public void MarkPageDirty()
    {
        CurrentPage.UpdateContent();
    }

    public void AddPage(double topY)
    {
        CurrentPage.UpdateContent();
        CurrentPage = _document.CreatePage(PageCount - 1, _pageRect);
        PageCount++;
        Y = topY;
    }
}

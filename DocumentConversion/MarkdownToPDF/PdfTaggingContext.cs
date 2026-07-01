using System.Diagnostics.CodeAnalysis;
using System.Globalization;
using System.Net;
using System.Text;
using System.Text.RegularExpressions;
using Datalogics.PDFL;

namespace MarkdownToPdf;

internal sealed class PdfTaggingContext
{
    private readonly Document _document;
    private readonly PDFDict _structTreeRoot;
    private readonly PDFArray _rootKids;
    private readonly NumberTree _parentTree;
    private readonly Dictionary<int, PageTagState> _pageStates = new();

    private int _nextStructParentKey;

    public PdfTaggingContext(Document document, string language)
    {
        _document = document;

        PDFDict markInfo = new PDFDict(_document, false);
        markInfo.Put("Marked", Bool(true));
        markInfo.Put("Suspects", Bool(false));
        _document.Root.Put("MarkInfo", markInfo);

        if (!string.IsNullOrWhiteSpace(language))
        {
            _document.Root.Put("Lang", Str(language));
        }

        _structTreeRoot = new PDFDict(_document, true);
        _structTreeRoot.Put("Type", Name("StructTreeRoot"));

        _rootKids = new PDFArray(_document, false);
        _structTreeRoot.Put("K", _rootKids);

        _parentTree = new NumberTree(_document);
        _structTreeRoot.Put("ParentTree", _parentTree.PDFDict);
        _structTreeRoot.Put("ParentTreeNextKey", Int(0));

        _document.Root.Put("StructTreeRoot", _structTreeRoot);

        DocumentElement = CreateElement("Document", parent: null);
    }

    public PdfTaggedElement DocumentElement { get; }

    public PdfTaggedElement CreateElement(string tagName, PdfTaggedElement? parent)
    {
        PDFDict element = new PDFDict(_document, true);
        PDFArray kids = new PDFArray(_document, false);

        element.Put("Type", Name("StructElem"));
        element.Put("S", Name(tagName));
        element.Put("P", parent?.Dictionary ?? _structTreeRoot);
        element.Put("K", kids);

        if (parent is null)
        {
            _rootKids.Add(element);
        }
        else
        {
            parent.Kids.Add(element);
        }

        return new PdfTaggedElement(tagName, element, kids);
    }

    public void SetAltText(PdfTaggedElement element, string altText)
    {
        if (!string.IsNullOrWhiteSpace(altText))
        {
            element.Dictionary.Put("Alt", Str(altText));
        }
    }

    public void SetActualText(PdfTaggedElement element, string actualText)
    {
        if (!string.IsNullOrWhiteSpace(actualText))
        {
            element.Dictionary.Put("ActualText", Str(actualText));
        }
    }

    public void AddTaggedElement(PdfLayoutContext layout, Element element, PdfTaggedElement owner)
    {
        Page page = layout.CurrentPage;
        PageTagState pageState = GetOrCreatePageState(page);

        int mcid = pageState.NextMcid++;

        PDFDict propertyList = new PDFDict(_document, false);
        propertyList.Put("MCID", Int(mcid));

        Container container = new Container(owner.TagName, propertyList, isInline: true)
        {
            Content = new Content(element)
        };

        page.Content.AddElement(container);

        PDFDict markedContentReference = new PDFDict(_document, false);
        markedContentReference.Put("Type", Name("MCR"));
        markedContentReference.Put("Pg", page.PDFDict);
        markedContentReference.Put("MCID", Int(mcid));

        owner.Kids.Add(markedContentReference);

        pageState.ParentArray.Add(owner.Dictionary);

        layout.MarkPageDirty();
    }

    public void AddArtifactElement(PdfLayoutContext layout, Element element)
    {
        PDFDict artifactProperties = new PDFDict(_document, false);
        artifactProperties.Put("Type", Name("Layout"));

        Container artifact = new Container("Artifact", artifactProperties, isInline: true)
        {
            Content = new Content(element)
        };

        layout.CurrentPage.Content.AddElement(artifact);
        layout.MarkPageDirty();
    }

    public void AddLinkAnnotation(PdfLayoutContext layout, string uri, Rect rect, PdfTaggedElement owner, string contents)
    {
        LinkAnnotation linkAnnotation = new LinkAnnotation(layout.CurrentPage, rect)
        {
            Action = new URIAction(uri, false),
            BorderStyleWidth = 0.0,
            Highlight = HighlightStyle.Invert,
            Contents = contents
        };

        AddObjectReference(linkAnnotation.PDFDict, owner);
    }

    public void Finish()
    {
        _structTreeRoot.Put("ParentTreeNextKey", Int(_nextStructParentKey));
    }

    private void AddObjectReference(PDFDict objectDictionary, PdfTaggedElement owner)
    {
        int structParentKey = _nextStructParentKey++;
        objectDictionary.Put("StructParent", Int(structParentKey));

        PDFDict objectReference = new PDFDict(_document, false);
        objectReference.Put("Type", Name("OBJR"));
        objectReference.Put("Obj", objectDictionary);

        owner.Kids.Add(objectReference);
        _parentTree.Put(structParentKey, owner.Dictionary);
    }

    private PageTagState GetOrCreatePageState(Page page)
    {
        int pageNumber = page.PageNumber;

        if (_pageStates.TryGetValue(pageNumber, out PageTagState? existing))
        {
            return existing;
        }

        int structParentsKey = _nextStructParentKey++;
        PDFArray parentArray = new PDFArray(_document, true);

        page.PDFDict.Put("StructParents", Int(structParentsKey));
        page.PDFDict.Put("Tabs", Name("S"));

        _parentTree.Put(structParentsKey, parentArray);

        PageTagState created = new PageTagState(structParentsKey, parentArray);
        _pageStates.Add(pageNumber, created);

        return created;
    }

    private PDFName Name(string value)
    {
        return new PDFName(value, _document, false);
    }

    private PDFInteger Int(int value)
    {
        return new PDFInteger(value, _document, false);
    }

    private PDFBoolean Bool(bool value)
    {
        return new PDFBoolean(value, _document, false);
    }

    private PDFString Str(string value)
    {
        return new PDFString(value, _document, false, storedAsHex: false);
    }

    private sealed class PageTagState
    {
        public PageTagState(int structParentsKey, PDFArray parentArray)
        {
            StructParentsKey = structParentsKey;
            ParentArray = parentArray;
        }

        public int StructParentsKey { get; }

        public int NextMcid { get; set; }

        public PDFArray ParentArray { get; }
    }
}

internal sealed record PdfTaggedElement(
    string TagName,
    PDFDict Dictionary,
    PDFArray Kids);

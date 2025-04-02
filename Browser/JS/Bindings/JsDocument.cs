using HtmlDocument = HtmlAgilityPack.HtmlDocument;

namespace Browser.JS.Bindings;

public class JsDocument
{
    private HtmlDocument _htmlDocument;

    public JsDocument(HtmlDocument htmlDocument)
    {
        _htmlDocument = htmlDocument;
    }

    public JsHtmlNode GetElementById(string id)
    {
        return new JsHtmlNode(_htmlDocument.GetElementbyId(id));
    }
}
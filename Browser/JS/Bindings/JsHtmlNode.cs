using HtmlAgilityPack;

namespace Browser.JS.Bindings;

public class JsHtmlNode
{
    private HtmlNode _htmlNode;

    public JsHtmlNode(HtmlNode htmlNode)
    {
        _htmlNode = htmlNode;
    }

    public string innerHTML
    {
        get
        {
            return _htmlNode.InnerHtml; // Получаем HTML-содержимое узла
        }
        set
        {
            _htmlNode.InnerHtml = value; // Устанавливаем новое содержимое
        }
    }
}
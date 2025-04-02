using Browser.JS.Bindings;

namespace Browser.JS;
using HtmlDocument = HtmlAgilityPack.HtmlDocument;
using Jint;

public class JavaScriptEngine
{
    private Engine _engine;
    private HtmlDocument _htmlDocument;

    public JavaScriptEngine(HtmlDocument htmlDocument)
    {
        _engine = new Engine();
        _htmlDocument = htmlDocument;
        _engine.SetValue("document", new JsDocument(_htmlDocument));
    }

    public void ExecuteScript(string script)
    {
        try
        {
            _engine.Execute(script);
        }
        catch (Exception ex)
        {
            Console.WriteLine($"JavaScript Error: {ex.Message}");
        }
    }
}
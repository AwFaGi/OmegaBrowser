using System.Text.RegularExpressions;
using Browser.CSS;
using Browser.DOM;
using Browser.JS;
using Browser.Networking;
using Browser.Render;
using HtmlAgilityPack;
using SkiaSharp.Views.Desktop;
using HtmlDocument = HtmlAgilityPack.HtmlDocument;

namespace Browser.Management;

public class Tab
{
    public string location { get; }
    public HtmlDocument document { get; }
    public CssHtmlDocument cssDocument { get; }
    
    public JavaScriptEngine jsExecutor { get; }
    public Layout layout { get; private set; }
    public List<Resource> resources { get; set; }
    public Resource mainResource { get; }
    public Browser owner { get; }

    private ObjectRenderer _renderer;
    
    public SKControl proxyPanel { get; private set; }

    public Tab(Resource mainResource, Browser owner, SKControl renderPanel)
    {
        
        this.mainResource = mainResource;
        this.owner = owner;

        location = mainResource.path;

        document = new HtmlDocument();
        document.Load(mainResource.localPath);

        resources = ResourceUtil.FillResourcesWithLocation(ResourceUtil.GetResources(document), location);
        foreach (var t in resources)
        {
            var resource = t;
            try
            {
                owner.resourceManager.GetResource(ref resource);
            }
            catch (Exception e)
            {
                Console.WriteLine($"Bad resource: {t}");
            }
        }
        
        jsExecutor = new JavaScriptEngine(document);
        HandleJs();
        
        cssDocument = new CssHtmlDocument(document);
        HandleCss();
        
        layout = new Layout(owner.Options.viewport, cssDocument, this);
        // foreach (var obj in layout.MakeRenderObjects(
        //              document.DocumentNode.SelectSingleNode("//body"), null)
        //          )
        // {
        //     Console.WriteLine(obj);
        // }
        proxyPanel = new SKControl()
        {
            Width = renderPanel.Width,
            Height = renderPanel.Height,
            Top = 0
        };
        _renderer = new ObjectRenderer(this, layout, proxyPanel);
        
    }

    public void HandleJs()
    {
        var jsNodes = document.DocumentNode.SelectNodes("//script[@src]|//script[not(@src)]");
        if (jsNodes != null)
        {
            var jsToExecute = new List<string>();
            var jsThreads = new List<JsThread>();
            var counter = 0;
            foreach (var node in jsNodes)
            {
                // внешний скрипт
                if (node.Attributes["src"] != null)
                {
                    string scriptSrc = node.GetAttributeValue("src", "");
                    var fileName = resources.First(res => scriptSrc.Equals(res.path)).localPath;
                    if (fileName != null)
                    {
                        var thread = new JsThread(counter, fileName);
                        thread.Start();
                        jsThreads.Add(thread);
                        jsToExecute.Add("");
                    }
                }
                else
                {
                    // Встроенный скрипт (без src)
                    string inlineScript = node.InnerHtml.Trim();
                    jsToExecute.Add(inlineScript);
                }

                counter += 1;
            }

            foreach (var thread in jsThreads)
            {
                thread.Join();
                jsToExecute[thread.Number] = thread.Content;
            }

            foreach (var jsCode in jsToExecute)
            {
                jsExecutor.ExecuteScript(jsCode);
            }
        }
    }

    public void HandleCss()
    {
        var cssNodes = document.DocumentNode.SelectNodes("//link[@rel='stylesheet']|//style");
        if (cssNodes != null)
        {
            foreach (var node in cssNodes)
            {

                CSSGlobalMap map;
                
                if (node.Name == "link")
                {
                    var link = node.Attributes.First(attribute => (attribute.Name == "href" || attribute.Name == "data-href"));
                    if (link == null)
                    {
                        continue;
                    }
                    var fileName = resources.First(res => link.Value.Equals(res.path)).localPath;
                    if (fileName == null)
                    {
                        continue;
                    }
                    map = CssParser.ParseFile(fileName);
                }
                else
                {
                    var cssString = node.InnerText;
                    cssString = cssString.Trim();
                    if (cssString.Equals(""))
                    {
                        continue;
                    }
                    map = CssParser.ParseString(cssString);
                }
                
                foreach (var kvp in map.getMap())
                {
                    var preprocessed = kvp.Key.ToLower().Split(",");
                    var filtered = preprocessed.Where(rule => !rule.Contains(':')).ToList();
                    if (filtered.Count == 0) continue;
                    var selector = string.Join(",", filtered);
                    IList<HtmlNode> selectedNodes;
                    try
                    {
                        selectedNodes = document.DocumentNode.QuerySelectorAll(selector);
                    }
                    catch (Exception e)
                    {
                        //bad css selector
                        continue;
                    }
                    if (selectedNodes == null)
                    {
                        continue;
                    }

                    foreach (var selectedNode in selectedNodes)
                    {
                        foreach (var attrKvp in kvp.Value.getMap())
                        {
                            cssDocument.GetMap()[selectedNode].getMap()[attrKvp.Key.ToLower()] = attrKvp.Value;
                            if (attrKvp.Key.ToLower() == "background-image")
                            {
                                string pattern = @"url\(([^)]+)\)";

                                Regex regex = new Regex(pattern);
                                Match match = regex.Match(attrKvp.Value);

                                if (match.Success)
                                {
                                    string url = match.Groups[1].Value;
                                    Resource res = new Resource(url, Resource.ResourceType.Img);
                                    
                                    try
                                    {
                                        owner.resourceManager.GetResource(ref res);
                                    }
                                    catch (Exception e)
                                    {
                                        Console.WriteLine($"Bad resource: {url}");
                                    }
                                }
                                else
                                {
                                    Console.WriteLine("URL not found in the input text.");
                                }
                            }
                        }
                    }
                }
            }
        }
        
        var allNodes = document.DocumentNode.Descendants();
        if (allNodes == null)
        {
            return;
        }

        foreach (var node in allNodes)
        {
            foreach (var attribute in node.Attributes)
            {
                if (!attribute.Name.Equals("style")) continue;
                var map = CssParser.ParseInline(attribute.Value);

                foreach (var kvp in map.getMap())
                {
                    cssDocument.GetMap()[node].getMap()[kvp.Key.ToLower()] = kvp.Value;
                    if (kvp.Key.ToLower() == "background-image")
                    {
                        string pattern = @"url\(([^)]+)\)";

                        Regex regex = new Regex(pattern);
                        Match match = regex.Match(kvp.Value);

                        if (match.Success)
                        {
                            string url = match.Groups[1].Value;
                            Resource res = new Resource(url, Resource.ResourceType.Img);
                                    
                            try
                            {
                                owner.resourceManager.GetResource(ref res);
                            }
                            catch (Exception e)
                            {
                                Console.WriteLine($"Bad resource: {url}");
                            }
                        }
                        else
                        {
                            Console.WriteLine("URL not found in the input text.");
                        }
                    }
                }
            }
            
        }
        
    }
    
}
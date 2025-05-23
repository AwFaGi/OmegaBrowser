using System.Text.RegularExpressions;
using Browser.DOM;
using Browser.Management;
using Browser.Networking;
using HtmlAgilityPack;
using SkiaSharp;

namespace Browser.Render;

public class Layout
{

    private int viewport;
    // public static readonly SKPaint paint = new()
    // {
    //     Color = SKColors.Black,
    //     TextSize = 16,
    //     IsAntialias = true,
    //     Typeface = SKTypeface.FromFamilyName("Times New Roman")
    // };
    public CssHtmlDocument document { get; set; }
    public Tab tab { get; set; }



    public Layout(int viewport, CssHtmlDocument document, Tab tab)
    {
        this.viewport = viewport;
        this.document = document;
        this.tab = tab;
    }

    public List<RenderObject> MakeRenderObjects(HtmlNode node, RenderObject parentObject, RenderObject? sibling = null)
    {
        var list = new List<RenderObject>();

        if (node.Name == "#text")
        {
            var text = node.InnerText;
            text = text.Replace("&nbsp;", " ")
                .Replace("&gt;", ">")
                .Replace("&lt;", "<")
                .Replace("&mdash;", "—")
                .Replace("&aring;", "å")
                .Replace("&copy;", "\u00a9")
                .Replace("&reg;", "\u00ae")
                .Replace("&#8220;", "“")
                .Replace("&#8221;", "”")
                .Replace("&Ccedil;", "Ç");

            if (string.IsNullOrEmpty(text.Trim()))
            {
                return list;
            }
            
            for (int i = 0; i < text.Length; i++)
            {
                if (char.IsWhiteSpace(text[i]))
                {
                    text = text.Replace(text[i].ToString(), " ");
                }
            }
        
            while (text.Contains("  "))
            {
                text = text.Replace("  ", " ");
            }

            var elem = MakeText(node, parentObject, text, sibling);
            list.AddRange(elem);

            var pLink = node.ParentNode;
            while (pLink != null && pLink.Name != "html")
            {
                document.GetMap()[pLink].getMap().TryGetValue("color", out var color);
                if (!string.IsNullOrEmpty(color))
                {
                    foreach (var textObject in elem)
                    {
                        textObject.Map.getMap()["color"] = color;
                    }
                    break;
                }

                pLink = pLink.ParentNode;
            }
            
            pLink = node.ParentNode;
            while (pLink != null && pLink.Name != "html")
            {
                document.GetMap()[pLink].getMap().TryGetValue("text-decoration", out var textDecor);
                if (!string.IsNullOrEmpty(textDecor))
                {
                    foreach (var textObject in elem)
                    {
                        textObject.Map.getMap()["text-decoration"] = textDecor;
                    }
                    break;
                }

                pLink = pLink.ParentNode;
            }
            
            return list;
        }

        if (node.Name == "#comment")
        {
            return list;
        }
        
        if (node.Name == "br")
        {
            list.AddRange(MakeText(node, parentObject, "\n", sibling));
            return list;
        }

        if (node.Name == "img")
        {
            try
            {
                var elem = MakeImage(node, node.Attributes["src"].Value, parentObject, sibling);
                list.Add(elem);
                return list;
            }
            catch (Exception)
            {
                if (node.Attributes.Contains("alt"))
                {
                    list.AddRange(MakeText(node, parentObject, node.Attributes["alt"].Value, sibling));
                    return list;
                }
            }
                
            
        }
        
        document.GetMap()[node].getMap().TryGetValue("display", out var displayType);
        
        switch (displayType)
        {
            case "none":
                return list;
            
            case "list-item":
            case "block":
            { // todo switch image
                var elem = MakeBlock(node, parentObject, sibling);
                DoBackground(elem);
                var localList = new List<RenderObject>();
                elem.Rectangle.bottom = elem.Rectangle.top;

                RenderObject ls = null;

                foreach (var childNode in node.ChildNodes)
                {
                    var ll = MakeRenderObjects(childNode, elem, ls);
                    localList.AddRange(ll);

                    if (childNode.Name == "#text")
                    {
                        foreach (var o in ll)
                        {
                            if (o.Rectangle.bottom > elem.Rectangle.bottom)
                            {
                                elem.Rectangle.bottom = o.Rectangle.bottom;
                            }

                            if (o.Rectangle.right > elem.Rectangle.right)
                            {
                                elem.Rectangle.right = o.Rectangle.right;
                            }
                            
                            
                        }
                    }
                    else
                    {
                        if (ll.Count > 0) //todo return max
                        {
                            elem.Rectangle.bottom = ll.Max(x => x.Rectangle.bottom);
                            // elem.Rectangle.bottom = Math.Max(ll[0].Rectangle.bottom, ll[^1].Rectangle.bottom);
                        }
                    }
                    
                    if (ll.Count > 0)
                    {
                        ls = ll[0];
                    }
                    
                }
            
                var parentWidth = parentObject == null ? 0 : parentObject.Rectangle.Width();
        
                // todo parent margin-top, ...
                CssMath.GetMargin(elem.Map.getMap(), parentWidth, viewport, 
                    out var marginLeft, out var marginRight, out var marginTop, out var marginBottom);
                
                CssMath.GetPadding(document.GetMap()[node.ParentNode].getMap(), parentWidth, viewport,
                    out var paddingLeft, out var paddingRight, out var paddingTop, out var paddingBottom);

                elem.Rectangle.bottom += marginBottom + paddingBottom;

                if (node.Name == "a")
                {
                    var link = new LinkObject(node.Attributes["href"]?.Value ?? "")
                    {
                        Rectangle = elem.Rectangle
                    };
                    list.Add(link);
                }
                
                list.Add(elem);
                list.AddRange(localList);
                return list;
            }
            
            case "flex":
            {
                var elem = MakeBlock(node, parentObject, sibling);
                DoBackground(elem);
                var localList = new List<RenderObject>();

                var children = new List<RenderObject>();

                int xCursor = elem.Rectangle.left;
                int yCursor = elem.Rectangle.top;
                int rowHeight = 0;

                document.GetMap()[node].getMap().TryGetValue("justify-content", out var justifyContent);
                document.GetMap()[node].getMap().TryGetValue("align-items", out var alignItems);
                document.GetMap()[node].getMap().TryGetValue("flex-direction", out var flexDirection);
    
                bool isRow = flexDirection is null or "row"; // default is 'row'
    
                foreach (var childNode in node.ChildNodes)
                {
                    var childRenderObjects = MakeRenderObjects(childNode, elem);

                    foreach (var child in childRenderObjects)
                    {
                        localList.Add(child);
                        children.Add(child);

                        var rect = child.Rectangle;

                        if (isRow)
                        {
                            rect.left = xCursor;
                            rect.top = yCursor;
                            rect.bottom = yCursor + rect.Height();
                            rect.right = xCursor + rect.Width();

                            xCursor += rect.Width();
                            rowHeight = Math.Max(rowHeight, rect.Height());
                        }
                        else
                        {
                            rect.left = xCursor;
                            rect.top = yCursor;
                            rect.bottom = rect.top + rect.Height();
                            rect.right = rect.left + rect.Width();

                            yCursor += rect.Height();
                        }

                        child.Rectangle = rect;
                    }
                }

                if (isRow)
                {
                    elem.Rectangle.right = xCursor;
                    elem.Rectangle.bottom = yCursor + rowHeight;
                }
                else
                {
                    elem.Rectangle.right = children.Max(c => c.Rectangle.right);
                    elem.Rectangle.bottom = yCursor;
                }

                list.Add(elem);
                list.AddRange(localList);
                return list;
            }
            
            case null:
            case "inline":
            {
                var elem = MakeInline(node, parentObject, sibling);
                DoBackground(elem);
                var localList = new List<RenderObject>();
                elem.Rectangle.bottom = elem.Rectangle.top;
                elem.Rectangle.right = elem.Rectangle.left;
                
                RenderObject ls = null;
            
                foreach (var childNode in node.ChildNodes)
                {
                    var ll = MakeRenderObjects(childNode, elem, ls);
                    localList.AddRange(ll);
            
                    if (childNode.Name == "#text")
                    {
                        foreach (var o in ll)
                        {
                            if (o.Rectangle.bottom > elem.Rectangle.bottom)
                            {
                                elem.Rectangle.bottom = o.Rectangle.bottom;
                            }

                            if (o.Rectangle.right > elem.Rectangle.right)
                            {
                                elem.Rectangle.right = o.Rectangle.right;
                            }
                            
                            
                        }
                    }
                    else
                    {
                        if (ll.Count > 0)
                        {
                            elem.Rectangle.bottom += ll[0].Rectangle.Height();
                            elem.Rectangle.right += ll[0].Rectangle.Width();
                        }
                    }
                    
                    if (ll.Count > 0)
                    {
                        ls = ll[0];
                    }
                    
                }
            
                if (node.Name == "a")
                {
                    var link = new LinkObject(node.Attributes["href"]?.Value ?? "")
                    {
                        Rectangle = elem.Rectangle
                    };
                    list.Add(link);
                }
                
                list.Add(elem);
                list.AddRange(localList);
                return list;
                
                break;
            }

        }

        return list;
    }

    private void DoBackground(RenderObject obj)
    {

        obj.BackgroundObjects = new List<RenderObjectBackground>();
        
        Rect rect = obj.Rectangle;
        obj.Map.getMap().TryGetValue("background-color", out var backColor);
        obj.Map.getMap().TryGetValue("background-image", out var backImg);
        
        string pattern = @"url\(([^)]+)\)";
        
        Regex regex = new Regex(pattern);
        if (backImg != null)
        {
            Match match = regex.Match(backImg);
        
            if (match.Success)
            {
                string url = match.Groups[1].Value;
                Resource res = new Resource(url, Resource.ResourceType.Img);
                            
                try
                {
                    tab.owner.resourceManager.GetResource(ref res);
                    if (res.localPath != null)
                    {
                        obj.BackgroundObjects.Add(new RenderObjectImageBackground(res));
                    }
                }
                catch (Exception)
                {
                    Console.WriteLine($"Bad resource: {url}");
                }
            }
        }
        
        if (!string.IsNullOrEmpty(backColor))
        {
            try
            {
                SKColor sColor = SKColor.Parse(backColor.ToUpper());
                obj.BackgroundObjects.Add(new RenderObjectSolidColorBackground(sColor));
            }
            catch (Exception exception)
            {
                obj.BackgroundObjects.Add(new RenderObjectSolidColorBackground(new SKColor(0,0,0,0)));
                // Console.WriteLine(exception);
            }
        }
        else
        {
            obj.BackgroundObjects.Add(new RenderObjectSolidColorBackground(new SKColor(0,0,0,0)));
        }
    }

    private RenderObject MakeInline(HtmlNode node, RenderObject parentObject, RenderObject? sibling)
    {
        var elem = new RenderObject
        {
            Map = document.GetMap()[node],
            HtmlNode = node,
            ObjectType = RenderObjectType.Inline
        };

        var rect = new Rect();
        var parentWidth = parentObject == null ? 0 : parentObject.Rectangle.Width();
        
        // todo parent margin-top, ...
        CssMath.GetMargin(elem.Map.getMap(), parentWidth, viewport, 
            out var marginLeft, out var marginRight, out var marginTop, out var marginBottom);
        
        CssMath.GetPadding(document.GetMap()[node.ParentNode].getMap(), parentWidth, viewport,
            out var paddingLeft, out var paddingRight, out var paddingTop, out var paddingBottom);

        if (parentObject == null)
        {
            rect.left = 0 + marginLeft + paddingLeft;
            // rect.right = viewport - marginRight - paddingRight;
            rect.top = 0 + marginTop + marginTop;
        }
        else
        {
            if (sibling != null)
            {
                rect.left = sibling.Rectangle.right + marginLeft;
                // rect.right = parentObject.Rectangle.right - marginRight - paddingRight;
                rect.top = sibling.Rectangle.top + marginTop;
            }
            else
            {
                rect.left = parentObject.Rectangle.left + marginLeft + paddingLeft;
                // rect.right = parentObject.Rectangle.right - marginRight - paddingRight;
                rect.top = parentObject.Rectangle.bottom + marginTop + paddingTop;
            }
        }

        elem.Rectangle = rect;
        return elem;
    }

    private RenderObject MakeBlock(HtmlNode node, RenderObject parentObject, RenderObject? sibling)
    {
        var elem = new RenderObject
        {
            Map = document.GetMap()[node],
            HtmlNode = node,
            ObjectType = RenderObjectType.Block
        };

        var rect = new Rect();
        var parentWidth = parentObject == null ? 0 : parentObject.Rectangle.Width();
        
        // todo parent margin-top, ...
        CssMath.GetMargin(elem.Map.getMap(), parentWidth, viewport, 
            out var marginLeft, out var marginRight, out var marginTop, out var marginBottom);
        
        CssMath.GetPadding(document.GetMap()[node.ParentNode].getMap(), parentWidth, viewport,
            out var paddingLeft, out var paddingRight, out var paddingTop, out var paddingBottom);

        if (parentObject == null)
        {
            rect.left = 0 + marginLeft + paddingLeft;
            rect.right = viewport - marginRight - paddingRight;
            rect.top = 0 + marginTop + paddingTop;
        }
        else
        {
            
            if (sibling != null)
            {
                rect.left = parentObject.Rectangle.left + marginLeft + paddingLeft;
                rect.right = parentObject.Rectangle.right - marginRight - paddingRight;
                rect.top = sibling.Rectangle.bottom + marginTop;
            }
            else
            {
                rect.left = parentObject.Rectangle.left + marginLeft + paddingLeft;
                rect.right = parentObject.Rectangle.right - marginRight - paddingRight;
                rect.top = parentObject.Rectangle.top + marginTop + paddingTop;
            }
        }

        elem.Rectangle = rect;
        return elem;
    }

    private List<TextObject> MakeText(HtmlNode node, RenderObject parentObject, string text, RenderObject? sibling=null)
    {
        var parentWidth = parentObject.Rectangle.Width();
        
        var fontHeight = 16;
        
        var pLink = node.ParentNode;
        while (pLink != null && pLink.Name != "html")
        {
            document.GetMap()[pLink].getMap().TryGetValue("font", out var fontSize);
            if (!string.IsNullOrEmpty(fontSize))
            {
                try
                {
                    CssMath.GetFontSize(fontSize, viewport, out fontHeight);
                }
                catch (Exception e)
                {
                    Console.WriteLine(e);
                }
                break;
            }

            pLink = pLink.ParentNode;
        }
        
        CssMath.GetMargin(document.GetMap()[node].getMap(), parentWidth, viewport, 
            out var marginLeft, out var marginRight, out var marginTop, out var marginBottom);
        
        CssMath.GetPadding(document.GetMap()[node.ParentNode].getMap(), parentWidth, viewport,
            out var paddingLeft, out var paddingRight, out var paddingTop, out var paddingBottom);

        var neededWidth = parentWidth - paddingLeft - paddingRight;
        
        SKRect size = new();
        SKPaint paint = new()
        {
            Color = SKColors.Black,
            TextSize = fontHeight,
            IsAntialias = true,
            Typeface = SKTypeface.FromFamilyName("Times New Roman")
        };
        paint.MeasureText(text, ref size);

        if (size.Width > neededWidth && parentObject.Rectangle.Width() > 0)
        {
            text = CssMath.SplitTextLines(text, neededWidth, paint);
            paint.MeasureText(text, ref size);

            var list = new List<TextObject>();
            
            foreach (var lText in text.Split("\n"))
            {
                var elem = new TextObject(lText, fontHeight)
                {
                    Map = document.GetMap()[node],
                    ObjectType = RenderObjectType.Inline
                };
                
                var rect = new Rect();

                // var dopLength = 4 * (text.Length - text.Trim().Length);
                var dopLength = 0;
                
                rect.left = parentObject.Rectangle.left + paddingLeft + marginLeft;
                rect.right = rect.left + (int)size.Width + paddingRight + marginRight + dopLength;
                rect.top = (sibling ?? parentObject).Rectangle.bottom + paddingTop + marginTop;
                rect.bottom = rect.top + fontHeight + paddingBottom + marginBottom;
                
                elem.Rectangle = rect;
                list.Add(elem);
                sibling = elem;
            }

            return list;

        }
        else
        {
            var elem = new TextObject(text, fontHeight)
            {
                Map = document.GetMap()[node],
                ObjectType = RenderObjectType.Inline
            };

            var rect = new Rect();
            
            var dopLength = 4 * (text.Length - text.Trim().Length);
        
            if (sibling != null)
            {
                rect.left = sibling.Rectangle.right + paddingLeft + marginLeft;
                rect.right = rect.left + (int)size.Width + paddingRight + marginRight + dopLength;
                rect.top = parentObject.Rectangle.top + paddingTop + marginTop;
                rect.bottom = rect.top + fontHeight + paddingBottom + marginBottom;
            }
            else
            {
                rect.left = parentObject.Rectangle.left + paddingLeft + marginLeft;
                rect.right = rect.left + (int)size.Width + paddingRight + marginRight + dopLength;
                rect.top = parentObject.Rectangle.bottom + paddingTop + marginTop;
                rect.bottom = rect.top + fontHeight + paddingBottom + marginBottom;
            }
        
            elem.Rectangle = rect;
            return [elem];
        }
        
    }
    
    private ImageObject MakeImage(HtmlNode node, string path, RenderObject parentObject, RenderObject? sibling)
    {
        var resource = tab.resources.First(x => path.Equals(x.path));
        var fileName = resource.localPath;
        var isGif = Path.GetExtension(resource.path).ToLower() == ".gif";
        var imageHeight = 0;
        var imageWidth = 0;
        if (fileName != null)
        {
            var img = Image.FromFile(fileName);
            imageHeight = img.Height;
            imageWidth = img.Width;
        }
        
        var elem = new ImageObject(fileName)
        {
            Map = document.GetMap()[node],
            HtmlNode = node,
            ObjectType = RenderObjectType.Block,
            LocalPath = fileName
        };

        var rect = new Rect();
        var parentWidth = parentObject == null ? 0 : parentObject.Rectangle.Width();
        
        // todo parent margin-top, ...
        CssMath.GetMargin(elem.Map.getMap(), parentWidth, viewport, 
            out var marginLeft, out var marginRight, out var marginTop, out var marginBottom);
        
        CssMath.GetPadding(document.GetMap()[node.ParentNode].getMap(), parentWidth, viewport,
            out var paddingLeft, out var paddingRight, out var paddingTop, out var paddingBottom);

        if (parentObject == null)
        {
            rect.left = 0 + marginLeft + paddingLeft;
            rect.right = viewport - marginRight - paddingRight;
            rect.top = 0 + marginTop + paddingTop;
            rect.bottom = rect.top + imageHeight + marginBottom + paddingBottom;
        }
        else
        {
            if (sibling != null)
            {
                rect.left = parentObject.Rectangle.left + marginLeft + paddingLeft;
                rect.right = rect.left + imageWidth + marginRight + paddingRight;
                rect.top = sibling.Rectangle.bottom + marginTop + paddingTop;
                rect.bottom = rect.top + imageHeight + marginBottom + paddingBottom;
            }
            else
            {
                rect.left = parentObject.Rectangle.left + marginLeft + paddingLeft;
                rect.right = rect.left + imageWidth + marginRight + paddingRight;
                rect.top = parentObject.Rectangle.top + marginTop + paddingTop;
                rect.bottom = rect.top + imageHeight + marginBottom + paddingBottom;
            }
            
        }

        elem.Rectangle = rect;
        return elem;
    }

}
using System.Text.RegularExpressions;
using Browser.Management;
using Browser.Networking;
using SkiaSharp;
using SkiaSharp.Views.Desktop;
using Timer = Browser.Management.Timer;

namespace Browser.Render;

public class ObjectRenderer
{
    private VScrollBar verticalScrollBar;
    private SKBitmap bufferBitmap;
    private SKCanvas bufferCanvas;
    private readonly Size browserSize;
    private List<LinkInfo> _linkInfos = [];
    
    public ObjectRenderer(Tab tab, Layout layout, SKControl parentPanel)
    {
        browserSize = new(parentPanel.Width, parentPanel.Height);
        
        var renderObjects = layout.MakeRenderObjects(tab.document.DocumentNode.SelectSingleNode("//body"), null);

        var renderPanel = new SKControl()
        {
            Height = parentPanel.Height,
            Width = parentPanel.Width
        };
        
        var pageRenderPanel = new SKControl();
        pageRenderPanel.Height = renderObjects[0].Rectangle.Height();
        pageRenderPanel.Width = parentPanel.Width - 20;
        pageRenderPanel.PaintSurface += (sender, e) =>
        {
            Timer.start();
            var surface = e.Surface;
            var canvas = surface.Canvas;
            canvas.Clear(SKColor.Parse("#ffffff"));
            
            if (bufferBitmap == null)
            {
                bufferBitmap = new SKBitmap(pageRenderPanel.Width, pageRenderPanel.Height);
                bufferCanvas = new SKCanvas(bufferBitmap);
                this.DrawAllObjects(tab, renderObjects);
                Timer.end();
            }

            var visibleRect = new SKRectI(0, verticalScrollBar.Value, 
                pageRenderPanel.Width, browserSize.Height+verticalScrollBar.Value);
            
            var defaultRect = new SKRectI(0, 0, 
                pageRenderPanel.Width, browserSize.Height);
    
            canvas.DrawBitmap(bufferBitmap, visibleRect, defaultRect);
        };
        
        pageRenderPanel.MouseClick += (sender, e) =>
        {
            float clickX = e.X;
            float clickY = e.Y + verticalScrollBar.Value; // Учитываем прокрутку

            foreach (var link in _linkInfos.Where(link => link.Bounds.Contains(clickX, clickY)))
            {
                Console.WriteLine($"Clicked on {link.Url}");
            }
        };
        
        var renderTimer = new System.Windows.Forms.Timer();
        renderTimer.Interval = 100;
        renderTimer.Tick += (sender, e) => pageRenderPanel.Invalidate();
        renderTimer.Start();
        
        verticalScrollBar = new VScrollBar();
        verticalScrollBar.Dock = DockStyle.Right;
        verticalScrollBar.Width = 20;
        verticalScrollBar.Height = parentPanel.Height;
        verticalScrollBar.Top = parentPanel.Top;
        verticalScrollBar.Maximum = renderObjects[0].Rectangle.Height() - (browserSize.Height - 125);
        verticalScrollBar.SmallChange = 50; 
        verticalScrollBar.LargeChange = 100;
        verticalScrollBar.Scroll += (sender, e) => pageRenderPanel.Invalidate();
        
        renderPanel.Controls.Add(pageRenderPanel);
        renderPanel.Controls.Add(verticalScrollBar);
        parentPanel.Controls.Add(renderPanel);
    }
    
    private void DrawAllObjects(Tab tab, List<RenderObject> list)
    {
        foreach (var obj in list)
        {
            obj.DoRender(this);
        }
    }
    
    public void drawDefaultRect( SKColor color, float x, float y, float w, float h)
    {
        SKPaint p = new() { Color = color };
        bufferCanvas.DrawRect(x, y, w, h, p);
    }
    public void drawDefaultRectWithBorder( SKColor color, float x, float y, float w, float h, SKColor borderColor, float borderWidth)
    {
        SKPaint p = new() { Color = color };
        bufferCanvas.DrawRect(x, y, w, h, p);
        
        SKPaint borderPaint = new SKPaint
        {
            Color = borderColor,
            IsAntialias = true,
            Style = SKPaintStyle.Stroke,
            StrokeWidth = borderWidth
        };
        bufferCanvas.DrawRect(x,y,w,h,borderPaint);
    }
    

    public void drawRoundRect(SKColor color, float x, float y, float w, float h, float cornerRadius)
    {
        SKPaint p = new() { Color = color };
        SKRect rect = new SKRect(x, y, x+w, y+h);
        bufferCanvas.DrawRoundRect(rect, cornerRadius, cornerRadius, p);
    }
    public void drawRoundRectWithBorder(SKColor color, float x, float y, float w, float h,
        float cornerRadius, SKColor borderColor, float borderWidth)
    {
        SKPaint p = new() { Color = color };
        SKRect rect = new SKRect(x, y, x+w, y+h);
        bufferCanvas.DrawRoundRect(rect, cornerRadius, cornerRadius, p);
        
        SKPaint borderPaint = new SKPaint
        {
            Color = borderColor,
            IsAntialias = true,
            Style = SKPaintStyle.Stroke,
            StrokeWidth = borderWidth
        };
        bufferCanvas.DrawRoundRect(rect, cornerRadius, cornerRadius, borderPaint);
    }
    

    public void drawText(SKColor color, Rect rect, string text, float textSize, bool under)
    {
        var p = new SKPaint()
        {
            Color = SKColors.Black,
            TextSize = textSize,
            IsAntialias = true,
            Typeface = SKTypeface.FromFamilyName("Times New Roman")
        };
        p.Color = color;
        bufferCanvas.DrawText(text,rect.left,rect.bottom, p);

        if (under)
        {
            float lineY = rect.bottom + 2.0f;
            bufferCanvas.DrawLine(rect.left, lineY, rect.right, lineY, p);
        }
    }

    public void addLink(LinkInfo linkInfo)
    {
        _linkInfos.Add(linkInfo);
    }

    public void drawImage(float x, float y, string path)
    {
        SKBitmap bitmap = SKBitmap.Decode(path);
        bufferCanvas.DrawBitmap(bitmap, new SKPoint(x,y));
    }
}
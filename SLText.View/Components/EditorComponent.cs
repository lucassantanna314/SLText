using SkiaSharp;
using SLText.Core.Engine;
using SLText.View.Abstractions;

namespace SLText.View.Components;

public class EditorComponent : IComponent
{
    public SKRect Bounds { get; set; }
    private readonly TextBuffer _buffer;
    private readonly CursorManager _cursor;
    
    public float ScrollY { get; set; } = 0;
    private float _lineHeight;
    private bool _needScrollToCursor;
    
    private readonly SKPaint _gutterPaint = new() 
    { 
        Color = new SKColor(100, 100, 100), 
        IsAntialias = true 
    };
    
    private readonly SKPaint _gutterBgPaint = new() 
    { 
        Color = new SKColor(40, 40, 40) 
    };
    
    public void RequestScrollToCursor() => _needScrollToCursor = true;
    
    private readonly SKFont _font;
    private readonly SKPaint _textPaint = new() 
    { 
        Color = SKColors.White, 
        IsAntialias = true 
    };
    private readonly SKPaint _cursorPaint = new() { Color = SKColors.Chocolate };

    private bool _showCursor = true;
    private double _cursorTimer; 
    private const double BlinkInterval = 0.5; 

    public EditorComponent(TextBuffer buffer, CursorManager cursor)
    {
        _buffer = buffer;
        _cursor = cursor;
        
        var typeface = SKTypeface.FromFamilyName("Consolas");
        
        _font = new SKFont(typeface, 18)
        {
            Edging = SKFontEdging.SubpixelAntialias, 
            Hinting = SKFontHinting.Full,
            Subpixel = true 
        };
    }

    public CursorManager GetCursor() => _cursor;

    public void Render(SKCanvas canvas)
    {
        _font.GetFontMetrics(out var metrics);
    _lineHeight = metrics.Descent - metrics.Ascent + 4;
    
    var lines = _buffer.GetLines().ToList();
    
    string maxLineStr = lines.Count.ToString();
    float gutterPadding = 20;
    float gutterWidth = _font.MeasureText(maxLineStr) + gutterPadding;

    canvas.Save();
    canvas.ClipRect(Bounds);
    
    canvas.Save();
    
    var gutterRect = new SKRect(Bounds.Left, Bounds.Top, Bounds.Left + gutterWidth, Bounds.Bottom);
    canvas.DrawRect(gutterRect, _gutterBgPaint);
    canvas.Restore();

    canvas.Translate(0, -ScrollY);
    
    for (int i = 0; i < lines.Count; i++)
    {
        float yPos = Bounds.Top + (i * _lineHeight) - metrics.Ascent;

        if (yPos - _lineHeight > Bounds.Bottom + ScrollY) break;
        if (yPos + _lineHeight < Bounds.Top + ScrollY) continue;

        string lineNum = (i + 1).ToString();
        float lineNumX = Bounds.Left + (gutterWidth - _font.MeasureText(lineNum) - 10);
        canvas.DrawText(lineNum, lineNumX, yPos, _font, _gutterPaint);

        float textX = Bounds.Left + gutterWidth + 10; 
        canvas.DrawText(lines[i], textX, yPos, _font, _textPaint);

        if (i == _cursor.Line && _showCursor)
        {
            float cursorX = _font.MeasureText(lines[i].Substring(0, _cursor.Column));
            var cursorRect = new SKRect(
                textX + cursorX,
                yPos + metrics.Ascent,
                textX + 2 + cursorX,
                yPos + metrics.Descent
            );
            canvas.DrawRect(cursorRect, _cursorPaint);
        }
    }
    
    canvas.Restore();
    }
    
    public void ScrollToCursor()
    {
        float cursorYTop = _cursor.Line * _lineHeight;
        float cursorYBottom = (_cursor.Line + 1) * _lineHeight;

        if (cursorYBottom > ScrollY + Bounds.Height)
        {
            ScrollY = cursorYBottom - Bounds.Height;
        }
        else if (cursorYTop < ScrollY)
        {
            ScrollY = cursorYTop;
        }
    }

    public void Update(double deltaTime)
    {
        _cursorTimer += deltaTime;
        if (_cursorTimer >= BlinkInterval)
        {
            _showCursor = !_showCursor;
            _cursorTimer = 0;
        }
        
        if (_needScrollToCursor) {
            ScrollToCursor();
            _needScrollToCursor = false; 
        }
    }
}
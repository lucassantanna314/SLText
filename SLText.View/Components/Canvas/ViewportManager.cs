using SkiaSharp;

namespace SLText.View.Components.Canvas;

public class ViewportManager
{
    public float ScrollX { get; set; } = 0;
    public float ScrollY { get; set; } = 0;
    
    private SKRect _bounds;
    private float _lineHeight;

    public ViewportManager(float lineHeight)
    {
        _lineHeight = lineHeight;
    }

    public void UpdateBounds(SKRect bounds) => _bounds = bounds;
    
    public void ScrollToCursor(int line, int column, float cursorXPos, float gutterWidth)
    {
        // --- SCROLL VERTICAL (Y) ---
        float cursorYTop = line * _lineHeight;
        float cursorYBottom = (line + 1) * _lineHeight;

        if (cursorYBottom > ScrollY + _bounds.Height)
            ScrollY = cursorYBottom - _bounds.Height;
        else if (cursorYTop < ScrollY)
            ScrollY = cursorYTop;

        // --- SCROLL HORIZONTAL (X) ---
        float viewPortWidth = _bounds.Width - gutterWidth - 40; 

        if (cursorXPos > ScrollX + viewPortWidth)
            ScrollX = cursorXPos - viewPortWidth;
        else if (cursorXPos < ScrollX)
            ScrollX = Math.Max(0, cursorXPos - 20);
    }

    
    public void ApplyScroll(float deltaX, float deltaY, float maxScrollX, float totalLinesHeight)
    {
        if (deltaX != 0)
        {
            ScrollX = Math.Clamp(ScrollX - deltaX, 0, maxScrollX);
        }

        if (deltaY != 0)
        {
            float maxScrollY = Math.Max(0, totalLinesHeight - (_bounds.Height / 2));
            ScrollY = Math.Clamp(ScrollY - deltaY, 0, maxScrollY);
        }
    }
    
    public (int line, int col) GetTextPosition(float x, float y, float gutterWidth, float charWidth)
    {
        float documentY = y + ScrollY;
        int line = (int)(documentY / _lineHeight);

        float documentX = x - _bounds.Left - gutterWidth - 10 + ScrollX;
        int col = (int)Math.Round(documentX / charWidth);

        return (line, col);
    }
    
    public void UpdateLineHeight(float newLineHeight)
    {
        float ratio = 0;
        if (_lineHeight > 0)
        {
            ratio = ScrollY / _lineHeight;
        }

        _lineHeight = newLineHeight;

        ScrollY = ratio * _lineHeight;
    }
}
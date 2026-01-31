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

    // Garante que o cursor esteja sempre dentro da visão, movendo o scroll se necessário.
   
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

  
    // Aplica o scroll vindo do mouse ou touch.
  
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

    // Converte uma coordenada de mouse para uma posição de texto (Linha/Coluna).
    
    public (int line, int col) GetTextPosition(float mouseX, float mouseY, float gutterWidth, float charWidth)
    {
        float relativeY = mouseY - _bounds.Top + ScrollY;
        int line = (int)(relativeY / _lineHeight);

        float textAreaLeft = _bounds.Left + gutterWidth; 
        float relativeX = (mouseX - textAreaLeft) + ScrollX; 

        int col = (int)Math.Max(0, Math.Round(relativeX / charWidth));

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
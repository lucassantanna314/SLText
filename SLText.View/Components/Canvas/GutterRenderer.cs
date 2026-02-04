using SkiaSharp;
using SLText.View.Styles;

namespace SLText.View.Components.Canvas;

public class GutterRenderer
{
    private readonly SKFont _font;
    private EditorTheme _theme;
    private readonly SKPaint _backgroundPaint = new() { IsAntialias = true };
    private readonly SKPaint _textPaint = new() { IsAntialias = true };

    public GutterRenderer(SKFont font, EditorTheme theme)
    {
        _font = font;
        _theme = theme;
        UpdatePaints();
    }

    public void SetTheme(EditorTheme theme)
    {
        _theme = theme;
        UpdatePaints();
    }

    private void UpdatePaints()
    {
        _backgroundPaint.Color = _theme.GutterBackground;
        _textPaint.Color = _theme.GutterForeground;
    }

    public float GetWidth(int totalLines)
    {
        string maxLineStr = totalLines.ToString();
        float gutterPadding = 35; 
        return _font.MeasureText(maxLineStr) + gutterPadding;
    }
    
    public void Render(SKCanvas canvas, SKRect bounds, List<int> visibleLineNumbers, float lineHeight, float scrollY)
    {
        int maxLineNumber = visibleLineNumbers.Count > 0 ? visibleLineNumbers[^1] : 0;
        float width = GetWidth(maxLineNumber);

        var gutterRect = new SKRect(bounds.Left, bounds.Top, bounds.Left + width, bounds.Bottom);
        canvas.DrawRect(gutterRect, _backgroundPaint);

        _font.GetFontMetrics(out var metrics);

        canvas.Save();
        canvas.ClipRect(gutterRect);

        foreach (int lineNum in visibleLineNumbers)
        {
            int lineIndex = lineNum - 1;
            float yPos = bounds.Top + (lineIndex * lineHeight) - scrollY;

            if (yPos < bounds.Top - lineHeight) continue;
            if (yPos > bounds.Bottom) break;

            string lineStr = lineNum.ToString();
            float textWidth = _font.MeasureText(lineStr);
            float xPos = bounds.Left + width - textWidth - 10; 

            canvas.DrawText(lineStr, xPos, yPos - metrics.Ascent, _font, _textPaint);
        }

        canvas.Restore();
    }

}
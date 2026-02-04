using SkiaSharp;
using SLText.Core.Engine;
using SLText.View.Styles;

namespace SLText.View.Components.Canvas;

public class IndentGuideRenderer
{
    private readonly BlockAnalyzer _analyzer = new();
    private EditorTheme _theme;

    public IndentGuideRenderer(EditorTheme theme)
    {
        _theme = theme;
    }

    public void SetTheme(EditorTheme theme) => _theme = theme;
    
    public void Render(SKCanvas canvas, TextBuffer buffer, CursorManager cursor, 
        float gutterWidth, float charWidth, SKRect bounds, 
        float lineHeight, float scrollY, string extension)
    {
        var blocks = _analyzer.AnalyzeBlocks(buffer, extension);
        float textPadding = 10;
        float startX = bounds.Left + gutterWidth + textPadding;

        using var guidePaint = new SKPaint
        {
            Color = _theme.SelectionBackground,
            StrokeWidth = 1f,
            PathEffect = SKPathEffect.CreateDash(new float[] { 2, 2 }, 0)
        };

        foreach (var block in blocks)
        {
            float xPos = startX + (block.IndentLevel * charWidth);
            
            float yStart = bounds.Top + (block.StartLine * lineHeight) - scrollY + lineHeight;
            float yEnd = bounds.Top + (block.EndLine * lineHeight) - scrollY;

            if (yEnd < bounds.Top || yStart > bounds.Bottom) continue;

            canvas.DrawLine(xPos, Math.Max(yStart, bounds.Top), xPos, Math.Min(yEnd, bounds.Bottom), guidePaint);
        }

        var activeBlock = blocks
            .Where(b => cursor.Line >= b.StartLine && cursor.Line <= b.EndLine)
            .OrderByDescending(b => b.IndentLevel)
            .FirstOrDefault();

        if (activeBlock != null)
        {
            using var activePaint = new SKPaint
            {
                Color = _theme.SelectionBackground.WithAlpha(100),
                StrokeWidth = 1.5f,
                IsAntialias = true
            };

            float xPos = startX + (activeBlock.IndentLevel * charWidth);
            float yStart = bounds.Top + (activeBlock.StartLine * lineHeight) - scrollY + lineHeight;
            float yEnd = bounds.Top + (activeBlock.EndLine * lineHeight) - scrollY;

            canvas.DrawLine(xPos, Math.Max(yStart, bounds.Top), xPos, Math.Min(yEnd, bounds.Bottom), activePaint);
        }
    }

}
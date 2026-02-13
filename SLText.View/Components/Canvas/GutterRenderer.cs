using System.Text.RegularExpressions;
using Microsoft.CodeAnalysis;
using SkiaSharp;
using SLText.Core.Engine;
using SLText.Core.Engine.LSP;
using SLText.View.Styles;

namespace SLText.View.Components.Canvas;

public class GutterRenderer
{
    private readonly SKFont _font;
    private EditorTheme _theme;
    private readonly SKPaint _backgroundPaint = new() { IsAntialias = true };
    private readonly SKPaint _textPaint = new() { IsAntialias = true };
    private static readonly Regex TestAttributeRegex = new Regex(@"\[(Test|Fact|TestMethod|TestClass)\]", RegexOptions.Compiled);
    private List<LspService.MappedDiagnostic> _diagnostics = new();
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
    public void SetDiagnostics(List<LspService.MappedDiagnostic> diags) => _diagnostics = diags;
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
    
    public void Render(SKCanvas canvas, SKRect bounds, List<int> visibleLineNumbers, float lineHeight, float scrollY, TextBuffer buffer)
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
            
            bool hasMissingReference = _diagnostics.Any(d => 
                d.Line == lineNum && 
                d.Id == "CS0246");

            if (hasMissingReference)
            {
                float iconX = bounds.Left + 5;
                float iconY = yPos + (lineHeight / 2);
                DrawLightBulb(canvas, iconX, iconY);
            }
            
            string lineContent = buffer.GetLine(lineIndex);
            if (lineContent.Contains("[Fact]") || lineContent.Contains("[Test]") || lineContent.Contains("[TestMethod]"))
            {
                float iconX = bounds.Left + 20;
                float iconY = yPos + (lineHeight / 2);
                DrawPlayIcon(canvas, iconX, iconY);
            }
            
            if (yPos > bounds.Bottom) break;

            string lineStr = lineNum.ToString();
            float textWidth = _font.MeasureText(lineStr);
            float xPos = bounds.Left + width - textWidth - 10; 

            canvas.DrawText(lineStr, xPos, yPos - metrics.Ascent, _font, _textPaint);
        }

        canvas.Restore();
    }
    
    private void DrawPlayIcon(SKCanvas canvas, float x, float y)
    {
        using var paint = new SKPaint { Color = SKColors.LightGreen, IsAntialias = true };
        float size = 12;
    
        var path = new SKPath();
        path.MoveTo(x, y - (size / 2));
        path.LineTo(x, y + (size / 2));
        path.LineTo(x + size, y);
        path.Close();
    
        canvas.DrawPath(path, paint);
    }
    
    private void DrawLightBulb(SKCanvas canvas, float x, float y)
    {
        using var paint = new SKPaint { Color = SKColors.Yellow, IsAntialias = true, Style = SKPaintStyle.Fill };
        canvas.DrawCircle(x + 5, y, 4, paint);
        
        using var stroke = new SKPaint { Color = SKColors.DarkGoldenrod, IsAntialias = true, Style = SKPaintStyle.Stroke };
        canvas.DrawCircle(x + 5, y, 4, stroke);
    }

}
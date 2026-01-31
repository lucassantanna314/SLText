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
        float gutterPadding = 25; 
        return _font.MeasureText(maxLineStr) + gutterPadding;
    }

    public void Render(SKCanvas canvas, SKRect bounds, int lineCount, float lineHeight, float scrollY)
    {
        float width = GetWidth(lineCount);
        
        // Desenha o fundo do Gutter
        var gutterRect = new SKRect(bounds.Left, bounds.Top, bounds.Left + width, bounds.Bottom);
        canvas.DrawRect(gutterRect, _backgroundPaint);

        // Prepara métricas para alinhar o texto verticalmente
        _font.GetFontMetrics(out var metrics);
        
        // Loop de renderização das linhas visíveis
        for (int i = 0; i < lineCount; i++)
        {
            // Calcula a posição Y da linha
            float yPos = bounds.Top + (i * lineHeight) - scrollY - metrics.Ascent;

            // Culling: Não desenha se estiver fora da tela (acima ou abaixo)
            if (yPos + metrics.Descent < bounds.Top) continue;
            if (yPos + metrics.Ascent > bounds.Bottom) break;

            // Desenha o número da linha
            string lineNum = (i + 1).ToString();
            
            // Alinhamento à direita com um pequeno padding (10)
            float xPos = bounds.Left + width - _font.MeasureText(lineNum) - 10;
            
            canvas.DrawText(lineNum, xPos, yPos, _font, _textPaint);

            // --- (Folding) ---
        }
    }
}
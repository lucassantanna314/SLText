using SkiaSharp;
using SLText.Core.Engine;
using SLText.Core.Engine.LSP;
using SLText.View.Styles;

namespace SLText.View.Components;

public class SignatureHelpComponent
{
    public bool IsVisible { get; set; }
    public SKRect Bounds { get; set; }
    
    private SignatureHelpResult? _data;
    private readonly SKFont _font;
    private readonly SKFont _boldFont;
    private EditorTheme _theme = EditorTheme.Dark;

    public SignatureHelpComponent()
    {
        string fontPath = Path.Combine(AppContext.BaseDirectory, "Assets", "JetBrainsMono-Regular.ttf");
        SKTypeface typeface;

        if (File.Exists(fontPath))
        {
            typeface = SKTypeface.FromFile(fontPath);
        }
        else
        {
            typeface = SKTypeface.FromFamilyName("monospace", SKFontStyle.Normal);
        }
        
        _font = new SKFont(typeface, 14) { Edging = SKFontEdging.SubpixelAntialias, Subpixel = true };
        
        var boldTypeface = SKTypeface.FromFamilyName(typeface.FamilyName, SKFontStyle.Bold);
        _boldFont = new SKFont(boldTypeface ?? typeface, 14) { Edging = SKFontEdging.SubpixelAntialias, Subpixel = true };
    }

    public void Show(float x, float y, SignatureHelpResult data)
    {
        _data = data;
        IsVisible = true;
        Bounds = new SKRect(x, y + 20, x + 1, y + 21);
    }
    
    public void ApplyTheme(EditorTheme theme) => _theme = theme;

    public void Render(SKCanvas canvas, EditorTheme theme)
    {
        if (!IsVisible || _data == null || !_data.Signatures.Any()) return;

        using var bgPaint = new SKPaint { Color = theme.ExplorerBackground.WithAlpha(250), IsAntialias = true };
        using var borderPaint = new SKPaint { Color = theme.LineHighlight, Style = SKPaintStyle.Stroke, StrokeWidth = 1 };
        using var textPaint = new SKPaint { Color = theme.Foreground, IsAntialias = true };
        using var methodPaint = new SKPaint { Color = theme.Method, IsAntialias = true };
        using var activeParamPaint = new SKPaint { Color = theme.TabActiveAccent, IsAntialias = true }; 

        float padding = 8;
        float lineHeight = _font.Size + 8;
        float totalHeight = (_data.Signatures.Count * lineHeight) + (padding * 2);
        
        float maxWidth = 200; 
        foreach (var sig in _data.Signatures)
        {
            float w = _font.MeasureText(sig.Label) + 50; 
            if (w > maxWidth) maxWidth = w;
        }

        var rect = new SKRect(Bounds.Left, Bounds.Top, Bounds.Left + maxWidth + (padding * 2), Bounds.Top + totalHeight);
        
        canvas.DrawRect(rect.Left + 4, rect.Top + 4, rect.Width, rect.Height, new SKPaint { Color = SKColors.Black.WithAlpha(60) });
        
        canvas.DrawRect(rect, bgPaint);
        canvas.DrawRect(rect, borderPaint);

        float currentY = rect.Top + padding + _font.Size;

        foreach (var sig in _data.Signatures)
        {
            float currentX = rect.Left + padding;

            string prefix = sig.Label.Split('(')[0] + "(";
            canvas.DrawText(prefix, currentX, currentY, _font, methodPaint); // Usa cor de método
            currentX += _font.MeasureText(prefix);

            for (int i = 0; i < sig.Parameters.Count; i++)
            {
                var param = sig.Parameters[i];
                
                bool isActive = (i == _data.ActiveParameter);
                var fontToUse = isActive ? _boldFont : _font;
                var paintToUse = isActive ? activeParamPaint : textPaint;

                string paramText = param.Display;
                
                if (i < sig.Parameters.Count - 1) paramText += ", ";

                canvas.DrawText(paramText, currentX, currentY, fontToUse, paintToUse);
                currentX += fontToUse.MeasureText(paramText);
            }

            canvas.DrawText(")", currentX, currentY, _font, methodPaint);

            if (!string.IsNullOrEmpty(sig.Documentation))
            {
               // Lógica futura para renderizar docs
            }

            currentY += lineHeight;
        }
    }
}
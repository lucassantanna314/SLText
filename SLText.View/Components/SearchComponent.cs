using SkiaSharp;
using SLText.View.Styles;

namespace SLText.View.Components;

public class SearchComponent
{
    public bool IsVisible { get; set; }
    public string SearchText { get; private set; } = "";
    public bool IsFilesMode { get; set; } 
    
    private SKRect _bounds;
    private readonly SKFont _font;
    private EditorTheme _theme = EditorTheme.Dark;
    
    public void ApplyTheme(EditorTheme theme) => _theme = theme;
    
    public SearchComponent()
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

        _font = new SKFont(typeface, 14) 
        { 
            Edging = SKFontEdging.SubpixelAntialias, 
            Subpixel = true 
        };
    }

    public void Render(SKCanvas canvas, SKRect windowBounds, EditorTheme theme)
    {
        if (!IsVisible) return;

        float width = 350; 
        float height = 45;
        
        _bounds = new SKRect(
            windowBounds.Right - width - 20, 
            windowBounds.Top + 20, 
            windowBounds.Right - 20, 
            windowBounds.Top + 20 + height
        );

        using var bgPaint = new SKPaint { Color = theme.Background, IsAntialias = true };
        canvas.DrawRoundRect(_bounds, 6, 6, bgPaint);

        using var borderPaint = new SKPaint
        {
            Color = theme.LineHighlight, 
            Style = SKPaintStyle.Stroke, 
            StrokeWidth = 1, 
            IsAntialias = true
        };
        
        canvas.DrawRoundRect(_bounds, 6, 6, borderPaint);

        using var textPaint = new SKPaint { Color = theme.Foreground, IsAntialias = true };
        
        string prefix = IsFilesMode ? "Search Files: " : "Find: ";
        string fullText = prefix + SearchText + "_"; // Cursor visual simples
        
        canvas.DrawText(fullText, _bounds.Left + 15, _bounds.MidY + (_font.Size / 3), _font, textPaint);
    }

    public void HandleInput(string key, bool backspace)
    {
        if (backspace && SearchText.Length > 0) 
            SearchText = SearchText.Substring(0, SearchText.Length - 1);
        else if (key.Length == 1) 
            SearchText += key;
    }

    public void Clear() => SearchText = "";
}
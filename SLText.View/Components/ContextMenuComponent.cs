using SkiaSharp;
using SLText.View.Styles;
namespace SLText.Components;

public class ContextMenuItem
{
    public string Label { get; set; } = "";
    public string Shortcut { get; set; } = "";
    public Action Action { get; set; } = () => { };
}

public class ContextMenuComponent : View.Abstractions.IComponent
{
    public SKRect Bounds { get; set; }
    public bool IsVisible { get; set; }
    public List<ContextMenuItem> Items { get; set; } = new();
    private readonly SKFont _font;
    private readonly SKFont _shortcutFont;
    private EditorTheme _theme = EditorTheme.Dark;
    private int _hoveredIndex = -1;

    public ContextMenuComponent()
    {
        string fontPath = Path.Combine(AppContext.BaseDirectory, "Assets", "JetBrainsMono-Regular.ttf");
        var typeface = File.Exists(fontPath) ? SKTypeface.FromFile(fontPath) : SKTypeface.FromFamilyName("monospace");
        _font = new SKFont(typeface, 13);
        _shortcutFont = new SKFont(typeface, 11);
    }

    public void Show(float x, float y, List<ContextMenuItem> items)
    {
        Items = items;
        IsVisible = true;

        float itemHeight = 28;
        float width = 200;
        float height = items.Count * itemHeight + 10;

        Bounds = new SKRect(x, y, x + width, y + height);
    }

    public void ApplyTheme(EditorTheme theme) => _theme = theme;

    public void OnMouseMove(float x, float y)
    {
        if (!IsVisible || !Bounds.Contains(x, y)) { _hoveredIndex = -1; return; }
        
        float relY = y - Bounds.Top - 5;
        _hoveredIndex = Math.Clamp((int)(relY / 28), 0, Items.Count - 1);
    }

    public bool HandleClick(float x, float y)
    {
        if (!IsVisible) return false;

        if (Bounds.Contains(x, y) && _hoveredIndex >= 0 && _hoveredIndex < Items.Count)
        {
            var item = Items[_hoveredIndex];
            IsVisible = false;
            item.Action?.Invoke();
            return true;
        }

        IsVisible = false;
        return false;
    }

    public void Render(SKCanvas canvas)
    {
        if (!IsVisible || Items.Count == 0) return;

        using var shadowPaint = new SKPaint { Color = SKColors.Black.WithAlpha(80) };
        canvas.DrawRect(Bounds.Left + 4, Bounds.Top + 4, Bounds.Width, Bounds.Height, shadowPaint);

        using var bgPaint = new SKPaint { Color = _theme.StatusBarBackground, IsAntialias = true };
        using var borderPaint = new SKPaint { Color = _theme.LineHighlight, Style = SKPaintStyle.Stroke, StrokeWidth = 1 };
        
        canvas.DrawRoundRect(Bounds, 6, 6, bgPaint);
        canvas.DrawRoundRect(Bounds, 6, 6, borderPaint);

        using var textPaint = new SKPaint { Color = _theme.Foreground, IsAntialias = true };
        using var hoverPaint = new SKPaint { Color = _theme.ExplorerSelection };

        float y = Bounds.Top + 22;

        for (int i = 0; i < Items.Count; i++)
        {
            var item = Items[i];
            var itemRect = new SKRect(Bounds.Left + 2, y - 16, Bounds.Right - 2, y + 8);

            if (i == _hoveredIndex)
            {
                canvas.DrawRoundRect(itemRect, 4, 4, hoverPaint);
            }

            canvas.DrawText(item.Label, Bounds.Left + 12, y, _font, textPaint);

            if (!string.IsNullOrEmpty(item.Shortcut))
            {
                textPaint.Color = _theme.Foreground.WithAlpha(130);
                canvas.DrawText(item.Shortcut, Bounds.Right - 60, y, _shortcutFont, textPaint);
                textPaint.Color = _theme.Foreground;
            }

            y += 28;
        }
    }

    public void Update(double deltaTime) { }
}

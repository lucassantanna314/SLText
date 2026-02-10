using SkiaSharp;
using SLText.View.Styles;

namespace SLText.View.Components;

public class AutocompleteComponent
{
    public SKRect Bounds { get; set; }
    public bool IsVisible { get; set; }
    
    private List<string> _items = new();
    private int _selectedIndex = 0;
    
    // Controle de Rolagem
    private int _scrollIndex = 0; 
    private const float ItemHeight = 25f;
    private const float MaxHeight = 250f;

    private SKFont _font;
    private EditorTheme _theme = EditorTheme.Dark;

    public AutocompleteComponent(SKFont font)
    {
        _font = font;
    }
    
    public void ApplyTheme(EditorTheme theme) => _theme = theme;

    public void Show(float x, float y, List<string> items)
    {
        _items = items;
        _selectedIndex = 0;
        _scrollIndex = 0; // Reseta a rolagem
        IsVisible = _items.Any();
    
        float width = 300; 
        // Calcula altura baseada nos itens, mas trava no MaxHeight
        float height = Math.Min(items.Count * ItemHeight, MaxHeight); 
    
        Bounds = new SKRect(x, y, x + width, y + height);
    }

    public void Render(SKCanvas canvas, EditorTheme theme)
    {
        if (!IsVisible) return;

        using var bgPaint = new SKPaint { Color = theme.StatusBarBackground.WithAlpha(250), IsAntialias = true }; // Um pouco mais opaco
        using var borderPaint = new SKPaint { Color = theme.LineHighlight, Style = SKPaintStyle.Stroke };
        
        canvas.DrawRect(Bounds.Left + 2, Bounds.Top + 2, Bounds.Width, Bounds.Height, new SKPaint { Color = SKColors.Black.WithAlpha(50) });
        
        canvas.DrawRect(Bounds, bgPaint);
        canvas.DrawRect(Bounds, borderPaint);

        int visibleCount = (int)(Bounds.Height / ItemHeight);
        
        int endIndex = Math.Min(_items.Count, _scrollIndex + visibleCount);

        canvas.Save();
        canvas.ClipRect(Bounds); 

        for (int i = _scrollIndex; i < endIndex; i++)
        {
            float relativeY = Bounds.Top + ((i - _scrollIndex) * ItemHeight);
            
            var itemRect = new SKRect(Bounds.Left, relativeY, Bounds.Right - 10, relativeY + ItemHeight); // -10 para dar espaço à scrollbar
            
            if (i == _selectedIndex)
            {
                using var selectPaint = new SKPaint { Color = theme.SelectionBackground };
                canvas.DrawRect(new SKRect(Bounds.Left, relativeY, Bounds.Right, relativeY + ItemHeight), selectPaint);
            }

            using var textPaint = new SKPaint { Color = theme.Foreground, IsAntialias = true };
            
            float textY = relativeY + (ItemHeight / 2) + (_font.Size / 2) - 2; 
            canvas.DrawText(_items[i], itemRect.Left + 8, textY, _font, textPaint);
        }

        canvas.Restore();

        if (_items.Count > visibleCount)
        {
            DrawScrollbar(canvas, theme, visibleCount);
        }
    }

    private void DrawScrollbar(SKCanvas canvas, EditorTheme theme, int visibleCount)
    {
        float scrollbarWidth = 6;
        float padding = 2;

        float trackHeight = Bounds.Height - (padding * 2);
        float trackX = Bounds.Right - scrollbarWidth - padding;
        float trackY = Bounds.Top + padding;

        float contentRatio = (float)visibleCount / _items.Count;
        float thumbHeight = Math.Max(20, trackHeight * contentRatio); 
        
        int totalScrollableItems = _items.Count - visibleCount;
        if (totalScrollableItems <= 0) return;
        
        float scrollProgress = (float)_scrollIndex / totalScrollableItems;
        float thumbY = trackY + (scrollProgress * (trackHeight - thumbHeight));

        using var thumbPaint = new SKPaint 
        { 
            Color = theme.LineHighlight, 
            IsAntialias = true,
            Style = SKPaintStyle.Fill
        };

        var thumbRect = new SKRect(trackX, thumbY, trackX + scrollbarWidth, thumbY + thumbHeight);
        canvas.DrawRoundRect(thumbRect, 3, 3, thumbPaint);
    }

    public void MoveSelection(int delta)
    {
        if (_items.Count == 0) return;

        _selectedIndex = Math.Clamp(_selectedIndex + delta, 0, _items.Count - 1);

        int visibleCount = (int)(Bounds.Height / ItemHeight);

        if (_selectedIndex < _scrollIndex)
        {
            _scrollIndex = _selectedIndex;
        }
        else if (_selectedIndex >= _scrollIndex + visibleCount)
        {
            _scrollIndex = _selectedIndex - visibleCount + 1;
        }
        
        _scrollIndex = Math.Clamp(_scrollIndex, 0, Math.Max(0, _items.Count - visibleCount));
    }

    public string? GetCurrentItem() => _items.Count > 0 ? _items[_selectedIndex] : null;
}
using SkiaSharp;
using SLText.Core.Engine;
using SLText.View.Abstractions;
using SLText.View.Styles;

namespace SLText.View.Components;

public class TabComponent : IComponent
{
    public SKRect Bounds { get; set; }
    private readonly TabManager _tabManager;
    private readonly SKFont _font;
    private EditorTheme _theme = EditorTheme.Dark;
    
    private const float TabMinWidth = 130;
    private const float TabHeight = 35;
    
    private float _scrollX = 0;
    public float ScrollX { get => _scrollX; set => _scrollX = value; }

    public TabComponent(TabManager tabManager)
    {
        _tabManager = tabManager;
        
        string fontPath = Path.Combine(AppContext.BaseDirectory, "Assets", "JetBrainsMono-Regular.ttf");
        var typeface = File.Exists(fontPath) ? SKTypeface.FromFile(fontPath) : SKTypeface.FromFamilyName("monospace");
        _font = new SKFont(typeface, 12);
    }

    public void ApplyTheme(EditorTheme theme) => _theme = theme;

    public void Render(SKCanvas canvas)
    {
        if (_tabManager.Tabs.Count == 0) return;

        using var barBg = new SKPaint { Color = _theme.StatusBarBackground };
        canvas.DrawRect(Bounds, barBg);

        canvas.Save();
        canvas.ClipRect(Bounds);
        canvas.Translate(-_scrollX, 0);

        float currentX = Bounds.Left + 5;

        for (int i = 0; i < _tabManager.Tabs.Count; i++)
        {
            var tab = _tabManager.Tabs[i];
            bool isActive = i == _tabManager.ActiveTabIndex;

            var tabRect = new SKRect(currentX, Bounds.Top + 5, currentX + TabMinWidth, Bounds.Bottom);

            using (var tabPaint = new SKPaint { IsAntialias = true })
            {
                if (tab.IsDirty)
                {
                    tabPaint.Color = _theme.TabDirtyBackground;
                }
                else
                {
                    tabPaint.Color = isActive ? _theme.Background : _theme.LineHighlight.WithAlpha(150);
                }

                canvas.DrawRoundRect(tabRect, 4, 4, tabPaint);
            }

            using (var textPaint = new SKPaint { IsAntialias = true })
            {
                if (tab.IsDirty)
                    textPaint.Color = _theme.TabDirtyForeground;
                else
                    textPaint.Color = isActive ? _theme.Foreground : _theme.Foreground.WithAlpha(150);
                
                float textY = tabRect.MidY + (_font.Size / 3);

                string dirtyPrefix = tab.IsDirty ? "* " : "";
                string title = dirtyPrefix + tab.Title;

                if (_font.MeasureText(title) > TabMinWidth - 40)
                {
                    title = title.Substring(0, Math.Min(title.Length, 10)) + "...";
                }

                canvas.DrawText(title, tabRect.Left + 10, textY, _font, textPaint);

                if (isActive)
                {
                    using var accentPaint = new SKPaint { Color = _theme.TabActiveAccent };
                    var accentRect = new SKRect(tabRect.Left, tabRect.Bottom - 3, tabRect.Right, tabRect.Bottom);
                    canvas.DrawRect(accentRect, accentPaint);

                    canvas.DrawText("×", tabRect.Right - 20, textY, _font, textPaint);
                }
            }

            currentX += TabMinWidth + 2;
        }

        canvas.Restore();
    }

    public int GetTabIndexAt(float x, float y)
    {
        if (!Bounds.Contains(x, y)) return -1;

        float adjustedX = x - Bounds.Left + _scrollX;
        int index = (int)(adjustedX / (TabMinWidth + 2));

        if (index >= 0 && index < _tabManager.Tabs.Count) return index;
        return -1;
    }

    public void ApplyScroll(float deltaX)
    {
        float totalWidth = _tabManager.Tabs.Count * (TabMinWidth + 2);
        float maxScroll = Math.Max(0, totalWidth - Bounds.Width + 20);
        _scrollX = Math.Clamp(_scrollX + deltaX, 0, maxScroll);
    }
    
    public void EnsureActiveTabVisible()
    {
        if (_tabManager.ActiveTabIndex == -1) return;

        float tabStartX = 5 + (_tabManager.ActiveTabIndex * (TabMinWidth + 2));
        float tabEndX = tabStartX + TabMinWidth;

        if (tabStartX < _scrollX)
        {
            _scrollX = tabStartX - 10; 
        }
        else if (tabEndX > _scrollX + Bounds.Width)
        {
            _scrollX = tabEndX - Bounds.Width + 10;
        }

        ApplyScroll(0); 
    }

    public float GetRequiredHeight() => TabHeight;
    public void Update(double deltaTime) { }
}
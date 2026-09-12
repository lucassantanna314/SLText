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

    /// <summary>Left padding before the first tab, and the horizontal period of the strip.</summary>
    private const float TabStartOffset = 5;
    private const float TabStride = TabMinWidth + 2;

    /// <summary>Close-button hit box, in tab-local coordinates. Rendered glyph sits at ~110.</summary>
    private const float CloseButtonStart = TabMinWidth - 26;
    private const float CloseButtonEnd = TabMinWidth - 6;
    private const float CloseButtonGlyphX = TabMinWidth - 20;
    
    private float _scrollX = 0;

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

        float currentX = Bounds.Left + TabStartOffset;

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

                    canvas.DrawText("×", tabRect.Left + CloseButtonGlyphX, textY, _font, textPaint);
                }
            }

            currentX += TabStride;
        }
        
        using var borderPaint = new SKPaint { Color = _theme.LineHighlight.WithAlpha(100) };
        canvas.DrawLine(Bounds.Left, Bounds.Bottom, Bounds.Right, Bounds.Bottom, borderPaint);

        canvas.Restore();
    }

    public int GetTabIndexAt(float x, float y)
    {
        if (!Bounds.Contains(x, y)) return -1;

        float adjustedX = x - Bounds.Left + _scrollX - TabStartOffset;
        if (adjustedX < 0) return -1;

        int index = (int)(adjustedX / TabStride);
        if (index < 0 || index >= _tabManager.Tabs.Count) return -1;

        // Reject clicks that land in the gap between two tabs.
        float offsetInTab = adjustedX - index * TabStride;
        if (offsetInTab > TabMinWidth) return -1;

        return index;
    }

    /// <summary>
    /// True when the point is over the close button of the active tab.
    /// </summary>
    /// <remarks>
    /// The old hit test lived in the window layer and computed
    /// <c>(x - Bounds.Left) % (TabMinWidth + 2) &gt; 100</c>. That ignored <see cref="_scrollX"/>, so
    /// it drifted once the strip was scrolled, and it matched the right-hand edge of *every* tab -
    /// clicking the edge of an inactive tab selected it and immediately closed it.
    /// </remarks>
    public bool IsCloseButtonAt(float x, float y)
    {
        if (!Bounds.Contains(x, y)) return false;

        int index = GetTabIndexAt(x, y);
        if (index == -1 || index != _tabManager.ActiveTabIndex) return false;

        float adjustedX = x - Bounds.Left + _scrollX - TabStartOffset;
        float offsetInTab = adjustedX - index * TabStride;

        return offsetInTab >= CloseButtonStart && offsetInTab <= CloseButtonEnd;
    }

    public void ResetScroll()
    {
        _scrollX = 0;
    }

    public void ApplyScroll(float deltaX)
    {
        float totalWidth = TabStartOffset + _tabManager.Tabs.Count * TabStride;
        float maxScroll = Math.Max(0, totalWidth - Bounds.Width + 20);
        _scrollX = Math.Clamp(_scrollX + deltaX, 0, maxScroll);
    }
    
    public void EnsureActiveTabVisible()
    {
        if (_tabManager.ActiveTabIndex == -1) return;

        float tabStartX = TabStartOffset + (_tabManager.ActiveTabIndex * TabStride);
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
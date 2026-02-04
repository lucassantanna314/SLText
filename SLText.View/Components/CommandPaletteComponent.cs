using SkiaSharp;
using SLText.Core.Engine;
using SLText.View.Abstractions;
using SLText.View.Styles;

namespace SLText.View.Components;

public class CommandPaletteComponent : IComponent
{
    public SKRect Bounds { get; set; }
    public bool IsVisible { get; set; }
    private EditorTheme _theme = EditorTheme.Dark;
    
    private string _filterText = "";
    private List<EditorCommand> _allCommands = new();
    private List<EditorCommand> _filteredCommands = new();
    private int _selectedIndex = 0;
    
    private readonly SKFont _font;
    private readonly SKFont _shortcutFont;
    
    public void ApplyTheme(EditorTheme theme) 
    {
        _theme = theme;
    }
    
    public CommandPaletteComponent()
    {
        string fontPath = Path.Combine(AppContext.BaseDirectory, "Assets", "JetBrainsMono-Regular.ttf");
        var typeface = File.Exists(fontPath) ? SKTypeface.FromFile(fontPath) : SKTypeface.FromFamilyName("monospace");
        _font = new SKFont(typeface, 14);
        _shortcutFont = new SKFont(typeface, 11);
    }
    
    public void LoadCommands(List<EditorCommand> commands)
    {
        _allCommands = commands;
        _filterText = "";
        _selectedIndex = 0;
        ApplyFilter();
    }
    
    public void HandleInput(string text, bool backspace)
    {
        if (backspace) { if (_filterText.Length > 0) _filterText = _filterText[..^1]; }
        else { _filterText += text; }
        ApplyFilter();
    }
    
    private void ApplyFilter()
    {
        _filteredCommands = _allCommands
            .Where(c => c.FullName.Contains(_filterText, StringComparison.OrdinalIgnoreCase))
            .ToList();
        
        _selectedIndex = Math.Clamp(_selectedIndex, 0, Math.Max(0, _filteredCommands.Count - 1));
    }
    
    public void MoveSelection(int delta)
    {
        if (_filteredCommands.Count == 0) return;
        _selectedIndex = (_selectedIndex + delta + _filteredCommands.Count) % _filteredCommands.Count;
    }
    
    public EditorCommand? GetSelectedCommand()
    {
        if (_selectedIndex >= 0 && _selectedIndex < _filteredCommands.Count)
            return _filteredCommands[_selectedIndex];
        return null;
    }

    public void Render(SKCanvas canvas)
    {
        if (!IsVisible) return;

        var windowBounds = Bounds;
        
        using var overlayPaint = new SKPaint { Color = SKColors.Black.WithAlpha(150) };
        canvas.DrawRect(windowBounds, overlayPaint);
        
        float width = 500;
        float height = Math.Min(400, 40 + (_filteredCommands.Count * 35));
        var panelRect = new SKRect(
            windowBounds.MidX - width / 2,
            windowBounds.Top + 100,
            windowBounds.MidX + width / 2,
            windowBounds.Top + 100 + height
        );

        using var paint = new SKPaint { Color = _theme.StatusBarBackground, IsAntialias = true };
        canvas.DrawRoundRect(panelRect, 8, 8, paint);
        
        float y = panelRect.Top + 30;
        using var textPaint = new SKPaint { Color = _theme.Foreground, IsAntialias = true };
        canvas.DrawText($"> {_filterText}_", panelRect.Left + 20, y, _font, textPaint);
        
        paint.Color = _theme.LineHighlight;
        canvas.DrawLine(panelRect.Left, y + 10, panelRect.Right, y + 10, paint);
        
        y += 35;
        for (int i = 0; i < _filteredCommands.Count; i++)
        {
            var cmd = _filteredCommands[i];
            bool isSelected = i == _selectedIndex;

            if (isSelected)
            {
                paint.Color = _theme.ExplorerSelection;
                canvas.DrawRect(new SKRect(panelRect.Left, y - 22, panelRect.Right, y + 10), paint);
            }

            textPaint.Color = isSelected 
                ? (_theme.Background.Red > 128 ? SKColors.Black : SKColors.White) 
                : _theme.Foreground;

            canvas.DrawText(cmd.FullName, panelRect.Left + 20, y, _font, textPaint);
            
            if (!string.IsNullOrEmpty(cmd.Shortcut))
            {
                textPaint.Color = _theme.Foreground.WithAlpha(120);
                canvas.DrawText(cmd.Shortcut, panelRect.Right - 120, y, _shortcutFont, textPaint);
            }

            y += 30;
            if (y > panelRect.Bottom - 10) break;
        }
    }

    public void Update(double deltaTime) { }
}
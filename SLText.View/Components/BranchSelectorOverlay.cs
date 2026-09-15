using SkiaSharp;
using SLText.Core.Engine.Git;
using SLText.Core.Engine.Model;
using SLText.View.Styles;
using System.Text;

namespace SLText.View.Components;

/// <summary>Branch selector overlay — local &amp; remote branches, switching &amp; creating.</summary>
public class BranchSelectorOverlay
{
    public bool IsVisible { get; set; }
    public SKRect Bounds { get; private set; }

    // Data — populated by WindowManager after async load
    internal List<string> LocalBranches { get; set; } = new();
    internal List<RemoteBranch> RemoteBranches { get; set; } = new();
    internal string? CurrentBranchName { get; set; }

    // Selection / scrolling
    private int _selectedIndex = 0;
    private int _scrollOffset = 0;
    private const float ItemHeight = 24f;

    // New-branch input state
    private bool _isCreatingBranch;
    private string _newBranchName = "";
    private int _createCursorPos = 0;

    // UI constants
    private readonly SKFont _font;
    private readonly SKFont _fontBold;
    private float _width = 450f;
    private const float HeaderHeight = 80f;
    private const float SectionGap = 6f;

    private EditorTheme _theme = EditorTheme.Dark;

    /// <summary>Action invoked when a branch is selected (switched or created).</summary>
    public Action<string>? OnBranchSelected { get; set; }

    /// <summary>Action invoked when overlay is dismissed without selection.</summary>
    public Action? OnDismissed { get; set; }

    /// <summary>Reset internal state for re-opening.</summary>
    public void ResetState()
    {
        _selectedIndex = 0;
        _scrollOffset = 0;
        _isCreatingBranch = false;
        _newBranchName = "";
        _createCursorPos = 0;
    }

    public BranchSelectorOverlay()
    {
        string fontPath = Path.Combine(AppContext.BaseDirectory, "Assets", "JetBrainsMono-Regular.ttf");
        SKTypeface typeface;
        if (File.Exists(fontPath))
            typeface = SKTypeface.FromFile(fontPath);
        else
            typeface = SKTypeface.FromFamilyName("monospace", SKFontStyle.Normal);

        _font = new SKFont(typeface, 13);
        _fontBold = new SKFont(typeface, 13) { Embolden = true };
    }

    public void ApplyTheme(EditorTheme theme) => _theme = theme;

    /// <summary>Open the overlay with data from the git service.</summary>
    public async Task ShowAsync(string? currentBranch, Func<Task<List<string>>> loadLocal, Func<Task<List<RemoteBranch>>> loadRemote, SKRect windowBounds)
    {
        CurrentBranchName = currentBranch;
        _isCreatingBranch = false;
        _newBranchName = "";
        _createCursorPos = 0;
        _selectedIndex = 0;
        _scrollOffset = 0;

        var localTask = loadLocal();
        var remoteTask = loadRemote();
        await Task.WhenAll(localTask, remoteTask);

        LocalBranches = await loadLocal();
        RemoteBranches = await loadRemote();

        int totalItems = CountTotalItems();
        if (totalItems == 0)
        {
            IsVisible = false;
            return;
        }

        _selectedIndex = ClampIndex(_selectedIndex, totalItems);
        ComputeBounds(windowBounds);
        IsVisible = true;
    }

    private int CountTotalItems()
    {
        int count = LocalBranches.Count;
        if (!_isCreatingBranch && RemoteBranches.Count > 0)
            count += 1; // separator line item
        count += RemoteBranches.Count;
        if (_isCreatingBranch)
            count += 1; // input row item
        return count;
    }

    /// <summary>Get label for an item index.</summary>
    public string GetItemLabel(int index, out bool isCurrent, out bool isRemote, out bool isInputRow)
    {
        isCurrent = false;
        isRemote = false;
        isInputRow = false;

        // Local branches section
        if (index < LocalBranches.Count)
        {
            var name = LocalBranches[index];
            isCurrent = string.Equals(name, CurrentBranchName, StringComparison.Ordinal);
            return $"  {(isCurrent ? "●" : "○")} {name}";
        }
        index -= LocalBranches.Count;

        // Separator (skip this virtual item)
        if (!_isCreatingBranch && RemoteBranches.Count > 0)
        {
            if (index == 0)
            {
                isInputRow = true; // special rendering for separator
                return "--- REMOTE BRANCHES ---";
            }
            index--;
        }

        // Remote branches
        if (index < RemoteBranches.Count)
        {
            var rb = RemoteBranches[index];
            isRemote = true;
            string tracking = !string.IsNullOrEmpty(rb.LocalBranchName) ? $" → {rb.LocalBranchName}" : " (no local)";
            return $"  ↗ {rb.Name}{tracking}";
        }

        // Create-branch input row
        if (_isCreatingBranch)
        {
            isInputRow = true;
            string cursorMark = _createCursorPos < _newBranchName.Length ? "▌" : "";
            return $"  + {_newBranchName}{cursorMark}";
        }

        return "";
    }

    /// <summary>Render the overlay.</summary>
    public void Render(SKCanvas canvas, SKRect windowBounds)
    {
        if (!IsVisible) return;

        // Semi-transparent backdrop
        using var dimPaint = new SKPaint { Color = new SKColor(0, 0, 0, 120) };
        canvas.DrawRect(windowBounds, dimPaint);

        // Shadow
        using var shadowPaint = new SKPaint { Color = SKColors.Black.WithAlpha(80), IsAntialias = true };
        canvas.DrawRoundRect(new SKRect(Bounds.Left - 2, Bounds.Top - 2, Bounds.Right + 2, Bounds.Bottom + 2), 10, 10, shadowPaint);

        // Panel background
        using var bgPaint = new SKPaint { Color = _theme.Background, IsAntialias = true };
        canvas.DrawRoundRect(Bounds, 8, 8, bgPaint);

        // Border
        using var borderPaint = new SKPaint { Color = _theme.SelectionBackground, Style = SKPaintStyle.Stroke, StrokeWidth = 1, IsAntialias = true };
        canvas.DrawRoundRect(Bounds, 8, 8, borderPaint);

        // Title area
        using var titlePaint = new SKPaint { Color = _theme.Foreground, IsAntialias = true };
        canvas.DrawText("Select Branch", Bounds.Left + 16, Bounds.Top + 26, _fontBold, titlePaint);

        if (CurrentBranchName != null)
        {
            using var subtitlePaint = new SKPaint { Color = _theme.Comment, IsAntialias = true };
            canvas.DrawText($"Current: {CurrentBranchName}", Bounds.Left + 16, Bounds.Top + 46, _font, subtitlePaint);
        }

        using var textPaint = new SKPaint { Color = _theme.Foreground, IsAntialias = true };
        using var selectBgPaint = new SKPaint { Color = _theme.SelectionBackground, IsAntialias = true };
        using var mutedPaint = new SKPaint { Color = _theme.Comment, IsAntialias = true };

        int totalItems = CountTotalItems();
        int visibleCount = (int)((Bounds.Height - HeaderHeight) / ItemHeight);
        visibleCount = Math.Max(1, visibleCount);

        // Clip to list area
        float listTop = Bounds.Top + HeaderHeight;
        float listBottom = Bounds.Bottom - 8;
        canvas.Save();
        canvas.ClipRect(new SKRect(Bounds.Left, listTop, Bounds.Right, listBottom));

        for (int i = _scrollOffset; i < Math.Min(totalItems, _scrollOffset + visibleCount); i++)
        {
            float y = listTop + (i - _scrollOffset) * ItemHeight;

            if (i == _selectedIndex)
                canvas.DrawRect(new SKRect(Bounds.Left + 1, y, Bounds.Right - 1, y + ItemHeight), selectBgPaint);

            string label = GetItemLabel(i, out var isCurrent, out var isRemote, out var isInputRow);

            // Use appropriate color
            SKPaint paintToUse = textPaint;
            if (isCurrent)
                paintToUse = new SKPaint { Color = _theme.ExplorerItemActive, IsAntialias = true };
            else if (isRemote)
                paintToUse = new SKPaint { Color = _theme.Type, IsAntialias = true };
            else if (label.StartsWith("---"))
                paintToUse = mutedPaint;
            else if (isInputRow)
                paintToUse = new SKPaint { Color = _theme.Keyword, IsAntialias = true };

            canvas.DrawText(label, Bounds.Left + 16, y + 16, _font, paintToUse);
        }

        canvas.Restore();

        // Scrollbar
        if (totalItems > visibleCount)
            DrawScrollbar(canvas, visibleCount, totalItems);

        // Footer hint
        using var footerPaint = new SKPaint { Color = _theme.Comment, IsAntialias = true };
        string footer = _isCreatingBranch ? "Enter to create · Esc to cancel" : "↑↓ Navigate · Enter to switch · Esc to dismiss";
        canvas.DrawText(footer, Bounds.Left + 16, Bounds.Bottom - 12, _font, footerPaint);
    }

    internal void ComputeBounds(SKRect windowBounds)
    {
        int totalItems = CountTotalItems();
        int visibleCount = 8; // Default visible items
        float height = HeaderHeight + (visibleCount * ItemHeight) + 30; // header + items + footer

        // Clamp height to window
        height = Math.Min(height, windowBounds.Height * 0.7f);

        float x = Math.Max(windowBounds.Left + 20, windowBounds.MidX - _width / 2);
        x = Math.Min(x, windowBounds.Right - _width - 20);

        Bounds = new SKRect(x, windowBounds.Top + 40, x + _width, windowBounds.Top + 40 + height);
        Bounds = SKRect.Create(
            Math.Clamp(Bounds.Left, windowBounds.Left, windowBounds.Right - _width),
            Math.Clamp(Bounds.Top, windowBounds.Top, windowBounds.Bottom - 40),
            _width,
            Math.Clamp(Bounds.Height, HeaderHeight + ItemHeight + 30, windowBounds.Height * 0.7f)
        );
    }

    private void DrawScrollbar(SKCanvas canvas, int visibleCount, int totalItems)
    {
        float scrollbarWidth = 6f;
        float padding = 2f;
        float listTop = Bounds.Top + HeaderHeight;
        float trackHeight = Bounds.Height - HeaderHeight - 20;
        float trackX = Bounds.Right - scrollbarWidth - padding;
        float trackY = listTop + padding;

        float contentRatio = (float)visibleCount / totalItems;
        float thumbHeight = Math.Max(20f, trackHeight * contentRatio);

        int totalScrollable = totalItems - visibleCount;
        if (totalScrollable <= 0) return;

        float scrollProgress = (float)_scrollOffset / totalScrollable;
        float thumbY = trackY + (scrollProgress * (trackHeight - thumbHeight));

        using var thumbPaint = new SKPaint
        {
            Color = _theme.LineHighlight,
            IsAntialias = true,
            Style = SKPaintStyle.Fill
        };

        canvas.DrawRoundRect(new SKRect(trackX, thumbY, trackX + scrollbarWidth, thumbY + thumbHeight), 3, 3, thumbPaint);
    }

    /// <summary>Handle keyboard input while overlay is visible. Returns true if consumed.</summary>
    public bool HandleKeyDown(string key, bool ctrl, bool shift)
    {
        if (!IsVisible) return false;

        // Reject modifiers for shortcut matching
        if (ctrl || shift) return true;

        int totalItems = CountTotalItems();

        if (key == "Escape")
        {
            if (_isCreatingBranch)
            {
                _isCreatingBranch = false;
                _newBranchName = "";
                _createCursorPos = 0;
                return true;
            }
            Close();
            return true;
        }

        if (key == "Up")
        {
            MoveSelection(-1);
            return true;
        }

        if (key == "Down")
        {
            MoveSelection(1);
            return true;
        }

        if (key == "Enter")
        {
            CommitSelection();
            return true;
        }

        if (key == "Tab" && _isCreatingBranch)
        {
            TryAutoCompleteBranchName();
            return true;
        }

        if (_isCreatingBranch && (key == "Left"))
        {
            _createCursorPos = Math.Max(0, _createCursorPos - 1);
            return true;
        }

        if (_isCreatingBranch && (key == "Right"))
        {
            _createCursorPos = Math.Min(_newBranchName.Length, _createCursorPos + 1);
            return true;
        }

        if (_isCreatingBranch && (key == "Backspace"))
        {
            if (_createCursorPos > 0 && _newBranchName.Length > 0)
            {
                _newBranchName = _newBranchName.Remove(_createCursorPos - 1, 1);
                _createCursorPos--;
                return true;
            }
        }

        if (_isCreatingBranch && _newBranchName.Length < 100)
        {
            // Single character insert
            if (key.Length == 1 && !char.IsControl(key[0]))
            {
                string sanitized = SanitizeBranchName(key);
                if (!string.IsNullOrEmpty(sanitized))
                {
                    _newBranchName = _newBranchName.Insert(_createCursorPos, sanitized);
                    _createCursorPos++;
                }
                return true;
            }
        }

        return false;
    }

    /// <summary>Handle mouse click. Returns true if consumed.</summary>
    public bool HandleClick(float x, float y)
    {
        if (!IsVisible) return false;

        // Check if click is inside the panel
        if (!Bounds.Contains(x, y))
        {
            Close();
            return true;
        }

        // Calculate item index from mouse Y
        float listTop = Bounds.Top + HeaderHeight;
        float relativeY = y - listTop;
        int clickedIndex = _scrollOffset + (int)(relativeY / ItemHeight);

        int totalItems = CountTotalItems();
        if (clickedIndex >= 0 && clickedIndex < totalItems)
        {
            _selectedIndex = ClampIndex(clickedIndex, totalItems);
            
            // Switch on first click
            CommitSelection();
            return true;
        }

        // Scrollbar click
        if (RemoteBranches.Count > 0 || LocalBranches.Count > 0)
        {
            int visibleCount = (int)((Bounds.Height - HeaderHeight) / ItemHeight);
            if (clickedIndex >= _scrollOffset + visibleCount)
            {
                _scrollOffset = Math.Min(totalItems - visibleCount, clickedIndex - visibleCount + 1);
                return true;
            }
        }

        return true;
    }

    /// <summary>Mouse wheel scroll support.</summary>
    public void HandleWheel(float deltaY)
    {
        if (!IsVisible) return;

        int totalItems = CountTotalItems();
        int visibleCount = (int)((Bounds.Height - HeaderHeight) / ItemHeight);
        int maxScroll = Math.Max(0, totalItems - visibleCount);

        if (deltaY > 0)
            _scrollOffset = Math.Min(maxScroll, _scrollOffset + 1);
        else if (deltaY < 0)
            _scrollOffset = Math.Max(0, _scrollOffset - 1);
    }

    private void MoveSelection(int delta)
    {
        int totalItems = CountTotalItems();
        if (totalItems == 0) return;

        _selectedIndex = ClampIndex(_selectedIndex + delta, totalItems);

        int visibleCount = (int)((Bounds.Height - HeaderHeight) / ItemHeight);
        visibleCount = Math.Max(1, visibleCount);

        // Auto-scroll
        if (_selectedIndex < _scrollOffset)
            _scrollOffset = _selectedIndex;
        else if (_selectedIndex >= _scrollOffset + visibleCount)
            _scrollOffset = _selectedIndex - visibleCount + 1;

        _scrollOffset = Math.Clamp(_scrollOffset, 0, Math.Max(0, CountTotalItems() - visibleCount));
    }

    private void CommitSelection()
    {
        if (_isCreatingBranch)
        {
            if (string.IsNullOrWhiteSpace(_newBranchName)) return;
            OnBranchSelected?.Invoke(_newBranchName);
            Close();
            return;
        }

        int totalItems = CountTotalItems();
        if (_selectedIndex >= totalItems) return;

        string label = GetItemLabel(_selectedIndex, out _, out var isRemote, out _);

        if (label.StartsWith("---")) return; // Don't commit separator

        // Extract branch name from label
        // Format: "  ● ○ name" or "  ↗ remote/branch → local" 
        int firstNonSpace = 0;
        while (firstNonSpace < label.Length && label[firstNonSpace] == ' ') firstNonSpace++;
        
        int emojiStart = firstNonSpace + 2; // Skip bullet character
        if (emojiStart >= label.Length) return;
        
        string branchName;
        if (isRemote)
        {
            // Format: "  ↗ name → local" — take everything after "↗ " until " →" or end
            int arrowIdx = label.IndexOf(" → ", emojiStart, StringComparison.Ordinal);
            branchName = arrowIdx > 0 ? label.Substring(emojiStart, arrowIdx - emojiStart).Trim()
                                       : label.Substring(emojiStart).Trim();
        }
        else
        {
            branchName = label.Substring(emojiStart).Trim();
        }

        if (!string.IsNullOrEmpty(branchName))
            OnBranchSelected?.Invoke(branchName);

        Close();
    }

    private void TryAutoCompleteBranchName()
    {
        if (string.IsNullOrEmpty(_newBranchName)) return;

        // Try to match against local branches
        var matches = LocalBranches
            .Where(b => b.StartsWith(_newBranchName, StringComparison.OrdinalIgnoreCase))
            .ToList();

        if (matches.Count == 1)
        {
            _newBranchName = matches[0];
            _createCursorPos = _newBranchName.Length;
        }
        else if (matches.Count > 1)
        {
            // Try to find common prefix among matches
            string common = matches[0];
            for (int i = 1; i < matches.Count; i++)
            {
                int j = 0;
                while (j < common.Length && j < matches[i].Length && common[j] == matches[i][j])
                    j++;
                common = common[..Math.Min(common.Length, j)];
            }
            if (common.Length > _newBranchName.Length)
            {
                _newBranchName = common;
                _createCursorPos = _newBranchName.Length;
            }
        }
    }

    private static string SanitizeBranchName(string input)
    {
        // Git branch name rules: allow alphanumerics, -, _, ., /
        var sb = new StringBuilder();
        foreach (char c in input)
        {
            if (char.IsLetterOrDigit(c) || "-_./".IndexOf(c) >= 0)
                sb.Append(c);
        }
        return sb.ToString();
    }

    private int ClampIndex(int index, int total) => Math.Clamp(index, 0, total - 1);

    private void Close()
    {
        IsVisible = false;
        _isCreatingBranch = false;
        _newBranchName = "";
        _createCursorPos = 0;
        OnDismissed?.Invoke();
    }
}

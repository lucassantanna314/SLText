using SkiaSharp;
using SLText.Core.Engine.Model;
using SLText.View.Abstractions;
using SLText.View.Styles;

namespace SLText.View.Components;

public class FileExplorerComponent : IComponent
{
    public SKRect Bounds { get; set; }
    public bool IsVisible { get; set; } = false;
    public float Width { get; set; } = 250;
    private EditorTheme _theme = EditorTheme.Dark;
    private readonly SKFont _font;
    
    private List<FileNode> _rootNodes = new();
    private string? _currentRootPath;
    private const float ItemHeight = 25;
    public bool HasRoot => !string.IsNullOrEmpty(_currentRootPath);
    
    private float _scrollY = 0;
    public float ScrollY { get => _scrollY; set => _scrollY = value; }
    private float _maxContentWidth = 0; 
    private string? _selectedFilePath;
    
    private float _scrollX = 0;
    public float ScrollX { get => _scrollX; set => _scrollX = value; }
    
    private bool _isDragging;
    private float _lastMouseY;
    private float _lastMouseX;
    private bool _hasMovedEnough;
    private const float DragThreshold = 5f;
    
    private bool _isDraggingVertical;
    private bool _isDraggingHorizontal;
    
    private string _searchText = "";
    public string SearchText => _searchText;
    public bool IsFocused { get; set; }
    private readonly SKRect _searchBoxHeight = new(0, 0, 0, 40);
    private List<FileNode> _filteredNodes = new();
    private List<FileNode> _flattenedVisibleNodes = new();
    private int _kbSelectedIndex = -1;
    public event Action<string>? OnFileOpenRequested;
    public event Action<FileNode>? OnFolderToggleRequested;
    private HashSet<string> _userExpandedPaths = new();
    private static readonly string[] ForbiddenFolders = { "bin", "obj", ".git", ".vs", "node_modules" };
    public void SetSelectedFile(string? path) 
    {
        _selectedFilePath = path;
        if (IsVisible) ExpandToPath(path);
    }
    
    private void ToggleNodeExpansion(FileNode node, bool expand)
    {
        node.IsExpanded = expand;
        if (expand)
        {
            _userExpandedPaths.Add(node.FullPath);
            if (node.Children.Count == 0) LoadSubNodes(node);
        }
        else
        {
            _userExpandedPaths.Remove(node.FullPath);
        }
        UpdateFlattenedNodes();
    }
    
    public void HandleMouseClick(FileNode node)
    {
        if (node.IsDirectory)
        {
            ToggleNodeExpansion(node, !node.IsExpanded);
        
            if (!node.IsExpanded) OnFolderCollapsed();
        }
        else
        {
            SetSelectedFile(node.FullPath);
        }
    }
    
    public void HandleSearchInput(string text, bool backspace)
    {
        if (backspace)
        {
            if (_searchText.Length > 0) _searchText = _searchText[..^1];
        }
        else
        {
            _searchText += text;
        }
        ApplyFilter();
    }

    public void HandleKeyDown(string key)
    {
        UpdateFlattenedNodes();

        switch (key)
        {
            case "Down":
                _kbSelectedIndex = Math.Min(_kbSelectedIndex + 1, _flattenedVisibleNodes.Count - 1);
                EnsureSelectionVisible();
                break;
            
            case "Up":
                _kbSelectedIndex = Math.Max(_kbSelectedIndex - 1, 0);
                EnsureSelectionVisible();
                break;
            
            case "Enter":
                if (_kbSelectedIndex >= 0 && _kbSelectedIndex < _flattenedVisibleNodes.Count)
                {
                    var node = _flattenedVisibleNodes[_kbSelectedIndex];
                    if (node.IsDirectory)
                    {
                        ToggleNodeExpansion(node, !node.IsExpanded);
                        UpdateFlattenedNodes();
                    }
                    else
                    {
                        _selectedFilePath = node.FullPath;
                        OnFileOpenRequested?.Invoke(node.FullPath);
                        ClearSearch();
                    }
                }
                break;
            
            case "Escape":
                IsFocused = false;
                break;
        }
    }
    
    public void ResetScroll()
    {
        _scrollY = 0;
        _scrollX = 0;
    }
    
    private void UpdateFlattenedNodes()
    {
        _flattenedVisibleNodes.Clear();
        var source = string.IsNullOrEmpty(_searchText) ? _rootNodes : _filteredNodes;
        foreach (var node in source)
        {
            FlattenRecursive(node);
        }
    }
    
    private void EnsureSelectionVisible()
    {
        if (_kbSelectedIndex < 0) return;

        float targetY = 60 + (_kbSelectedIndex * ItemHeight);
        float viewTop = _scrollY;
        float viewBottom = _scrollY + Bounds.Height - 60;

        if (targetY < viewTop)
        {
            _scrollY = targetY - 20;
        }
        else if (targetY + ItemHeight > viewBottom)
        {
            _scrollY = targetY - Bounds.Height + ItemHeight + 40;
        }
    
        _scrollY = Math.Max(0, _scrollY);
    }
    
    private void FlattenRecursive(FileNode node)
    {
        _flattenedVisibleNodes.Add(node);
        if (node.IsDirectory && node.IsExpanded)
        {
            foreach (var child in node.Children)
                FlattenRecursive(child);
        }
    }
    
    private void ApplyFilter()
    {
        if (string.IsNullOrEmpty(_searchText))
        {
            _filteredNodes = _rootNodes;
            _kbSelectedIndex = -1; 
        }
        else
        {
            _filteredNodes = FilterRecursive(_rootNodes, _searchText.ToLower());
        
            UpdateFlattenedNodes();
        
            _kbSelectedIndex = -1;
            for (int i = 0; i < _flattenedVisibleNodes.Count; i++)
            {
                if (_flattenedVisibleNodes[i].Name.Contains(_searchText, StringComparison.OrdinalIgnoreCase))
                {
                    _kbSelectedIndex = i;
                    break;
                }
            }

            if (_kbSelectedIndex == -1 && _flattenedVisibleNodes.Count > 0)
            {
                _kbSelectedIndex = 0;
            }
        }

        EnsureSelectionVisible();
        _scrollY = 0; 
    }
    
    private List<FileNode> FilterRecursive(List<FileNode> nodes, string query)
    {
        var result = new List<FileNode>();
        foreach (var node in nodes)
        {
            if (node.IsDirectory && ForbiddenFolders.Contains(node.Name.ToLower()))
                continue;
            
            bool matches = node.Name.ToLower().Contains(query);
        
            if (node.IsDirectory)
            {
                if (node.Children.Count == 0) LoadSubNodes(node);

                var matchedChildren = FilterRecursive(node.Children, query);

                if (matches || matchedChildren.Count > 0)
                {
                    node.IsExpanded = true; 
                    result.Add(node);
                }
                else
                {
                    node.IsExpanded = _userExpandedPaths.Contains(node.FullPath);
                }
            }
            else if (matches)
            {
                result.Add(node);
            }
        }
        return result;
    }
    
    public void ExpandToPath(string? fullPath)
    {
        if (string.IsNullOrEmpty(fullPath) || string.IsNullOrEmpty(_currentRootPath)) return;
        if (!fullPath.StartsWith(_currentRootPath)) return;

        string relativePath = Path.GetRelativePath(_currentRootPath, fullPath);
        string[] parts = relativePath.Split(Path.DirectorySeparatorChar, StringSplitOptions.RemoveEmptyEntries);

        List<FileNode> currentLevel = _rootNodes;

        foreach (var part in parts)
        {
            var foundNode = currentLevel.FirstOrDefault(n => n.Name.Equals(part, StringComparison.OrdinalIgnoreCase));
        
            if (foundNode != null)
            {
                if (foundNode.IsDirectory)
                {
                    foundNode.IsExpanded = true;
                    _userExpandedPaths.Add(foundNode.FullPath); 
                
                    if (foundNode.Children.Count == 0) LoadSubNodes(foundNode);
                    currentLevel = foundNode.Children;
                }
            
                if (foundNode.FullPath == fullPath)
                {
                    _selectedFilePath = foundNode.FullPath;
                    UpdateFlattenedNodes();
                    _kbSelectedIndex = _flattenedVisibleNodes.IndexOf(foundNode);
                    EnsureSelectionVisible();
                    break;
                }
            }
        }
    }
    
    public void SetRootDirectory(string path)
    {
        if (!Directory.Exists(path)) return;
        
        if (_currentRootPath == path) return;

        _currentRootPath = path;
        _scrollY = 0;
        _maxContentWidth = 0;
        Refresh();
    }
    
    private float GetTotalHeight(List<FileNode> nodes)
    {
        float height = 0;
        foreach (var node in nodes)
        {
            height += ItemHeight;
            if (node.IsDirectory && node.IsExpanded)
                height += GetTotalHeight(node.Children);
        }
        return height;
    }
    
    public void ApplyScroll(float deltaX, float deltaY)
    {
        var currentNodes = string.IsNullOrEmpty(_searchText) ? _rootNodes : _filteredNodes;
        
        float totalHeight = GetTotalHeight(currentNodes);
        float maxScrollY = Math.Max(0, totalHeight - Bounds.Height + 60);
        _scrollY = Math.Clamp(_scrollY + deltaY, 0, maxScrollY);

        float maxScrollX = Math.Max(0, _maxContentWidth - Bounds.Width + 40);
        _scrollX = Math.Clamp(_scrollX + deltaX, 0, maxScrollX);
    }
    
    public void Refresh()
    {
        if (string.IsNullOrEmpty(_currentRootPath)) return;
        _rootNodes.Clear();
        var entries = Directory.GetFileSystemEntries(_currentRootPath)
            .OrderByDescending(e => Directory.Exists(e))
            .ThenBy(e => e);

        foreach (var entry in entries)
        {
            var newNode = new FileNode(entry, 0);
        
            if (_userExpandedPaths.Contains(newNode.FullPath))
            {
                newNode.IsExpanded = true;
                LoadSubNodes(newNode);
                RestoreExpansionState(newNode.Children); 
            }
            _rootNodes.Add(newNode);
        }
        
        UpdateFlattenedNodes();
    }
    
    private HashSet<string> GetExpandedPaths(List<FileNode> nodes)
    {
        var expanded = new HashSet<string>();
        foreach (var node in nodes)
        {
            if (node.IsDirectory && node.IsExpanded)
            {
                expanded.Add(node.FullPath);
                expanded.UnionWith(GetExpandedPaths(node.Children));
            }
        }
        return expanded;
    }

    public FileExplorerComponent()
    {
        string fontPath = Path.Combine(AppContext.BaseDirectory, "Assets", "JetBrainsMono-Regular.ttf");
        var typeface = File.Exists(fontPath) ? SKTypeface.FromFile(fontPath) : SKTypeface.FromFamilyName("monospace");
        _font = new SKFont(typeface, 14);
    }
    
    public void ApplyTheme(EditorTheme theme) => _theme = theme;
    
    private void DrawFolderIcon(SKCanvas canvas, float x, float y, bool isOpen, SKPaint paint)
    {
        float size = 12;
        float ty = y - size + 2;
        paint.Color = _theme.FolderIcon;
        
        var rect = new SKRect(x, ty, x + size, ty + (size * 0.8f));
        canvas.DrawRect(rect, paint);
        canvas.DrawRect(new SKRect(x, ty - 2, x + size * 0.4f, ty), paint);
    }
    
    private void DrawFileIcon(SKCanvas canvas, float x, float y, string fileName, SKPaint paint)
    {
        paint.Color = fileName.EndsWith(".cs") ? _theme.FileIconCSharp : _theme.FileIconDefault;
        float size = 10;
        float ty = y - size + 1;
        
        var rect = new SKRect(x, ty, x + size, ty + size + 2);
        canvas.DrawRect(rect, paint);
    }
    
    public void Render(SKCanvas canvas)
    {
        if (!IsVisible) return;
        _maxContentWidth = 0;
        UpdateFlattenedNodes();
        
        using var paint = new SKPaint { Color = _theme.ExplorerBackground };
        canvas.DrawRect(Bounds, paint);
        
        var searchRect = new SKRect(Bounds.Left + 5, Bounds.Top + 5, Bounds.Right - 5, Bounds.Top + 35);
        paint.Color = _theme.StatusBarBackground;
        canvas.DrawRoundRect(searchRect, 4, 4, paint);
        
        if (IsFocused) {
            paint.Style = SKPaintStyle.Stroke;
            paint.Color = _theme.ExplorerItemActive;
            canvas.DrawRoundRect(searchRect, 4, 4, paint);
            paint.Style = SKPaintStyle.Fill;
        }

        using var textPaint = new SKPaint { Color = _theme.Foreground, IsAntialias = true, TextSize = 13 };
        canvas.DrawText(string.IsNullOrEmpty(_searchText) && !IsFocused ? "Search..." : _searchText, 
            searchRect.Left + 10, searchRect.MidY + 5, _font, textPaint);
        
        canvas.Save();
        var listBounds = new SKRect(Bounds.Left, Bounds.Top + 40, Bounds.Right, Bounds.Bottom);
        canvas.ClipRect(listBounds);
        canvas.Translate(0, -_scrollY);
        
        float yOffset = Bounds.Top + 60;
        
        var nodesToRender = string.IsNullOrEmpty(_searchText) ? _rootNodes : _filteredNodes;
        foreach (var node in nodesToRender)
        {
            RenderNode(canvas, node, ref yOffset, textPaint);
        }
        
        canvas.Restore();
        DrawScrollbars(canvas);
        
        paint.Color = _theme.LineHighlight.WithAlpha(120);
        paint.Style = SKPaintStyle.Stroke;
        canvas.DrawLine(Bounds.Right, Bounds.Top, Bounds.Right, Bounds.Bottom, paint);
    }
    
    private void RenderNode(SKCanvas canvas, FileNode node, ref float y, SKPaint paint)
    {
        float xOffset = Bounds.Left + 15 + (node.Level * 20); 
        float textWidth = paint.MeasureText(node.Name);
        _maxContentWidth = Math.Max(_maxContentWidth, xOffset + textWidth + 50);
        
        bool isKbSelected = _kbSelectedIndex >= 0 && 
                            _kbSelectedIndex < _flattenedVisibleNodes.Count && 
                            _flattenedVisibleNodes[_kbSelectedIndex] == node;
        
        if (isKbSelected)
        {
            using var kbPaint = new SKPaint { Color = _theme.ExplorerSelection.WithAlpha(100) };
            var kbRect = new SKRect(Bounds.Left, y - 18, Bounds.Right, y + 7);
            canvas.DrawRect(kbRect, kbPaint);
            
            if (IsFocused) {
                kbPaint.Style = SKPaintStyle.Stroke;
                kbPaint.Color = _theme.ExplorerItemActive.WithAlpha(150);
                canvas.DrawRect(kbRect, kbPaint);
                kbPaint.Style = SKPaintStyle.Fill;
            }
        }
        if (node.FullPath == _selectedFilePath)
        {
            using var selectPaint = new SKPaint { Color = _theme.ExplorerSelection };
            canvas.DrawRect(new SKRect(Bounds.Left, y - 18, Bounds.Right, y + 7), selectPaint);
            
            selectPaint.Color = _theme.ExplorerItemActive;
            canvas.DrawRect(new SKRect(Bounds.Left, y - 18, Bounds.Left + 3, y + 7), selectPaint);
        }
        
        using var iconPaint = new SKPaint { IsAntialias = true, Style = SKPaintStyle.Fill };
        if (node.IsDirectory)
            DrawFolderIcon(canvas, xOffset, y, node.IsExpanded, iconPaint);
        else
            DrawFileIcon(canvas, xOffset, y, node.Name, iconPaint);
        
        float currentX = xOffset + 20;
        string name = node.Name;
        string query = _searchText.ToLower();

        if (!string.IsNullOrEmpty(query) && name.ToLower().Contains(query))
        {
            int startIndex = name.ToLower().IndexOf(query);
            
            string prefix = name.Substring(0, startIndex);
            paint.Color = _theme.Foreground;
            canvas.DrawText(prefix, currentX, y, _font, paint);
            currentX += _font.MeasureText(prefix);
            
            string match = name.Substring(startIndex, _searchText.Length);
            using (var highlightPaint = new SKPaint { Color = _theme.LineHighlight.WithAlpha(180) })
            {
                var highlightRect = new SKRect(currentX, y - 13, currentX + _font.MeasureText(match), y + 3);
                canvas.DrawRect(highlightRect, highlightPaint);
            }
            paint.Color = _theme.ExplorerItemActive; 
            canvas.DrawText(match, currentX, y, _font, paint);
            currentX += _font.MeasureText(match);
            
            string suffix = name.Substring(startIndex + _searchText.Length);
            paint.Color = _theme.Foreground;
            canvas.DrawText(suffix, currentX, y, _font, paint);
        }
        else
        {
            paint.Color = node.FullPath == _selectedFilePath ? SKColors.White : _theme.Foreground;
            canvas.DrawText(node.Name, currentX, y, _font, paint);
        }
        
        
        y += ItemHeight;

        if (node.IsDirectory && node.IsExpanded)
        {
            foreach (var child in node.Children)
                RenderNode(canvas, child, ref y, paint);
        }
    }
    
    public FileNode? GetNodeAt(float mouseX, float mouseY)
    {
        if (mouseY < Bounds.Top + 40) return null;
    
        if (!IsVisible || !Bounds.Contains(mouseX, mouseY)) return null;

        float adjustedMouseY = mouseY + _scrollY;
    
        float currentY = Bounds.Top + 60; 
    
        var nodesToSearch = string.IsNullOrEmpty(_searchText) ? _rootNodes : _filteredNodes;
    
        return FindNodeRecursive(nodesToSearch, adjustedMouseY, ref currentY);
    }
    
    private void DrawScrollbars(SKCanvas canvas)
    {
        using var scrollPaint = new SKPaint 
        { 
            Color = _theme.LineHighlight.WithAlpha(150), 
            Style = SKPaintStyle.Fill,
            IsAntialias = true
        };

        float totalHeight = GetTotalHeight(_rootNodes) + 60;
        if (totalHeight > Bounds.Height)
        {
            float viewRatio = Bounds.Height / totalHeight;
            float barHeight = Math.Max(20, Bounds.Height * viewRatio);
            float barY = Bounds.Top + (_scrollY / totalHeight) * Bounds.Height;
        
            var vBar = new SKRect(Bounds.Right - 7, barY, Bounds.Right - 1, barY + barHeight);
            canvas.DrawRoundRect(vBar, 3, 3, scrollPaint);
        }

        if (_maxContentWidth > Bounds.Width)
        {
            float viewRatio = Bounds.Width / _maxContentWidth;
            float barWidth = Math.Max(20, Bounds.Width * viewRatio);
            float barX = Bounds.Left + (_scrollX / _maxContentWidth) * Bounds.Width;
        
            var hBar = new SKRect(barX, Bounds.Bottom - 7, barX + barWidth, Bounds.Bottom - 1);
            canvas.DrawRoundRect(hBar, 3, 3, scrollPaint);
        }
    }
    
    private FileNode? FindNodeRecursive(List<FileNode> nodes, float targetY, ref float currentY)
    {
        foreach (var node in nodes)
        {
            var rect = new SKRect(Bounds.Left, currentY - 15, Bounds.Right, currentY + 10);
        
            if (targetY >= rect.Top && targetY <= rect.Bottom)
            {
                return node;
            }

            currentY += ItemHeight;

            if (node.IsDirectory && node.IsExpanded)
            {
                var found = FindNodeRecursive(node.Children, targetY, ref currentY);
                if (found != null) return found;
            }
        }
        return null;
    }
    
    public void LoadSubNodes(FileNode node)
    {
        if (!node.IsDirectory) return;

        try
        {
            node.Children.Clear();
            var entries = Directory.GetFileSystemEntries(node.FullPath)
                .OrderByDescending(e => Directory.Exists(e))
                .ThenBy(e => e);

            foreach (var entry in entries)
            {
                node.Children.Add(new FileNode(entry, node.Level + 1));
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Erro ao acessar pasta: {ex.Message}");
        }
    }
    
    public void OnFolderCollapsed()
    {
        _maxContentWidth = 0; 
    }
    
    public void ClearSearch()
    {
        _searchText = "";
        IsFocused = false;
        _scrollY = 0; 
        RestoreExpansionState(_rootNodes);
        _filteredNodes = _rootNodes; 
        UpdateFlattenedNodes();
    }
    
    private void RestoreExpansionState(List<FileNode> nodes)
    {
        foreach (var node in nodes)
        {
            if (_userExpandedPaths.Contains(node.FullPath))
            {
                node.IsExpanded = true;
            
                if (node.Children.Count == 0) LoadSubNodes(node); 
            
                if (node.Children.Count > 0)
                    RestoreExpansionState(node.Children);
            }
            else
            {
                node.IsExpanded = false;
            }
        }
    }
    
    public void OnMouseDown(float x, float y)
    {
        _isDraggingVertical = false;
        _isDraggingHorizontal = false;

        float totalHeight = GetTotalHeight(_rootNodes) + 60;
        if (totalHeight > Bounds.Height)
        {
            float viewRatio = Bounds.Height / totalHeight;
            float barHeight = Math.Max(20, Bounds.Height * viewRatio);
            float barY = Bounds.Top + (_scrollY / totalHeight) * Bounds.Height;
            var vBarRect = new SKRect(Bounds.Right - 10, barY, Bounds.Right, barY + barHeight); // Área de clique um pouco maior (10px)
        
            if (vBarRect.Contains(x, y)) {
                _isDraggingVertical = true;
                _lastMouseY = y;
                return;
            }
        }

        if (_maxContentWidth > Bounds.Width)
        {
            float viewRatio = Bounds.Width / _maxContentWidth;
            float barWidth = Math.Max(20, Bounds.Width * viewRatio);
            float barX = Bounds.Left + (_scrollX / _maxContentWidth) * Bounds.Width;
            var hBarRect = new SKRect(barX, Bounds.Bottom - 10, barX + barWidth, Bounds.Bottom);
        
            if (hBarRect.Contains(x, y)) {
                _isDraggingHorizontal = true;
                _lastMouseX = x;
            }
        }
    }
    
    public void OnMouseMove(float x, float y)
    {
        if (_isDraggingVertical)
        {
            float totalHeight = GetTotalHeight(_rootNodes) + 60;
            float deltaY = y - _lastMouseY;
            float scrollDelta = (deltaY / Bounds.Height) * totalHeight;
            ApplyScroll(0, scrollDelta);
            _lastMouseY = y;
        }
        else if (_isDraggingHorizontal)
        {
            float deltaX = x - _lastMouseX;
            float scrollDelta = (deltaX / Bounds.Width) * _maxContentWidth;
            ApplyScroll(scrollDelta, 0);
            _lastMouseX = x;
        }
    }
    
    public bool WasDragging => _isDraggingVertical || _isDraggingHorizontal;
    public void OnMouseUp() 
    {
        _isDraggingVertical = false;
        _isDraggingHorizontal = false;
    }
    
    public bool IsOnResizeBorder(float x)
    {
        float borderX = Bounds.Right;
        return x >= borderX - 5 && x <= borderX + 5;
    }
    
    public void Update(double deltaTime) { }
}
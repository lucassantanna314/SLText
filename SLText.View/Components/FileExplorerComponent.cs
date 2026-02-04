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
    
    public void SetSelectedFile(string? path) => _selectedFilePath = path;
    
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
    
    private void ApplyFilter()
    {
        if (string.IsNullOrEmpty(_searchText))
        {
            _filteredNodes = _rootNodes;
            return;
        }

        _filteredNodes = FilterRecursive(_rootNodes, _searchText.ToLower());
    }
    
    private List<FileNode> FilterRecursive(List<FileNode> nodes, string query)
    {
        var result = new List<FileNode>();
        foreach (var node in nodes)
        {
            bool matches = node.Name.ToLower().Contains(query);
            List<FileNode> matchedChildren = new();

            if (node.IsDirectory)
            {
                matchedChildren = FilterRecursive(node.Children, query);
            }

            if (matches || matchedChildren.Count > 0)
            {
                if (matchedChildren.Count > 0) node.IsExpanded = true;
                result.Add(node);
            }
            
        }
        return result;
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
            _rootNodes.Add(new FileNode(entry, 0));
        }
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
        canvas.Translate(-_scrollX, -_scrollY);
        
        float yOffset = Bounds.Top + 60;
        
        var nodesToRender = string.IsNullOrEmpty(_searchText) ? _rootNodes : _filteredNodes;
        foreach (var node in nodesToRender)
        {
            RenderNode(canvas, node, ref yOffset, textPaint);
        }
        
        canvas.Restore();
        DrawScrollbars(canvas);
        
        paint.Color = _theme.LineHighlight.WithAlpha(120);
        canvas.DrawLine(Bounds.Right, Bounds.Top, Bounds.Right, Bounds.Bottom, paint);
    }
    
    private void RenderNode(SKCanvas canvas, FileNode node, ref float y, SKPaint paint)
    {
        float relativeY = y - ScrollY;
        float xOffset = Bounds.Left + 15 + (node.Level * 20); 
        
        float textWidth = paint.MeasureText(node.Name);
        _maxContentWidth = Math.Max(_maxContentWidth, xOffset + textWidth + 50);

        if (relativeY >= Bounds.Top - ItemHeight && relativeY <= Bounds.Bottom + ItemHeight)
        {
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
            
            //highlight
            float currentX = xOffset + 20;
            string name = node.Name;
            string query = _searchText.ToLower();

            if (!string.IsNullOrEmpty(query) && name.ToLower().Contains(query))
            {
                int startIndex = name.ToLower().IndexOf(query);

                string prefix = name.Substring(0, startIndex);
                canvas.DrawText(prefix, currentX, y, _font, paint);
                currentX += _font.MeasureText(prefix);
                
                string match = name.Substring(startIndex, query.Length);
                using (var highlightPaint = new SKPaint { Color = _theme.LineHighlight.WithAlpha(180) })
                {
                    var highlightRect = new SKRect(currentX, y - 13, currentX + _font.MeasureText(match), y + 3);
                    canvas.DrawRect(highlightRect, highlightPaint);
                }
                
                canvas.DrawText(match, currentX, y, _font, paint);
                currentX += _font.MeasureText(match);
                
                string suffix = name.Substring(startIndex + query.Length);
                canvas.DrawText(suffix, currentX, y, _font, paint);
                
            }
            else
            {
                paint.Color = node.FullPath == _selectedFilePath ? SKColors.White : _theme.Foreground;
                canvas.DrawText(node.Name, xOffset + 20, y, _font, paint); 
            }
            
            
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
        Refresh();   
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
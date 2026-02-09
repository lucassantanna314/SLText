using System.Text.RegularExpressions;
using Microsoft.CodeAnalysis;
using SkiaSharp;
using SLText.Core.Engine;
using SLText.Core.Engine.Model;
using SLText.Core.Interfaces;
using SLText.View.Abstractions;
using SLText.View.Styles;
using SLText.View.Components.Canvas;

namespace SLText.View.Components;

public class EditorComponent : IComponent, IZoomable
{
    private List<SearchResult> _searchResults = new();
    private string _lastSearchTerm = "";

    public SKRect Bounds { get; set; }
    private TextBuffer _buffer;
    private CursorManager _cursor;
    private readonly SyntaxProvider _syntaxProvider = new();
    private readonly BlockAnalyzer _blockAnalyzer = new();

    private readonly GutterRenderer _gutterRenderer;
    private readonly TextRenderer _textRenderer;
    private readonly ViewportManager _viewport;
    private readonly BracketRenderer _bracketRenderer;
    private readonly IndentGuideRenderer _indentGuideRenderer;

    private EditorTheme _theme = EditorTheme.Dark;
    private List<(string pattern, SKColor color)> _currentRules = new();
    private readonly SKFont _font;
    public SKFont Font => _font;
    private float _lineHeight;

    private bool _showCursor = true;
    private double _cursorTimer;
    private const double BlinkInterval = 0.5;
    private bool _needScrollToCursor;
    private string? _currentFilePath;

    private bool _isDraggingVertical;
    private bool _isDraggingHorizontal;
    private float _lastMouseY;
    private float _lastMouseX;
    
    public event Action<int>? OnRunTestRequested;
    private static readonly Regex TestAttributeRegex = new Regex(@"\[(Test|Fact|TestMethod|TestClass)\]", RegexOptions.Compiled);
    
    private List<Diagnostic> _diagnostics = new();
    
    public void SetDiagnostics(List<Diagnostic> diagnostics) 
    {
        _diagnostics = diagnostics;
        if (diagnostics.Any())
        {
            Console.WriteLine($"Editor recebeu {diagnostics.Count} erros do LSP.");
        }
    }
    
    public EditorComponent(TextBuffer buffer, CursorManager cursor)
    {
        _buffer = buffer;
        _cursor = cursor;

        // Inicialização da Fonte (JetBrains Mono)
        string fontPath = Path.Combine(AppContext.BaseDirectory, "Assets", "JetBrainsMono-Regular.ttf");
        var typeface = File.Exists(fontPath) ? SKTypeface.FromFile(fontPath) : SKTypeface.FromFamilyName("monospace");
        _font = new SKFont(typeface, 16) { Edging = SKFontEdging.SubpixelAntialias, Subpixel = true };

        // Cálculo de métricas iniciais
        _font.GetFontMetrics(out var metrics);
        _lineHeight = metrics.Descent - metrics.Ascent + 4;

        // Inicialização dos Especialistas
        _gutterRenderer = new GutterRenderer(_font, _theme);
        _textRenderer = new TextRenderer(_font, _theme);
        _viewport = new ViewportManager(_lineHeight);
        _bracketRenderer = new BracketRenderer(_font, _theme);
        _indentGuideRenderer = new IndentGuideRenderer(_theme);
    }
    
    private void RenderDiagnostics(SKCanvas canvas, int lineIndex, float textX, float yPos)
    {
        if (_diagnostics == null || _diagnostics.Count == 0) return;

        var lineErrors = _diagnostics.Where(d => d.Location.GetLineSpan().StartLinePosition.Line == lineIndex);

        using var paint = new SKPaint
        {
            Color = SKColors.Red,
            Style = SKPaintStyle.Stroke,
            StrokeWidth = 2.0f, 
            IsAntialias = true,
            PathEffect = SKPathEffect.CreateDash(new float[] { 2, 2 }, 0) 
        };

        foreach (var diag in lineErrors)
        {
            var span = diag.Location.GetLineSpan();
            string lineText = _buffer.GetLine(lineIndex);

            int startChar = Math.Clamp(span.StartLinePosition.Character, 0, lineText.Length);
            int endChar = Math.Clamp(span.EndLinePosition.Character, 0, lineText.Length);
        
            float startX = _font.MeasureText(lineText.Substring(0, startChar));
            float endX = _font.MeasureText(lineText.Substring(0, endChar));

            if (endX - startX < 4) 
            {
                endX = startX + 8; 
            }

            canvas.DrawLine(textX + startX, yPos + 3, textX + endX, yPos + 3, paint);
        }
    }

    public void Render(SKCanvas canvas)
    {
        using var bgPaint = new SKPaint { Color = _theme.Background };
        canvas.DrawRect(Bounds, bgPaint);

        _viewport.UpdateBounds(Bounds);
        _font.GetFontMetrics(out var metrics);

        var lines = _buffer.GetLines().ToList();
        float gutterWidth = _gutterRenderer.GetWidth(lines.Count);
        float charWidth = _font.MeasureText(" ");
        string extension = Path.GetExtension(_currentFilePath ?? "").ToLower();

        var visibleLineNumbers = Enumerable.Range(1, lines.Count).ToList();

        _gutterRenderer.Render(canvas, Bounds, visibleLineNumbers, _lineHeight, _viewport.ScrollY, _buffer);
        
        canvas.Save();
        var contentClip = new SKRect(Bounds.Left + gutterWidth, Bounds.Top, Bounds.Right, Bounds.Bottom);
        canvas.ClipRect(contentClip);

        _indentGuideRenderer.Render(canvas, _buffer, _cursor, gutterWidth, charWidth, Bounds, _lineHeight,
            _viewport.ScrollY, extension);

        canvas.Translate(-_viewport.ScrollX, -_viewport.ScrollY);

        float textX = Bounds.Left + gutterWidth + 10;

        for (int i = 0; i < lines.Count; i++)
        {
            float yPos = Bounds.Top + (i * _lineHeight) - metrics.Ascent;

            float relativeY = yPos - _viewport.ScrollY;

            if (relativeY < Bounds.Top - _lineHeight) continue;

            if (relativeY > Bounds.Bottom + _lineHeight) break;

            if (i == _cursor.Line) RenderLineHighlight(canvas, yPos, gutterWidth, metrics);

            RenderSelection(canvas, i, yPos, gutterWidth, metrics);

            var lineResults = _searchResults.Where(r => r.Line == i);
            foreach (var res in lineResults)
            {
                float startX = _font.MeasureText(lines[i].Substring(0, res.Column));
                float width = _font.MeasureText(lines[i].Substring(res.Column, res.Length));

                using var searchPaint = new SKPaint { Color = SKColors.BlueViolet.WithAlpha(40) };
                var searchRect = new SKRect(
                    textX + startX,
                    yPos + metrics.Ascent,
                    textX + startX + width,
                    yPos + metrics.Descent
                );
                canvas.DrawRect(searchRect, searchPaint);
            }

            string lineToRender = lines[i];
            _textRenderer.RenderLine(canvas, lineToRender, textX, yPos, _currentRules);
            
            RenderDiagnostics(canvas, i, textX, yPos);

            if (i == _cursor.Line && _showCursor)
            {
                RenderCursor(canvas, textX, yPos, lineToRender, metrics);
            }
        }

        _bracketRenderer.Render(canvas, _buffer, _cursor, textX, Bounds, _lineHeight, _viewport.ScrollY, metrics);

        canvas.Restore();
        DrawScrollbars(canvas);
    }

    private void DrawScrollbars(SKCanvas canvas)
    {
        using var scrollPaint = new SKPaint
        {
            Color = _theme.LineHighlight.WithAlpha(150),
            Style = SKPaintStyle.Fill,
            IsAntialias = true
        };

        // --- Scrollbar Vertical ---
        float totalHeight = _buffer.LineCount * _lineHeight + (Bounds.Height / 2);
        if (totalHeight > Bounds.Height)
        {
            float viewRatio = Bounds.Height / totalHeight;
            float barHeight = Math.Max(20, Bounds.Height * viewRatio);
            float barY = Bounds.Top + (_viewport.ScrollY / totalHeight) * Bounds.Height;

            var vBar = new SKRect(Bounds.Right - 7, barY, Bounds.Right - 1, barY + barHeight);
            canvas.DrawRoundRect(vBar, 3, 3, scrollPaint);
        }

        // --- Scrollbar Horizontal ---
        var maxChars = _buffer.GetLines().Max(l => l.Length);
        float totalWidth = (maxChars * _font.MeasureText(" ")) + 100;

        if (totalWidth > Bounds.Width)
        {
            float viewRatio = Bounds.Width / totalWidth;
            float barWidth = Math.Max(20, Bounds.Width * viewRatio);
            float barX = Bounds.Left + (_viewport.ScrollX / totalWidth) * Bounds.Width;

            var hBar = new SKRect(barX, Bounds.Bottom - 7, barX + barWidth, Bounds.Bottom - 1);
            canvas.DrawRoundRect(hBar, 3, 3, scrollPaint);
        }
    }

    public void SetCurrentData(TextBuffer buffer, CursorManager cursor)
    {
        _buffer = buffer;
        _cursor = cursor;

        _viewport.UpdateBounds(Bounds);

    }

    public void SetScroll(float x, float y)
    {
        _viewport.ScrollX = x;
        _viewport.ScrollY = y;
    }

    public void PerformSearch(string term)
    {
        _lastSearchTerm = term;
        _searchResults = _buffer.SearchAll(term);

        if (_searchResults.Any())
        {
            var first = _searchResults[0];
            _cursor.SetPosition(first.Line, first.Column);
            RequestScrollToCursor();
        }
    }

    public void EnsureCursorVisible()
    {
        int targetLine = Math.Clamp(_cursor.Line, 0, _buffer.LineCount - 1);
        if (targetLine != _cursor.Line)
        {
            _cursor.SetPosition(targetLine, _cursor.Column);
        }

        RequestScrollToCursor();
    }

    public float GetGutterWidth()
    {
        var lines = _buffer.GetLines().ToList();
        return _gutterRenderer.GetWidth(lines.Count);
    }

    public void HandleGutterClick(float x, float y)
    {
        float gutterWidth = _gutterRenderer.GetWidth(_buffer.LineCount);
        if (x < Bounds.Left + 30) 
        {
            var (line, _) = GetTextPositionFromMouse(x, y);
            string lineContent = _buffer.GetLine(line);

            if (TestAttributeRegex.IsMatch(lineContent))
            {
                OnRunTestRequested?.Invoke(line); 
            }
        }
        EnsureCursorVisible();
    }

    private void RenderLineHighlight(SKCanvas canvas, float yPos, float gutterWidth, SKFontMetrics metrics)
    {
        using var paint = new SKPaint { Color = _theme.LineHighlight };
        var rect = new SKRect(Bounds.Left + gutterWidth + _viewport.ScrollX, yPos + metrics.Ascent,
            Bounds.Right + _viewport.ScrollX, yPos + metrics.Descent);
        canvas.DrawRect(rect, paint);
    }

    private void RenderCursor(SKCanvas canvas, float textX, float yPos, string lineText, SKFontMetrics metrics)
    {
        int safeCol = Math.Min(_cursor.Column, lineText.Length);
        float cursorX = _font.MeasureText(lineText.Substring(0, safeCol));

        using var paint = new SKPaint { Color = _theme.Cursor };
        var rect = new SKRect(textX + cursorX, yPos + metrics.Ascent, textX + 2 + cursorX, yPos + metrics.Descent);
        canvas.DrawRect(rect, paint);
    }

    public void Update(double deltaTime)
    {
        _cursorTimer += deltaTime;
        if (_cursorTimer >= BlinkInterval)
        {
            _showCursor = !_showCursor;
            _cursorTimer = 0;
        }

        if (_needScrollToCursor)
        {
            float gutterWidth = _gutterRenderer.GetWidth(_buffer.LineCount);
            string currentLine = _buffer.GetLines().ElementAtOrDefault(_cursor.Line) ?? "";
            float cursorX = _font.MeasureText(currentLine.Substring(0, Math.Min(_cursor.Column, currentLine.Length)));

            _viewport.ScrollToCursor(_cursor.Line, _cursor.Column, cursorX, gutterWidth);
            _needScrollToCursor = false;
        }
    }

    public void ApplyScroll(float deltaX, float deltaY)
    {
        var maxChars = _buffer.GetLines().Max(l => l.Length);
        float maxScrollX = Math.Max(0, (maxChars * 10) - Bounds.Width + 100);

        float totalHeight = _buffer.LineCount * _lineHeight;
        _viewport.ApplyScroll(deltaX, deltaY, maxScrollX, totalHeight);
    }

    public float ScrollX
    {
        get => _viewport.ScrollX;
        set => _viewport.ScrollX = value;
    }

    public float ScrollY
    {
        get => _viewport.ScrollY;
        set => _viewport.ScrollY = value;
    }

    public (int line, int col) GetTextPositionFromMouse(float x, float y)
    {
        _font.GetFontMetrics(out var metrics);

        float gutterWidth = _gutterRenderer.GetWidth(_buffer.LineCount);
    
        float localX = x - Bounds.Left - gutterWidth - 10;
    
        float localY = y - Bounds.Top;

        return _viewport.GetTextPosition(
            localX, 
            localY, 
            0, 
            _font.MeasureText(" ")
        );
    }

    public string UpdateSyntax(string? path)
    {
        _currentFilePath = path;
        _currentRules = _syntaxProvider.GetRulesForFile(path, _theme);
        return _syntaxProvider.GetLanguageName(path);
    }

    public void SetTheme(EditorTheme theme)
    {
        _theme = theme;
        _gutterRenderer.SetTheme(theme);
        _textRenderer.SetTheme(theme);
        _bracketRenderer.SetTheme(theme);
        _indentGuideRenderer.SetTheme(theme);
    }

    public void RequestScrollToCursor() => _needScrollToCursor = true;

    private void RenderSelection(SKCanvas canvas, int lineIndex, float yPos, float gutterWidth, SKFontMetrics metrics)
    {
        var range = _cursor.GetSelectionRange();
        if (range == null) return;

        var (sLine, sCol, eLine, eCol) = range.Value;

        if (lineIndex >= sLine && lineIndex <= eLine)
        {
            var lines = _buffer.GetLines().ToList();
            string text = lines[lineIndex];
            float textX = Bounds.Left + gutterWidth + 10;

            float startX = 0;
            float endX = 0;

            if (lineIndex == sLine && lineIndex == eLine)
            {
                startX = _font.MeasureText(text.Substring(0, Math.Min(sCol, text.Length)));
                endX = _font.MeasureText(text.Substring(0, Math.Min(eCol, text.Length)));
            }
            else if (lineIndex == sLine)
            {
                startX = _font.MeasureText(text.Substring(0, Math.Min(sCol, text.Length)));
                endX = _font.MeasureText(text) + 10;
            }
            else if (lineIndex == eLine)
            {
                startX = 0;
                endX = _font.MeasureText(text.Substring(0, Math.Min(eCol, text.Length)));
            }
            else
            {
                startX = 0;
                endX = _font.MeasureText(text) + 10;
            }

            using var selectionPaint = new SKPaint { Color = _theme.SelectionBackground };

            var selectionRect = new SKRect(
                textX + startX,
                yPos + metrics.Ascent,
                textX + endX,
                yPos + metrics.Descent
            );

            canvas.DrawRect(selectionRect, selectionPaint);
        }
    }

    public float FontSize
    {
        get => _font.Size;
        set
        {
            float newSize = Math.Clamp(value, 8, 72);
            _font.Size = newSize;

            _font.GetFontMetrics(out var metrics);
            _lineHeight = metrics.Descent - metrics.Ascent + 4;
            _viewport.UpdateLineHeight(_lineHeight);

            RequestScrollToCursor();
        }
    }

    public bool OnMouseDown(float x, float y)
    {
        _isDraggingVertical = false;
        _isDraggingHorizontal = false;

        // Barra Vertical
        float totalHeight = _buffer.LineCount * _lineHeight + (Bounds.Height / 2);
        if (totalHeight > Bounds.Height)
        {
            float viewRatio = Bounds.Height / totalHeight;
            float barHeight = Math.Max(20, Bounds.Height * viewRatio);
            float barY = Bounds.Top + (_viewport.ScrollY / totalHeight) * Bounds.Height;
            var vBarArea = new SKRect(Bounds.Right - 15, barY, Bounds.Right, barY + barHeight);

            if (vBarArea.Contains(x, y))
            {
                _isDraggingVertical = true;
                _lastMouseY = y;
                return true; // Capturou o clique
            }
        }

        //  Barra Horizontal
        var maxChars = _buffer.GetLines().Max(l => l.Length);
        float totalWidth = (maxChars * _font.MeasureText(" ")) + 100;
        if (totalWidth > Bounds.Width)
        {
            float viewRatio = Bounds.Width / totalWidth;
            float barWidth = Math.Max(20, Bounds.Width * viewRatio);
            float barX = Bounds.Left + (_viewport.ScrollX / totalWidth) * Bounds.Width;
            var hBarArea = new SKRect(barX, Bounds.Bottom - 15, barX + barWidth, Bounds.Bottom);

            if (hBarArea.Contains(x, y))
            {
                _isDraggingHorizontal = true;
                _lastMouseX = x;
                return true;
            }
        }

        return false;
    }

    public void OnMouseMove(float x, float y)
    {
        if (_isDraggingVertical)
        {
            float totalHeight = _buffer.LineCount * _lineHeight + (Bounds.Height / 2);
            float deltaY = y - _lastMouseY;
            float scrollDelta = (deltaY / Bounds.Height) * totalHeight;
            ApplyScroll(0, -scrollDelta);
            _lastMouseY = y;
        }
        else if (_isDraggingHorizontal)
        {
            var maxChars = _buffer.GetLines().Max(l => l.Length);
            float totalWidth = (maxChars * _font.MeasureText(" ")) + 100;
            float deltaX = x - _lastMouseX;
            float scrollDelta = (deltaX / Bounds.Width) * totalWidth;
            ApplyScroll(-scrollDelta, 0);
            _lastMouseX = x;
        }
    }

    public void OnMouseUp()
    {
        _isDraggingVertical = false;
        _isDraggingHorizontal = false;
    }
    
    public (float x, float y) GetCursorScreenPosition()
    {
        _font.GetFontMetrics(out var metrics);
        float gutterWidth = GetGutterWidth();
    
        string lineText = _buffer.GetLine(_cursor.Line);
        int safeCol = Math.Min(_cursor.Column, lineText.Length);
        float cursorX = _font.MeasureText(lineText.Substring(0, safeCol));

        float screenX = Bounds.Left + gutterWidth + 10 + cursorX - _viewport.ScrollX;
        float screenY = Bounds.Top + (_cursor.Line * _lineHeight) + _lineHeight - _viewport.ScrollY;

        return (screenX, screenY);
    }
}
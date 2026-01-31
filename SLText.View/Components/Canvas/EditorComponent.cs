using SkiaSharp;
using SLText.Core.Engine;
using SLText.Core.Interfaces;
using SLText.View.Abstractions;
using SLText.View.Styles;
using SLText.View.Components.Canvas;

namespace SLText.View.Components;

public class EditorComponent : IComponent, IZoomable
{
    public SKRect Bounds { get; set; }
    private readonly TextBuffer _buffer;
    private readonly CursorManager _cursor;
    private readonly SyntaxProvider _syntaxProvider = new();
    
    private readonly GutterRenderer _gutterRenderer;
    private readonly TextRenderer _textRenderer;
    private readonly ViewportManager _viewport;

    private EditorTheme _theme = EditorTheme.Dark;
    private List<(string pattern, SKColor color)> _currentRules = new();
    private readonly SKFont _font;
    private float _lineHeight;
    
    private bool _showCursor = true;
    private double _cursorTimer;
    private const double BlinkInterval = 0.5;
    private bool _needScrollToCursor;

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
    }

    public void Render(SKCanvas canvas)
    {
        canvas.Clear(_theme.Background);
        _viewport.UpdateBounds(Bounds);
        _font.GetFontMetrics(out var metrics);

        var lines = _buffer.GetLines().ToList();
        float gutterWidth = _gutterRenderer.GetWidth(lines.Count);

        // --- RENDERIZAR GUTTER (Fixo no X) ---
        _gutterRenderer.Render(canvas, Bounds, lines.Count, _lineHeight, _viewport.ScrollY);

        // --- RENDERIZAR CONTEÚDO (Clipado e com Scroll) ---
        canvas.Save();
        var contentClip = new SKRect(Bounds.Left + gutterWidth, Bounds.Top, Bounds.Right, Bounds.Bottom);
        canvas.ClipRect(contentClip);
        canvas.Translate(-_viewport.ScrollX, -_viewport.ScrollY);

        float textX = Bounds.Left + gutterWidth + 10;

        for (int i = 0; i < lines.Count; i++)
        {
            float yPos = Bounds.Top + (i * _lineHeight) - metrics.Ascent;

            // Culling: Desenha apenas o que está visível
            if (yPos < _viewport.ScrollY - _lineHeight) continue;
            if (yPos > _viewport.ScrollY + Bounds.Height + _lineHeight) break;

            // Destaque de linha atual
            if (i == _cursor.Line) RenderLineHighlight(canvas, yPos, gutterWidth, metrics);

            // Seleção e Texto
            RenderSelection(canvas, i, yPos, gutterWidth, metrics);
            _textRenderer.RenderLine(canvas, lines[i], textX, yPos, _currentRules);

            // Cursor
            if (i == _cursor.Line && _showCursor) RenderCursor(canvas, textX, yPos, lines[i], metrics);
        }

        canvas.Restore();
    }

    private void RenderLineHighlight(SKCanvas canvas, float yPos, float gutterWidth, SKFontMetrics metrics)
    {
        using var paint = new SKPaint { Color = _theme.LineHighlight };
        var rect = new SKRect(Bounds.Left + gutterWidth + _viewport.ScrollX, yPos + metrics.Ascent, Bounds.Right + _viewport.ScrollX, yPos + metrics.Descent);
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
        if (_cursorTimer >= BlinkInterval) { _showCursor = !_showCursor; _cursorTimer = 0; }
        
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

    public (int line, int col) GetTextPositionFromMouse(float x, float y) => 
        _viewport.GetTextPosition(x, y, _gutterRenderer.GetWidth(_buffer.LineCount), _font.MeasureText(" "));

    public string UpdateSyntax(string? path)
    {
        _currentRules = _syntaxProvider.GetRulesForFile(path, _theme);
        return _syntaxProvider.GetLanguageName(path);
    }

    public void SetTheme(EditorTheme theme)
    {
        _theme = theme;
        _gutterRenderer.SetTheme(theme);
        _textRenderer.SetTheme(theme);
    }

    public void RequestScrollToCursor() => _needScrollToCursor = true;
    public float LineHeight => _lineHeight;
    public int GetTotalLines() => _buffer.LineCount;
    
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
}
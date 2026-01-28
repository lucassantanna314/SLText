using SkiaSharp;
using SLText.Core.Engine;
using SLText.View.Abstractions;
using SLText.View.Styles;

namespace SLText.View.Components;

public class EditorComponent : IComponent
{
    public SKRect Bounds { get; set; }
    private readonly TextBuffer _buffer;
    private readonly CursorManager _cursor;
    
    public float ScrollY { get; set; } = 0;
    private float _lineHeight;
    private bool _needScrollToCursor;
    
    private EditorTheme _theme = EditorTheme.Dark;
    private List<(string pattern, SKColor color)> _currentRules = new();
    
    public void SetTheme(EditorTheme theme) => _theme = theme;
    private readonly SyntaxProvider _syntaxProvider = new();
    
    public string UpdateSyntax(string? filePath) 
    {
        _currentRules = _syntaxProvider.GetRulesForFile(filePath, _theme);
        return _syntaxProvider.GetLanguageName(filePath);
    }
    
    private readonly SKPaint _gutterPaint = new() 
    { 
        Color = new SKColor(100, 100, 100), 
        IsAntialias = true 
    };
    
    private readonly SKPaint _gutterBgPaint = new() 
    { 
        Color = new SKColor(40, 40, 40) 
    };
    
    public void RequestScrollToCursor() => _needScrollToCursor = true;
    
    private readonly SKFont _font;
    private readonly SKPaint _textPaint = new() 
    { 
        Color = SKColors.White, 
        IsAntialias = true 
    };
    private readonly SKPaint _cursorPaint = new() { Color = SKColors.Chocolate };

    private bool _showCursor = true;
    private double _cursorTimer; 
    private const double BlinkInterval = 0.5; 

    public EditorComponent(TextBuffer buffer, CursorManager cursor)
    {
        _buffer = buffer;
        _cursor = cursor;
        
        var typeface = SKTypeface.FromFamilyName("Consolas");
        
        _font = new SKFont(typeface, 18)
        {
            Edging = SKFontEdging.SubpixelAntialias, 
            Hinting = SKFontHinting.Full,
            Subpixel = true 
        };
    }

    public CursorManager GetCursor() => _cursor;

    public void Render(SKCanvas canvas)
{
    _font.GetFontMetrics(out var metrics);
    _lineHeight = metrics.Descent - metrics.Ascent + 4;
    
    var lines = _buffer.GetLines().ToList();
    
    string maxLineStr = lines.Count.ToString();
    float gutterPadding = 20;
    float gutterWidth = _font.MeasureText(maxLineStr) + gutterPadding;

    canvas.Save();
    canvas.ClipRect(Bounds);
    
    using (var gutterBgPaint = new SKPaint { Color = _theme.GutterBackground })
    {
        var gutterRect = new SKRect(Bounds.Left, Bounds.Top, Bounds.Left + gutterWidth, Bounds.Bottom);
        canvas.DrawRect(gutterRect, gutterBgPaint);
    }

    canvas.Translate(0, -ScrollY);
    
    for (int i = 0; i < lines.Count; i++)
    {
        float yPos = Bounds.Top + (i * _lineHeight) - metrics.Ascent;

        if (yPos - _lineHeight > Bounds.Bottom + ScrollY) break;
        if (yPos + _lineHeight < Bounds.Top + ScrollY) continue;

        // --- HIGHLIGHT DA LINHA ---
        if (i == _cursor.Line)
        {
            using var highlightPaint = new SKPaint { Color = _theme.LineHighlight };
            var lineRect = new SKRect(
                Bounds.Left + gutterWidth, 
                yPos + metrics.Ascent, 
                Bounds.Right, 
                yPos + metrics.Descent
            );
            canvas.DrawRect(lineRect, highlightPaint);
        }

        // --- NÚMEROS DA LINHA ---
        string lineNum = (i + 1).ToString();
        float lineNumX = Bounds.Left + (gutterWidth - _font.MeasureText(lineNum) - 10);
        _gutterPaint.Color = _theme.GutterForeground;
        canvas.DrawText(lineNum, lineNumX, yPos, _font, _gutterPaint);

        // --- TEXTO ---
        float textX = Bounds.Left + gutterWidth + 10; 
        RenderHighlightedLine(canvas, lines[i], textX, yPos);

        // --- CURSOR ---
        if (i == _cursor.Line && _showCursor)
        {
            int safeCol = Math.Min(_cursor.Column, lines[i].Length);
            float cursorX = _font.MeasureText(lines[i].Substring(0, safeCol));
            
            var cursorRect = new SKRect(
                textX + cursorX,
                yPos + metrics.Ascent,
                textX + 2 + cursorX,
                yPos + metrics.Descent
            );
            
            _cursorPaint.Color = _theme.Cursor; 
            canvas.DrawRect(cursorRect, _cursorPaint);
        }
    }
    
    canvas.Restore();
}
    private record TextToken(string Text, SKColor Color);

    private List<TextToken> TokenizeLine(string text)
    {
        var tokens = new List<(int Index, int Length, SKColor Color)>();

        // Encontra todas as matches de todas as regras
        foreach (var (pattern, color) in _currentRules)
        {
            var matches = System.Text.RegularExpressions.Regex.Matches(text, pattern);
            foreach (System.Text.RegularExpressions.Match match in matches)
            {
                tokens.Add((match.Index, match.Length, color));
            }
        }

        // Ordena por índice
        var sortedMatches = tokens.OrderBy(t => t.Index).ToList();

        var result = new List<TextToken>();
        int lastIndex = 0;

        // Preenche os vazios com a cor padrão e adiciona os coloridos
        foreach (var match in sortedMatches)
        {
            if (match.Index < lastIndex) continue; // Pula sobreposições simples

            // Texto normal antes da match
            if (match.Index > lastIndex)
            {
                result.Add(new TextToken(text.Substring(lastIndex, match.Index - lastIndex), _theme.Foreground));
            }

            // Texto colorido
            result.Add(new TextToken(text.Substring(match.Index, match.Length), match.Color));
            lastIndex = match.Index + match.Length;
        }

        // Texto normal restante
        if (lastIndex < text.Length)
        {
            result.Add(new TextToken(text.Substring(lastIndex), _theme.Foreground));
        }

        return result;
    }
    
    public void ScrollToCursor()
    {
        float cursorYTop = _cursor.Line * _lineHeight;
        float cursorYBottom = (_cursor.Line + 1) * _lineHeight;

        if (cursorYBottom > ScrollY + Bounds.Height)
        {
            ScrollY = cursorYBottom - Bounds.Height;
        }
        else if (cursorYTop < ScrollY)
        {
            ScrollY = cursorYTop;
        }
    }
    
    private void RenderHighlightedLine(SKCanvas canvas, string text, float x, float y)
    {
        if (string.IsNullOrEmpty(text)) return;

        if (_currentRules == null || _currentRules.Count == 0)
        {
            _textPaint.Color = _theme.Foreground;
            canvas.DrawText(text, x, y, _font, _textPaint);
            return;
        }

        var tokens = TokenizeLine(text);
        float currentX = x;

        foreach (var token in tokens)
        {
            _textPaint.Color = token.Color;
            canvas.DrawText(token.Text, currentX, y, _font, _textPaint);
        
            currentX += _font.MeasureText(token.Text);
        }
    }

    public void Update(double deltaTime)
    {
        _cursorTimer += deltaTime;
        if (_cursorTimer >= BlinkInterval)
        {
            _showCursor = !_showCursor;
            _cursorTimer = 0;
        }
        
        if (_needScrollToCursor) {
            ScrollToCursor();
            _needScrollToCursor = false; 
        }
    }
}
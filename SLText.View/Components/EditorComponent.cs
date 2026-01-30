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
    public float ScrollX { get; set; } = 0;
    
    private float _lineHeight;
    private bool _needScrollToCursor;
    
    private EditorTheme _theme = EditorTheme.Dark;
    private List<(string pattern, SKColor color)> _currentRules = new();
    
    public int GetTotalLines() => _buffer.GetLines().Count();
    public float LineHeight => _lineHeight;
    
    public void SetTheme(EditorTheme theme) 
    {
        _theme = theme;
    
        _gutterPaint.Color = _theme.GutterForeground;
        _textPaint.Color = _theme.Foreground;
        _cursorPaint.Color = _theme.Cursor;
    }
    private readonly SyntaxProvider _syntaxProvider = new();
    
    public string UpdateSyntax(string? filePath) 
    {
        _currentRules = _syntaxProvider.GetRulesForFile(filePath, _theme);
        return _syntaxProvider.GetLanguageName(filePath);
    }
    
    private readonly SKPaint _gutterPaint = new() { IsAntialias = true };    
    public void RequestScrollToCursor() => _needScrollToCursor = true;
    
    private readonly SKFont _font;
    private readonly SKPaint _textPaint = new() { IsAntialias = true };
    private readonly SKPaint _cursorPaint = new() { IsAntialias = true };
    private bool _showCursor = true;
    private double _cursorTimer; 
    private const double BlinkInterval = 0.5; 
    private readonly SKPaint _sharedAuxPaint = new() { IsAntialias = true };

    public EditorComponent(TextBuffer buffer, CursorManager cursor)
    {
        _buffer = buffer;
        _cursor = cursor;
        
        string fontPath = Path.Combine(AppContext.BaseDirectory, "Assets", "JetBrainsMono-Regular.ttf");
        
        SKTypeface typeface;

        if (File.Exists(fontPath))
        {
            typeface = SKTypeface.FromFile(fontPath);
        }
        else
        {
            typeface = SKTypeface.FromFamilyName("monospace", SKFontStyle.Normal);
            Console.WriteLine($"Fonte não encontrada em {fontPath}. Usando fallback.");
        }

        _font = new SKFont(typeface, 16)
        {
            Edging = SKFontEdging.SubpixelAntialias,
            Hinting = SKFontHinting.Full,
            Subpixel = true
        };
    }

    public CursorManager GetCursor() => _cursor;
    
    public float GetMaxHorizontalScroll()
    {
        var maxChars = _buffer.GetLines().Max(l => l.Length);
        float totalWidth = maxChars * 10; 
        return Math.Max(0, totalWidth - Bounds.Width + 100);
    }

    public void Render(SKCanvas canvas)
{
    canvas.Clear(_theme.Background);

    _font.GetFontMetrics(out var metrics);
    _lineHeight = metrics.Descent - metrics.Ascent + 4;
    
    var lines = _buffer.GetLines().ToList();
    
    float gutterWidth = GetGutterWidth();

    canvas.Save();
    canvas.ClipRect(Bounds);
    
    // --- DESENHAR O GUTTER (ESTÁTICO NO EIXO X) ---
    canvas.Save();
    canvas.Translate(0, -ScrollY);
    
    _sharedAuxPaint.Color = _theme.GutterBackground;
    var gutterRect = new SKRect(Bounds.Left, Bounds.Top + ScrollY, Bounds.Left + gutterWidth, Bounds.Bottom + ScrollY);
    canvas.DrawRect(gutterRect, _sharedAuxPaint);
    
    for (int i = 0; i < lines.Count; i++)
    {
        float yPos = Bounds.Top + (i * _lineHeight) - metrics.Ascent;
        if (yPos - _lineHeight > Bounds.Bottom + ScrollY) break;
        if (yPos + _lineHeight < Bounds.Top + ScrollY) continue;

        string lineNum = (i + 1).ToString();
        float lineNumX = Bounds.Left + (gutterWidth - _font.MeasureText(lineNum) - 10);
        _gutterPaint.Color = _theme.GutterForeground; 
        canvas.DrawText(lineNum, lineNumX, yPos, _font, _gutterPaint);
    }
    canvas.Restore();

    // --- DESENHAR O CONTEÚDO (SCROLL X e Y) ---
    canvas.Save();
    canvas.Translate(-ScrollX, -ScrollY);
    
    var textClip = new SKRect(Bounds.Left + gutterWidth + ScrollX, Bounds.Top + ScrollY, Bounds.Right + ScrollX, Bounds.Bottom + ScrollY);
    canvas.ClipRect(textClip);

    for (int i = 0; i < lines.Count; i++)
    {
        float yPos = Bounds.Top + (i * _lineHeight) - metrics.Ascent;
        if (yPos - _lineHeight > Bounds.Bottom + ScrollY) break;
        if (yPos + _lineHeight < Bounds.Top + ScrollY) continue;

        // --- DESTAQUE DA LINHA ATUAL ---
        if (i == _cursor.Line)
        {
            _sharedAuxPaint.Color = _theme.LineHighlight;
            var lineRect = new SKRect(Bounds.Left + gutterWidth + ScrollX, yPos + metrics.Ascent, Bounds.Right + ScrollX, yPos + metrics.Descent);
            canvas.DrawRect(lineRect, _sharedAuxPaint);
        }

        float textX = Bounds.Left + gutterWidth + 10; 
        RenderSelection(canvas, i, yPos, gutterWidth, metrics);
        RenderHighlightedLine(canvas, lines[i], textX, yPos);

        if (i == _cursor.Line && _showCursor)
        {
            int safeCol = Math.Min(_cursor.Column, lines[i].Length);
            float cursorX = _font.MeasureText(lines[i].Substring(0, safeCol));
            
            var cursorRect = new SKRect(textX + cursorX, yPos + metrics.Ascent, textX + 2 + cursorX, yPos + metrics.Descent);
            _cursorPaint.Color = _theme.Cursor; 
            canvas.DrawRect(cursorRect, _cursorPaint);
        }
    }
    
    canvas.Restore(); 
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
        // --- SCROLL VERTICAL (Y) ---
        float cursorYTop = _cursor.Line * _lineHeight;
        float cursorYBottom = (_cursor.Line + 1) * _lineHeight;

        if (cursorYBottom > ScrollY + Bounds.Height)
            ScrollY = cursorYBottom - Bounds.Height;
        else if (cursorYTop < ScrollY)
            ScrollY = cursorYTop;

        // --- SCROLL HORIZONTAL (X) ---
        var lines = _buffer.GetLines().ToList();
        if (_cursor.Line < lines.Count)
        {
            string currentLineText = lines[_cursor.Line];
            int safeCol = Math.Min(_cursor.Column, currentLineText.Length);
        
            // posição X do cursor em pixels
            float cursorXPos = _font.MeasureText(currentLineText.Substring(0, safeCol));
            float gutterWidth = GetGutterWidth();
            float viewPortWidth = Bounds.Width - gutterWidth - 20; // Espaço útil do texto

            // Se o cursor passou da borda direita
            if (cursorXPos > ScrollX + viewPortWidth)
            {
                ScrollX = cursorXPos - viewPortWidth + 40; // +40 para dar uma folga
            }
            // Se o cursor voltou para a esquerda (atrás do scroll)
            else if (cursorXPos < ScrollX)
            {
                ScrollX = Math.Max(0, cursorXPos - 20);
            }
        }
    }
    
    public void ApplyScroll(float deltaX, float deltaY)
    {
        if (deltaX != 0)
        {
            ScrollX -= deltaX;
            if (ScrollX < 0) ScrollX = 0;

            float maxScrollX = GetMaxHorizontalScroll();
            if (ScrollX > maxScrollX) ScrollX = maxScrollX;
        }

        if (deltaY != 0)
        {
            ScrollY -= deltaY;
            if (ScrollY < 0) ScrollY = 0;

            float maxScrollY = Math.Max(0, (GetTotalLines() * LineHeight) - (Bounds.Height / 2));
            if (ScrollY > maxScrollY) ScrollY = maxScrollY;
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
    
    private float GetGutterWidth()
    {
        var lines = _buffer.GetLines();
        string maxLineStr = lines.Count().ToString();
        float gutterPadding = 20;
        return _font.MeasureText(maxLineStr) + gutterPadding;
    }
    
    public (int line, int col) GetTextPositionFromMouse(float mouseX, float mouseY)
    {
        float relativeY = mouseY - Bounds.Top + ScrollY;
        int line = (int)(relativeY / _lineHeight);

        float textAreaLeft = Bounds.Left + GetGutterWidth(); 
        float relativeX = (mouseX - textAreaLeft) + ScrollX; 

        float charWidth = _font.MeasureText(" "); 
        int col = (int)Math.Max(0, Math.Round(relativeX / charWidth));

        var lines = _buffer.GetLines().ToList();
        line = Math.Clamp(line, 0, Math.Max(0, lines.Count - 1));
        col = Math.Clamp(col, 0, lines[line].Length);

        return (line, col);
    }
    
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
}
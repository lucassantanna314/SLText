using SkiaSharp;
using SLText.Core.Engine;
using SLText.View.Styles;

namespace SLText.View.Components.Canvas;

public class BracketRenderer
{
    private readonly SKFont _font;
    private EditorTheme _theme;
    private readonly SKPaint _bracketPaint = new() { IsAntialias = true };

    public BracketRenderer(SKFont font, EditorTheme theme)
    {
        _font = font;
        _theme = theme;
        UpdatePaints();
    }

    public void SetTheme(EditorTheme theme)
    {
        _theme = theme;
        UpdatePaints();
    }

    private void UpdatePaints()
    {
        _bracketPaint.Color = _theme.Foreground.WithAlpha(50);
        _bracketPaint.Style = SKPaintStyle.Fill;
    }

    public void Render(SKCanvas canvas, TextBuffer buffer, CursorManager cursor, float textX, SKRect bounds, float lineHeight, float scrollY, SKFontMetrics metrics)
    {
        var lines = buffer.GetLines().ToList();
        if (cursor.Line < 0 || cursor.Line >= lines.Count) return;
        
        string text = lines[cursor.Line];
        int col = cursor.Column;
        char? foundChar = null;
        int foundCol = -1;

        string delimiters = "{}[]<>()";

        if (col < text.Length && delimiters.Contains(text[col]))
        {
            foundChar = text[col];
            foundCol = col;
        }
        else if (col > 0 && col <= text.Length && delimiters.Contains(text[col - 1]))
        {
            foundChar = text[col - 1];
            foundCol = col - 1;
        }

        if (foundChar.HasValue)
        {
            var match = FindMatchingBracket(lines, cursor.Line, foundCol, foundChar.Value);
            if (match != null)
            {
                DrawBracketRect(canvas, buffer, lines, cursor.Line, foundCol, textX, bounds, lineHeight, scrollY, metrics);
                DrawBracketRect(canvas, buffer, lines, match.Value.line, match.Value.col, textX, bounds, lineHeight, scrollY, metrics);
            }
        }
    }

    private void DrawBracketRect(SKCanvas canvas, TextBuffer buffer, List<string> lines, int line, int col, float textX, SKRect bounds, float lineHeight, float scrollY, SKFontMetrics metrics)
    {
        if (line < 0 || line >= lines.Count) return;
        string lineText = lines[line];
        
        if (col < 0 || col >= lineText.Length) return;
        
        float xOffset = _font.MeasureText(lineText.Substring(0, col));
        float yOffset = bounds.Top + (line * lineHeight) - metrics.Ascent;
        
        float charWidth = _font.MeasureText(lineText[col].ToString());
    
        var rect = new SKRect(
            textX + xOffset, 
            yOffset + metrics.Ascent, 
            textX + xOffset + charWidth, 
            yOffset + metrics.Descent
        );
        
        float relativeY = yOffset - scrollY;
        if (relativeY >= -lineHeight && relativeY <= bounds.Height + lineHeight)
        {
            canvas.DrawRect(rect, _bracketPaint);
        }
    }

    private (int line, int col)? FindMatchingBracket(List<string> lines, int startLine, int startCol, char bracket)
    {
        var pairs = new Dictionary<char, char> {
            { '{', '}' }, { '}', '{' },
            { '[', ']' }, { ']', '[' },
            { '<', '>' }, { '>', '<' },
            { '(', ')' }, { ')', '(' }
        };

        int direction = ("{[<(".Contains(bracket)) ? 1 : -1;
        char target = pairs[bracket];
        int counter = 0;

        for (int i = startLine; i >= 0 && i < lines.Count; i += direction)
        {
            string text = lines[i];
            int start = (i == startLine) ? startCol : (direction == 1 ? 0 : text.Length - 1);

            for (int j = start; direction == 1 ? j < text.Length : j >= 0; j += direction)
            {
                if (text[j] == bracket) counter++;
                else if (text[j] == target)
                {
                    counter--;
                    if (counter == 0) return (i, j);
                }
            }
        }
        return null;
    }
}
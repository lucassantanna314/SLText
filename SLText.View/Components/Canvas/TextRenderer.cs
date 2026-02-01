using SkiaSharp;
using SLText.View.Styles;
using System.Text.RegularExpressions;

namespace SLText.View.Components.Canvas;

public class TextRenderer
{
    private EditorTheme _theme;
    private readonly SKFont _font;
    private readonly SKPaint _paint = new() { IsAntialias = true };

    private record TextToken(string Text, SKColor Color);

    public TextRenderer(SKFont font, EditorTheme theme)
    {
        _font = font;
        _theme = theme;
    }

    public void SetTheme(EditorTheme theme) => _theme = theme;

    public void RenderLine(SKCanvas canvas, string text, float x, float y, List<(string pattern, SKColor color)> rules)
    {
        if (string.IsNullOrEmpty(text)) return;

        if (rules == null || rules.Count == 0)
        {
            _paint.Color = _theme.Foreground;
            canvas.DrawText(text, x, y, _font, _paint);
            return;
        }

        var tokens = Tokenize(text, rules);
        float currentX = x;

        foreach (var token in tokens)
        {
            _paint.Color = token.Color;
            canvas.DrawText(token.Text, currentX, y, _font, _paint);
            currentX += _font.MeasureText(token.Text);
        }
    }

    private List<TextToken> Tokenize(string text, List<(string pattern, SKColor color)> rules)
    {
        var matches = new List<(int Index, int Length, SKColor Color)>();

        foreach (var (pattern, color) in rules)
        {
            var results = Regex.Matches(text, pattern);
            foreach (Match match in results)
            {
                matches.Add((match.Index, match.Length, color));
            }
        }

        var sortedMatches = matches.OrderBy(m => m.Index).ToList();

        var tokens = new List<TextToken>();
        int lastIndex = 0;

        foreach (var match in sortedMatches)
        {
            if (match.Index < lastIndex) continue;

            if (match.Index > lastIndex)
            {
                tokens.Add(new TextToken(text.Substring(lastIndex, match.Index - lastIndex), _theme.Foreground));
            }

            tokens.Add(new TextToken(text.Substring(match.Index, match.Length), match.Color));
            lastIndex = match.Index + match.Length;
        }

        if (lastIndex < text.Length)
        {
            tokens.Add(new TextToken(text.Substring(lastIndex), _theme.Foreground));
        }

        return tokens;
    }
}
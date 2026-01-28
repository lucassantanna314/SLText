using SkiaSharp;
using SLText.Core.Engine;
using SLText.View.Abstractions;
using SLText.View.Styles;

namespace SLText.View.Components;

public class StatusBarComponent : IComponent
{
    public SKRect Bounds { get; set; }
    private readonly CursorManager _cursor;
    private readonly TextBuffer _buffer;

    private readonly SKPaint _bgPaint = new();
    private readonly SKPaint _textPaint = new() { IsAntialias = true };
    private readonly SKFont _font;
    
    private EditorTheme _theme = EditorTheme.Dark;
    
    public string LanguageName { get; set; } = "Plain Text";
    public string FileInfo { get; set; } = "New File";

    public StatusBarComponent(CursorManager cursor, TextBuffer buffer)
    {
        _cursor = cursor;
        _buffer = buffer;
        
        string fontPath = Path.Combine(AppContext.BaseDirectory, "Assets", "JetBrainsMono-Regular.ttf");
        SKTypeface typeface;

        if (File.Exists(fontPath))
        {
            typeface = SKTypeface.FromFile(fontPath);
        }
        else
        {
            typeface = SKTypeface.FromFamilyName("monospace", SKFontStyle.Normal);
        }
        
        _font = new SKFont(typeface, 12);
        
        ApplyTheme(_theme);
    }
    
    public void ApplyTheme(EditorTheme theme)
    {
        _theme = theme;
        _bgPaint.Color = _theme.StatusBarBackground;
        _textPaint.Color = _theme.Foreground;
    }

    public void Render(SKCanvas canvas)
    {
        canvas.DrawRect(Bounds, _bgPaint);
        _font.GetFontMetrics(out var metrics);
        float textY = Bounds.MidY - (metrics.Ascent + metrics.Descent) / 2;

        // --- LADO ESQUERDO: Contagem de Linhas e LINGUAGEM ---
        string leftText = $"{_buffer.LineCount} linhas  |  {LanguageName}";
        canvas.DrawText(leftText, Bounds.Left + 15, textY, _font, _textPaint);

        // --- CENTRO: Nome do Arquivo ---
        float fileTextWidth = _font.MeasureText(FileInfo);
        canvas.DrawText(FileInfo, Bounds.MidX - (fileTextWidth / 2), textY, _font, _textPaint);

        // --- DIREITA: Posição do Cursor ---
        string positionText = $"Ln {_cursor.Line + 1}, Col {_cursor.Column + 1}";
        canvas.DrawText(positionText, Bounds.Right - _font.MeasureText(positionText) - 15, textY, _font, _textPaint);
    }

    public void Update(double deltaTime) { }
}
using SkiaSharp;
using SLText.Core.Engine;
using SLText.View.Abstractions;

namespace SLText.View.Components;

public class StatusBarComponent : IComponent
{
    public SKRect Bounds { get; set; }
    private readonly CursorManager _cursor;
    private readonly TextBuffer _buffer;

    // Estilos (Cores típicas de editores Dark)
    private readonly SKPaint _bgPaint = new() { Color = new SKColor(40, 40, 40)  };
    private readonly SKPaint _textPaint = new() { Color = SKColors.White, IsAntialias = true };
    private readonly SKFont _font;
    
    public string LanguageName { get; set; } = "Plain Text";

    public string FileInfo { get; set; } = "Novo Arquivo";

    public StatusBarComponent(CursorManager cursor, TextBuffer buffer)
    {
        _cursor = cursor;
        _buffer = buffer;
        _font = new SKFont(SKTypeface.FromFamilyName("Segoe UI"), 12);
    }

    public void Render(SKCanvas canvas)
    {
        canvas.DrawRect(Bounds, _bgPaint);
        _font.GetFontMetrics(out var metrics);
        float textY = Bounds.MidY - (metrics.Ascent + metrics.Descent) / 2;

        // --- LADO ESQUERDO: Contagem de Linhas e LINGUAGEM ---
        // Agora incluímos o nome da linguagem aqui ou no centro
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
using SkiaSharp;
using SLText.Core.Engine;
using SLText.Core.Engine.Model;
using SLText.View.Abstractions;
using SLText.View.Styles;

namespace SLText.View.Components;

public class StatusBarComponent : IComponent
{
    public SKRect Bounds { get; set; }
    private CursorManager _cursor;
    private TextBuffer _buffer;

    private readonly SKFont _font;
    private EditorTheme _theme = EditorTheme.Dark;
    
    public string LanguageName { get; set; } = "Plain Text";
    public string FileInfo { get; set; } = "New File";
    private readonly EditorComponent _editor;
    
    private RunConfiguration? _activeConfiguration;
    public SKRect PlayButtonBounds { get; private set; }
    public SKRect SelectorBounds { get; private set; }

    public void SetActiveConfiguration(RunConfiguration? config) 
    {
        _activeConfiguration = config;
    }
    
    public StatusBarComponent(CursorManager cursor, TextBuffer buffer, EditorComponent editor)
    {
        _cursor = cursor;
        _buffer = buffer;
        _editor = editor;
        
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
    }
    
    public void ApplyTheme(EditorTheme theme) => _theme = theme;
    
    public void UpdateActiveBuffer(TextBuffer buffer, CursorManager cursor)
    {
        _buffer = buffer;
        _cursor = cursor;
    }

    public void Render(SKCanvas canvas)
    {
        using var bgPaint = new SKPaint { Color = _theme.StatusBarBackground };
        using var textPaint = new SKPaint { Color = _theme.Foreground, IsAntialias = true };
        
        canvas.DrawRect(Bounds, bgPaint);
        
        _font.GetFontMetrics(out var metrics);
        float textY = Bounds.MidY - (metrics.Ascent + metrics.Descent) / 2;

        // --- LADO ESQUERDO ---
        string leftText = $"{_buffer.LineCount} L  |  {LanguageName}  | {_editor.FontSize:0}pt";
        canvas.DrawText(leftText, Bounds.Left + 15, textY, _font, textPaint);

        // --- SEÇÃO DE EXECUÇÃO ---
        float runSectionX = Bounds.Left + 220;
        
        // Play 
        using var playPaint = new SKPaint { Color = SKColors.LightGreen, IsAntialias = true };
        PlayButtonBounds = new SKRect(runSectionX, Bounds.Top + 5, runSectionX + 20, Bounds.Bottom - 5);
        
        var path = new SKPath();
        path.MoveTo(PlayButtonBounds.Left + 4, PlayButtonBounds.Top + 3);
        path.LineTo(PlayButtonBounds.Left + 4, PlayButtonBounds.Bottom - 3);
        path.LineTo(PlayButtonBounds.Right - 4, PlayButtonBounds.MidY);
        path.Close();
        canvas.DrawPath(path, playPaint);

        // Texto da Configuração
        string configName = _activeConfiguration?.Name ?? "Select Config...";
        float nameWidth = _font.MeasureText(configName);
        SelectorBounds = new SKRect(PlayButtonBounds.Right + 8, Bounds.Top, PlayButtonBounds.Right + 20 + nameWidth, Bounds.Bottom);
        
        canvas.DrawText($"{configName} ▾", SelectorBounds.Left, textY, _font, textPaint);

        // --- CENTRO: Nome do Arquivo ---
        float fileTextWidth = _font.MeasureText(FileInfo);
        canvas.DrawText(FileInfo, Bounds.MidX - (fileTextWidth / 2), textY, _font, textPaint);

        // --- DIREITA: Posição do Cursor ---
        string positionText = $"Ln {_cursor.Line + 1}, Col {_cursor.Column + 1}";
        canvas.DrawText(positionText, Bounds.Right - _font.MeasureText(positionText) - 15, textY, _font, textPaint);
    }
    public void Update(double deltaTime) { }
}
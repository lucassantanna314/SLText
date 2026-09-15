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

    // --- GitHub Branch Button (Fase 2) ---
    public SKRect BranchButtonBounds { get; private set; }
    private string _currentBranch = "—";
    private bool _hasGitConnection = false;

    public void SetBranchName(string? name) => _currentBranch = name ?? "—";
    public void SetGitConnection(bool connected) => _hasGitConnection = connected;

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
        float leftWidth = _font.MeasureText(leftText);
        canvas.DrawText(leftText, Bounds.Left + 15, textY, _font, textPaint);

        // --- DIREITA: Cursor position ---
        string positionText = $"Ln {_cursor.Line + 1}, Col {_cursor.Column + 1}";
        float positionWidth = _font.MeasureText(positionText);
        float rightX = Bounds.Right - 15 - positionWidth;
        canvas.DrawText(positionText, rightX, textY, _font, textPaint);

        // --- CENTRO: Arquivo name ---
        float fileTextWidth = _font.MeasureText(FileInfo);
        canvas.DrawText(FileInfo, Bounds.MidX - (fileTextWidth / 2), textY, _font, textPaint);

        // --- SEÇÃO EXECUTAVEL (direita antes do cursor) ---
        float sectionEnd = rightX - 10; // 10px gap before cursor
        string configNameStr = _activeConfiguration?.Name ?? "Select Config...";
        float configWidth = _font.MeasureText($"{configNameStr} ▾");
        float selectorWidth = configWidth + 30; // button area padding
        float playWidth = 30; // play triangle width
        
        // Branch text + arrow
        string branchText = _hasGitConnection ? $"▲ {_currentBranch} ▾" : "";
        float branchWidth = _hasGitConnection ? _font.MeasureText(branchText) + 15 : 0;

        // Calculate positions from right to left within the exec section
        float selLeft = sectionEnd - selectorWidth;
        SelectorBounds = new SKRect(selLeft, Bounds.Top, selLeft + selectorWidth, Bounds.Bottom);

        float playRight = selLeft - 8; // 8px gap between selector and play
        PlayButtonBounds = new SKRect(playRight - playWidth, Bounds.Top + 5, playRight, Bounds.Bottom - 5);

        float branchRight = playRight - 15; // 15px gap between play and branch
        BranchButtonBounds = branchWidth > 0 
            ? new SKRect(branchRight - branchWidth, Bounds.Top, branchRight, Bounds.Bottom)
            : SKRect.Empty;

        // Draw branch button
        if (_hasGitConnection && branchWidth > 0)
        {
            using var branchPaint = new SKPaint
            {
                Color = _theme.Foreground,
                IsAntialias = true
            };
            canvas.DrawText(branchText, BranchButtonBounds.Left, textY, _font, branchPaint);
        }

        // Draw play triangle
        bool hasGit = _hasGitConnection;
        using var playPaint = new SKPaint 
        { 
            Color = hasGit ? SKColors.LightGreen : SKColors.Gray.WithAlpha(180), 
            IsAntialias = true 
        };
        
        var path = new SKPath();
        path.MoveTo(PlayButtonBounds.Left + 4, PlayButtonBounds.Top + 5);
        path.LineTo(PlayButtonBounds.Left + 4, PlayButtonBounds.Bottom - 5);
        path.LineTo(PlayButtonBounds.Right - 6, PlayButtonBounds.MidY);
        path.Close();
        canvas.DrawPath(path, playPaint);

        // Draw selector text
        canvas.DrawText($"{configNameStr} ▾", SelectorBounds.Left + 10, textY, _font, textPaint);
    }
    public void Update(double deltaTime) { }
}
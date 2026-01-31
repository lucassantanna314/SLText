using SkiaSharp;
using SLText.View.Styles;

namespace SLText.View.Components;

public class ModalComponent
{
    public bool IsVisible { get; set; }
    public string Title { get; set; } = "Confirmação";
    public string Message { get; set; } = "";
    
    public Action? OnYes { get; set; }
    public Action? OnNo { get; set; }
    public Action? OnCancel { get; set; }
    
    private SKRect _yesBtn;
    private SKRect _noBtn;
    private SKRect _cancelBtn;
    
    private readonly SKFont _font;
    private readonly SKFont _fontBold;
    private DateTime _lastClosedTime = DateTime.MinValue;
    public bool IsRecentlyClosed => (DateTime.Now - _lastClosedTime).TotalMilliseconds < 100;
    
    public ModalComponent()
    {
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

        _font = new SKFont(typeface, 14);
        _fontBold = new SKFont(typeface, 16) { Embolden = true };
    }
    
    public void Render(SKCanvas canvas, SKRect windowBounds, EditorTheme theme)
{
    if (!IsVisible) return;

    using var dimPaint = new SKPaint { Color = new SKColor(0, 0, 0, 180) };
    canvas.DrawRect(windowBounds, dimPaint);

    float width = 450;
    float height = 200;
    var rect = new SKRect(
        windowBounds.MidX - width / 2,
        windowBounds.MidY - height / 2,
        windowBounds.MidX + width / 2,
        windowBounds.MidY + height / 2
    );

    using var bgPaint = new SKPaint { Color = theme.Background, IsAntialias = true };
    canvas.DrawRoundRect(rect, 8, 8, bgPaint);

    using var borderPaint = new SKPaint { Color = theme.GutterForeground, Style = SKPaintStyle.Stroke, StrokeWidth = 1, IsAntialias = true };
    canvas.DrawRoundRect(rect, 8, 8, borderPaint);

    using var textPaint = new SKPaint { Color = theme.Foreground, IsAntialias = true };
    
    canvas.DrawText(Title, rect.Left + 20, rect.Top + 40, _fontBold, textPaint);
    
    float maxWidth = rect.Width - 40; 
    float x = rect.Left + 20;
    float y = rect.Top + 80;        
    float lineHeight = _font.Size + 5; 

    string[] words = Message.Split(' '); 
    string currentLine = "";

    foreach (var word in words)
    {
        string testLine = string.IsNullOrEmpty(currentLine) ? word : $"{currentLine} {word}";
        float measuredWidth = _font.MeasureText(testLine);

        if (measuredWidth > maxWidth && !string.IsNullOrEmpty(currentLine))
        {
            canvas.DrawText(currentLine, x, y, _font, textPaint);
            currentLine = word; 
            y += lineHeight;   
        }
        else
        {
            currentLine = testLine;
        }
    }
    
    if (!string.IsNullOrEmpty(currentLine))
    {
        canvas.DrawText(currentLine, x, y, _font, textPaint);
    }

    RenderButtons(canvas, rect, theme, textPaint);
}
    
    private void RenderButtons(SKCanvas canvas, SKRect dialogRect, EditorTheme theme, SKPaint textPaint)
    {
        float btnW = 80;
        float btnH = 30;
        float spacing = 10;
        float y = dialogRect.Bottom - 50;

        _cancelBtn = new SKRect(dialogRect.Right - 20 - btnW, y, dialogRect.Right - 20, y + btnH);
        _noBtn = new SKRect(_cancelBtn.Left - spacing - btnW, y, _cancelBtn.Left - spacing, y + btnH);
        _yesBtn = new SKRect(_noBtn.Left - spacing - btnW, y, _noBtn.Left - spacing, y + btnH);

        DrawButton(canvas, _yesBtn, "Sim (S)", theme, textPaint);
        DrawButton(canvas, _noBtn, "Não (N)", theme, textPaint);
        DrawButton(canvas, _cancelBtn, "Esc", theme, textPaint);
    }
    
    private void DrawButton(SKCanvas canvas, SKRect rect, string label, EditorTheme theme, SKPaint textPaint)
    {
        using var p = new SKPaint { Color = theme.LineHighlight, IsAntialias = true };
        canvas.DrawRoundRect(rect, 4, 4, p);
        
        float textX = rect.MidX - (textPaint.MeasureText(label) / 2);
        canvas.DrawText(label, textX, rect.MidY + 5, textPaint);
    }
    
    public bool HandleKeyDown(string key)
    {
        if (!IsVisible) return false;

        if (key == "S" || key == "Enter" || key == "N" || key == "Escape") 
        {
            IsVisible = false;
            _lastClosedTime = DateTime.Now; 

            if (key == "S" || key == "Enter") OnYes?.Invoke();
            else if (key == "N") OnNo?.Invoke();
            else OnCancel?.Invoke();
        }

        return true;
    }
    
    public bool HandleClick(float x, float y)
    {
        if (!IsVisible) return false;

        if (_yesBtn.Contains(x, y)) { IsVisible = false; OnYes?.Invoke(); return true; }
        if (_noBtn.Contains(x, y)) { IsVisible = false; OnNo?.Invoke(); return true; }
        if (_cancelBtn.Contains(x, y)) { IsVisible = false; OnCancel?.Invoke(); return true; }

        return true; 
    }
    
    public void Show(string title, string message, Action? onYes = null, Action? onNo = null, Action? onCancel = null)
    {
        Title = title;
        Message = message;
        OnYes = onYes;
        OnNo = onNo;
        OnCancel = onCancel;
        IsVisible = true;
    }
}
using SkiaSharp;

namespace SLText.View.Styles;

public class EditorTheme
{
    public SKColor Background { get; set; } = new SKColor(30, 30, 30);
    public SKColor Foreground { get; set; } = SKColors.White;
    public SKColor GutterBackground { get; set; } = new SKColor(40, 40, 40);
    public SKColor GutterForeground { get; set; } = new SKColor(120, 120, 120);
    public SKColor Cursor { get; set; } = SKColors.Chocolate;
    public SKColor Selection { get; set; } = new SKColor(60, 120, 200, 100);
    public SKColor StatusBarBackground { get; set; } = new SKColor(0, 122, 204);
    public SKColor LineHighlight { get; set; } = new SKColor(255, 255, 255, 8);

    public static EditorTheme Dark => new();
    
    public SKColor Keyword { get; set; } = new SKColor(204, 120, 50);
    public SKColor Type { get; set; } = new SKColor(17, 186, 161);
    public SKColor Method { get; set; } = new SKColor(255, 198, 109);
    public SKColor String { get; set; } = new SKColor(106, 135, 89);
    public SKColor Comment { get; set; } = new SKColor(133, 153, 0);
    public SKColor Number { get; set; } = new SKColor(104, 151, 187);
}
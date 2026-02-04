using SkiaSharp;

namespace SLText.View.Styles;

public class EditorTheme
{
    public SKColor Background { get; set; } = new SKColor(30, 30, 30);    
    public SKColor Foreground { get; set; } = new SKColor(188, 190, 196);
    public SKColor GutterBackground { get; set; } = new SKColor(40, 40, 40);
    public SKColor GutterForeground { get; set; } = new SKColor(120, 120, 120);
    public SKColor Cursor { get; set; } = SKColors.Chocolate;
    public SKColor SelectionBackground { get; set; } = new SKColor(33, 66, 131, 180);    
    public SKColor StatusBarBackground { get; set; } = new SKColor(25, 25, 28);
    public SKColor LineHighlight { get; set; } = new SKColor(40, 40, 40);
    
    public static EditorTheme Dark => new();
    
    public SKColor Keyword { get; set; } = new SKColor(86, 156, 214);
    public SKColor Type { get; set; } = new SKColor(204, 120, 50);    
    public SKColor Method { get; set; } = new SKColor(255, 198, 109);    
    public SKColor String { get; set; } = new SKColor(106, 135, 89);   
    public SKColor Comment { get; set; } = new SKColor(128, 128, 128); 
    public SKColor Number { get; set; } = new SKColor(104, 151, 187); 
    public SKColor Operator { get; set; } = new SKColor(187, 187, 187); 
    public SKColor Attribute { get; set; } = new SKColor(187, 181, 41);
}
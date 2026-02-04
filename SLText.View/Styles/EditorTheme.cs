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
    public SKColor LineHighlight { get; set; } = new SKColor(40, 50, 80, 100);
    
    // --- Cores do Explorer ---
    public SKColor ExplorerBackground { get; set; } = new SKColor(20, 20, 25);
    public SKColor ExplorerItemActive { get; set; } = new SKColor(80, 160, 255); // Linha azul lateral
    public SKColor ExplorerSelection { get; set; } = new SKColor(40, 50, 80, 100); // Fundo do item selecionado
    public SKColor FolderIcon { get; set; } = new SKColor(240, 200, 100);
    public SKColor FileIconDefault { get; set; } = new SKColor(180, 180, 180);
    public SKColor FileIconCSharp { get; set; } = new SKColor(120, 180, 240);
    
    // --- Cores das Abas ---
    public SKColor TabActiveAccent { get; set; } = SKColors.CornflowerBlue;
    public SKColor TabDirtyBackground { get; set; } = new SKColor(45, 0, 90, 180);
    public SKColor TabDirtyForeground { get; set; } = new SKColor(220, 180, 255);
    
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
using SkiaSharp;

namespace SLText.View.Styles;

public class EditorTheme
{
    public SKColor Background { get; set; }
    public SKColor Foreground { get; set; }
    public SKColor GutterBackground { get; set; }
    public SKColor GutterForeground { get; set; }
    public SKColor Cursor { get; set; }
    public SKColor SelectionBackground { get; set; }
    public SKColor StatusBarBackground { get; set; }
    public SKColor LineHighlight { get; set; }

    public SKColor ExplorerBackground { get; set; }
    public SKColor ExplorerItemActive { get; set; }
    public SKColor ExplorerSelection { get; set; }
    public SKColor FolderIcon { get; set; }
    public SKColor FileIconDefault { get; set; }
    public SKColor FileIconCSharp { get; set; }

    public SKColor TabActiveAccent { get; set; }
    public SKColor TabDirtyBackground { get; set; }
    public SKColor TabDirtyForeground { get; set; }

    public SKColor Keyword { get; set; }
    public SKColor Type { get; set; }
    public SKColor Method { get; set; }
    public SKColor String { get; set; }
    public SKColor Comment { get; set; }
    public SKColor Number { get; set; }
    public SKColor Operator { get; set; }
    public SKColor Attribute { get; set; }

    public static EditorTheme Dark => new EditorTheme
    {
        Background = new SKColor(27, 27, 29),         
        Foreground = new SKColor(207, 209, 211),      
        GutterBackground = new SKColor(27, 27, 29),   
        GutterForeground = new SKColor(85, 87, 91),   
        Cursor = new SKColor(71, 142, 245),            
        SelectionBackground = new SKColor(44, 73, 115), 
        StatusBarBackground = new SKColor(32, 32, 34), 
        LineHighlight = new SKColor(35, 36, 40),       

        ExplorerBackground = new SKColor(22, 22, 23),  
        ExplorerItemActive = new SKColor(111, 157, 240),
        ExplorerSelection = new SKColor(38, 41, 49),
        FolderIcon = new SKColor(230, 185, 90),
        FileIconDefault = new SKColor(150, 155, 160),
        FileIconCSharp = new SKColor(122, 115, 225),   

        TabActiveAccent = new SKColor(62, 134, 243),
        TabDirtyBackground = new SKColor(55, 30, 30),
        TabDirtyForeground = new SKColor(255, 140, 140),

        Keyword = new SKColor(207, 142, 240),         
        Type = new SKColor(72, 192, 198),           
        Method = new SKColor(230, 190, 120),           
        String = new SKColor(110, 180, 120),         
        Comment = new SKColor(110, 115, 130),         
        Number = new SKColor(245, 140, 100),          
        Operator = new SKColor(190, 195, 205),
        Attribute = new SKColor(180, 210, 110)
    };
    
    public static EditorTheme Light => new EditorTheme
    {
        Background = new SKColor(255, 255, 255),     
        Foreground = new SKColor(31, 35, 40),        
        GutterBackground = new SKColor(246, 248, 250), 
        GutterForeground = new SKColor(175, 184, 193),
        Cursor = new SKColor(5, 113, 230),
        SelectionBackground = new SKColor(218, 235, 255), 
        StatusBarBackground = new SKColor(246, 248, 250),
        LineHighlight = new SKColor(240, 245, 255),

        ExplorerBackground = new SKColor(246, 248, 250),
        ExplorerItemActive = new SKColor(9, 105, 218),
        ExplorerSelection = new SKColor(230, 235, 241),
        FolderIcon = new SKColor(153, 120, 30),
        FileIconDefault = new SKColor(87, 96, 106),
        FileIconCSharp = new SKColor(50, 120, 190),

        TabActiveAccent = new SKColor(253, 140, 18),  
        TabDirtyBackground = new SKColor(255, 240, 240),
        TabDirtyForeground = new SKColor(207, 34, 46),

        Keyword = new SKColor(207, 34, 46),          
        Type = new SKColor(149, 56, 201),           
        Method = new SKColor(9, 105, 218),            
        String = new SKColor(10, 116, 51),            
        Comment = new SKColor(87, 96, 106),
        Number = new SKColor(5, 113, 230),
        Operator = new SKColor(31, 35, 40),
        Attribute = new SKColor(120, 70, 0)
    };
}
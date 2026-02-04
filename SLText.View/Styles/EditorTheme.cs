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
        Background = new SKColor(30, 30, 30),
        Foreground = new SKColor(188, 190, 196),
        GutterBackground = new SKColor(40, 40, 40),
        GutterForeground = new SKColor(120, 120, 120),
        Cursor = SKColors.Chocolate,
        SelectionBackground = new SKColor(33, 66, 131, 180),
        StatusBarBackground = new SKColor(25, 25, 28),
        LineHighlight = new SKColor(40, 50, 80, 100),

        ExplorerBackground = new SKColor(20, 20, 25),
        ExplorerItemActive = new SKColor(80, 160, 255),
        ExplorerSelection = new SKColor(40, 50, 80, 100),
        FolderIcon = new SKColor(240, 200, 100),
        FileIconDefault = new SKColor(180, 180, 180),
        FileIconCSharp = new SKColor(120, 180, 240),

        TabActiveAccent = SKColors.CornflowerBlue,
        TabDirtyBackground = new SKColor(45, 0, 90, 180),
        TabDirtyForeground = new SKColor(220, 180, 255),

        Keyword = new SKColor(86, 156, 214),
        Type = new SKColor(204, 120, 50),
        Method = new SKColor(255, 198, 109),
        String = new SKColor(106, 135, 89),
        Comment = new SKColor(128, 128, 128),
        Number = new SKColor(104, 151, 187),
        Operator = new SKColor(187, 187, 187),
        Attribute = new SKColor(187, 181, 41)
    };

    public static EditorTheme Light => new EditorTheme
    {
        Background = new SKColor(245, 245, 247), 
        Foreground = new SKColor(36, 41, 46),
        GutterBackground = new SKColor(238, 238, 240), 
        GutterForeground = new SKColor(160, 160, 160),
        Cursor = new SKColor(0, 92, 197),
        SelectionBackground = new SKColor(187, 225, 255, 150),
        StatusBarBackground = new SKColor(225, 225, 230),
        LineHighlight = new SKColor(230, 235, 245, 120),

        ExplorerBackground = new SKColor(235, 235, 238),
        ExplorerItemActive = new SKColor(3, 102, 214),
        ExplorerSelection = new SKColor(215, 225, 235),
        FolderIcon = new SKColor(210, 170, 50),
        FileIconDefault = new SKColor(100, 100, 100),
        FileIconCSharp = new SKColor(50, 110, 180),

        TabActiveAccent = new SKColor(3, 102, 214),
        TabDirtyBackground = new SKColor(255, 230, 230),
        TabDirtyForeground = new SKColor(180, 50, 50),

        Keyword = new SKColor(215, 58, 73),
        Type = new SKColor(3, 102, 214),
        Method = new SKColor(111, 66, 193),
        String = new SKColor(34, 134, 58),
        Comment = new SKColor(106, 115, 125),
        Number = new SKColor(0, 92, 197),
        Operator = new SKColor(215, 58, 73),
        Attribute = new SKColor(227, 98, 9)
    };
}
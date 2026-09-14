using SkiaSharp;

namespace SLText.View.Styles;

/// <summary>
/// Paleta completa usada pelo editor, explorer, abas e status bar.
/// Todas as cores são ARGB (alpha = 255), então podem ser usadas direto no SkiaSharp.
/// </summary>
public class EditorTheme
{
    public string Name { get; set; } = "Custom";
    public bool IsDark { get; set; } = true;

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

    // ------------------------------------------------------------------
    // DARK
    // ------------------------------------------------------------------

    public static EditorTheme Dark => new EditorTheme
    {
        Name = "Dark",
        IsDark = true,
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

    /// <summary>Midnight — azul-noite profundo, acentos em azul elétrico e menta.</summary>
    public static EditorTheme Midnight => new EditorTheme
    {
        Name = "Midnight",
        IsDark = true,
        Background = new SKColor(17, 22, 34),
        Foreground = new SKColor(205, 214, 232),
        GutterBackground = new SKColor(15, 20, 30),
        GutterForeground = new SKColor(72, 86, 114),
        Cursor = new SKColor(100, 181, 246),
        SelectionBackground = new SKColor(41, 62, 104),
        StatusBarBackground = new SKColor(21, 27, 41),
        LineHighlight = new SKColor(24, 31, 47),

        ExplorerBackground = new SKColor(13, 17, 27),
        ExplorerItemActive = new SKColor(120, 190, 255),
        ExplorerSelection = new SKColor(33, 44, 68),
        FolderIcon = new SKColor(240, 196, 110),
        FileIconDefault = new SKColor(138, 150, 176),
        FileIconCSharp = new SKColor(110, 168, 254),

        TabActiveAccent = new SKColor(96, 165, 250),
        TabDirtyBackground = new SKColor(62, 33, 40),
        TabDirtyForeground = new SKColor(255, 148, 148),

        Keyword = new SKColor(126, 178, 255),
        Type = new SKColor(94, 214, 202),
        Method = new SKColor(255, 209, 128),
        String = new SKColor(134, 214, 152),
        Comment = new SKColor(94, 108, 138),
        Number = new SKColor(255, 158, 105),
        Operator = new SKColor(163, 178, 205),
        Attribute = new SKColor(198, 162, 255)
    };

    /// <summary>Nordic — inspirado no Nord: frio, dessaturado, confortável para sessões longas.</summary>
    public static EditorTheme Nordic => new EditorTheme
    {
        Name = "Nordic",
        IsDark = true,
        Background = new SKColor(46, 52, 64),
        Foreground = new SKColor(216, 222, 233),
        GutterBackground = new SKColor(42, 47, 58),
        GutterForeground = new SKColor(99, 112, 134),
        Cursor = new SKColor(136, 192, 208),
        SelectionBackground = new SKColor(67, 78, 99),
        StatusBarBackground = new SKColor(40, 45, 56),
        LineHighlight = new SKColor(53, 59, 72),

        ExplorerBackground = new SKColor(37, 42, 51),
        ExplorerItemActive = new SKColor(143, 188, 187),
        ExplorerSelection = new SKColor(60, 70, 86),
        FolderIcon = new SKColor(235, 203, 139),
        FileIconDefault = new SKColor(154, 166, 184),
        FileIconCSharp = new SKColor(94, 129, 172),

        TabActiveAccent = new SKColor(129, 161, 193),
        TabDirtyBackground = new SKColor(66, 46, 48),
        TabDirtyForeground = new SKColor(236, 150, 140),

        Keyword = new SKColor(129, 161, 193),
        Type = new SKColor(143, 188, 187),
        Method = new SKColor(136, 192, 208),
        String = new SKColor(163, 190, 140),
        Comment = new SKColor(97, 110, 136),
        Number = new SKColor(180, 142, 173),
        Operator = new SKColor(229, 233, 240),
        Attribute = new SKColor(235, 203, 139)
    };

    // ------------------------------------------------------------------
    // LIGHT
    // ------------------------------------------------------------------

    public static EditorTheme Light => new EditorTheme
    {
        Name = "Light",
        IsDark = false,
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

    /// <summary>Sepia — papel quente/creme, baixo cansaço visual sob luz ambiente.</summary>
    public static EditorTheme Sepia => new EditorTheme
    {
        Name = "Sepia",
        IsDark = false,
        Background = new SKColor(251, 244, 230),
        Foreground = new SKColor(67, 56, 42),
        GutterBackground = new SKColor(245, 236, 218),
        GutterForeground = new SKColor(176, 160, 130),
        Cursor = new SKColor(180, 83, 9),
        SelectionBackground = new SKColor(238, 226, 196),
        StatusBarBackground = new SKColor(243, 233, 214),
        LineHighlight = new SKColor(247, 240, 224),

        ExplorerBackground = new SKColor(244, 235, 216),
        ExplorerItemActive = new SKColor(154, 52, 18),
        ExplorerSelection = new SKColor(233, 221, 194),
        FolderIcon = new SKColor(146, 64, 14),
        FileIconDefault = new SKColor(120, 113, 108),
        FileIconCSharp = new SKColor(30, 64, 175),

        TabActiveAccent = new SKColor(217, 119, 6),
        TabDirtyBackground = new SKColor(253, 232, 226),
        TabDirtyForeground = new SKColor(159, 18, 57),

        Keyword = new SKColor(170, 13, 145),
        Type = new SKColor(32, 81, 209),
        Method = new SKColor(146, 64, 14),
        String = new SKColor(38, 100, 62),
        Comment = new SKColor(140, 126, 102),
        Number = new SKColor(178, 60, 24),
        Operator = new SKColor(101, 86, 65),
        Attribute = new SKColor(133, 77, 14)
    };

    /// <summary>Rose — branco rosado frio com acentos em magenta e pêssego.</summary>
    public static EditorTheme Rose => new EditorTheme
    {
        Name = "Rose",
        IsDark = false,
        Background = new SKColor(255, 250, 251),
        Foreground = new SKColor(63, 44, 55),
        GutterBackground = new SKColor(252, 241, 244),
        GutterForeground = new SKColor(190, 160, 172),
        Cursor = new SKColor(219, 39, 119),
        SelectionBackground = new SKColor(252, 231, 243),
        StatusBarBackground = new SKColor(252, 242, 245),
        LineHighlight = new SKColor(253, 244, 246),

        ExplorerBackground = new SKColor(252, 243, 246),
        ExplorerItemActive = new SKColor(190, 24, 93),
        ExplorerSelection = new SKColor(250, 229, 238),
        FolderIcon = new SKColor(194, 65, 12),
        FileIconDefault = new SKColor(126, 102, 114),
        FileIconCSharp = new SKColor(124, 58, 237),

        TabActiveAccent = new SKColor(236, 72, 153),
        TabDirtyBackground = new SKColor(254, 226, 226),
        TabDirtyForeground = new SKColor(190, 18, 60),

        Keyword = new SKColor(190, 24, 93),
        Type = new SKColor(109, 40, 217),
        Method = new SKColor(194, 65, 12),
        String = new SKColor(21, 128, 61),
        Comment = new SKColor(155, 132, 144),
        Number = new SKColor(3, 105, 161),
        Operator = new SKColor(87, 66, 78),
        Attribute = new SKColor(14, 116, 144)
    };
}
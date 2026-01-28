using Silk.NET.Input;

namespace SLText.View.UI.Input;

public static class KeyboardMapper
{
    public static string? Normalize(Key key) => key switch
    {
        Key.Up        => "UpArrow",
        Key.Down      => "DownArrow",
        Key.Left      => "LeftArrow",
        Key.Right     => "RightArrow",
        Key.Enter     => "Enter",
        Key.Backspace => "Backspace",
        Key.Delete    => "Delete",
        _         => key.ToString()
    };
    
    public static string? MapNavigationKey(Key key) => Normalize(key);
    
    public static string MapShortcutKey(Key key) => Normalize(key)!;
}
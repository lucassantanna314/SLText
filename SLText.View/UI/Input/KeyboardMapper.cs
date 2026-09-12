using Silk.NET.Input;

namespace SLText.View.UI.Input;

/// <summary>
/// Maps a physical <see cref="Key"/> to the string the shortcut tables in
/// <c>SLText.Core.Engine.InputHandler</c> are keyed on.
/// </summary>
/// <remarks>
/// Two contracts matter here:
/// <list type="bullet">
/// <item>
/// Navigation and editing keys map to <em>names</em> ("UpArrow", "Backspace", "PageDown") that
/// <c>IsMovementKey</c>/<c>IsDestructiveKey</c> and the registered shortcuts recognise.
/// </item>
/// <item>
/// Keys that produce text map to their <em>single character</em>. <c>HandleShortcut</c> ignores a
/// one-character key when no modifier is held, because the character itself is delivered separately
/// through the KeyChar callback. Returning an enum name instead (the previous fallback did this for
/// everything but ten keys) made "Space" and "Number2".."Number9" five and seven characters long, so
/// they no longer short-circuited: every space and every digit 2-9 flushed the pending typing
/// command, and Ctrl+Z then undid one character at a time instead of one burst.
/// </item>
/// </list>
/// </remarks>
public static class KeyboardMapper
{
    public static string Normalize(Key key) => key switch
    {
        // Navigation
        Key.Up => "UpArrow",
        Key.Down => "DownArrow",
        Key.Left => "LeftArrow",
        Key.Right => "RightArrow",
        Key.Home => "Home",
        Key.End => "End",
        Key.PageUp => "PageUp",
        Key.PageDown => "PageDown",

        // Editing
        Key.Enter => "Enter",
        Key.Backspace => "Backspace",
        Key.Delete => "Delete",
        Key.Tab => "Tab",
        Key.Escape => "Escape",

        // Digits - previously only 0 and 1 were mapped.
        Key.Number0 or Key.Keypad0 => "0",
        Key.Number1 or Key.Keypad1 => "1",
        Key.Number2 or Key.Keypad2 => "2",
        Key.Number3 or Key.Keypad3 => "3",
        Key.Number4 or Key.Keypad4 => "4",
        Key.Number5 or Key.Keypad5 => "5",
        Key.Number6 or Key.Keypad6 => "6",
        Key.Number7 or Key.Keypad7 => "7",
        Key.Number8 or Key.Keypad8 => "8",
        Key.Number9 or Key.Keypad9 => "9",

        // Printable punctuation, so each is a single character.
        Key.Space => " ",
        Key.Comma => ",",
        Key.Period => ".",
        Key.Semicolon => ";",
        Key.Apostrophe => "'",
        Key.Slash => "/",
        Key.BackSlash => "\\",
        Key.Minus => "-",
        Key.Equal => "=",
        Key.GraveAccent => "`",
        Key.LeftBracket => "[",
        Key.RightBracket => "]",

        // Letters already stringify to their uppercase name ("A"), which is what the tables expect.
        _ => key.ToString(),
    };
}

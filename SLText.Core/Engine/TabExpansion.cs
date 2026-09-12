using System.Text;

namespace SLText.Core.Engine;

/// <summary>
/// Maps between buffer columns (where a tab is one character) and display columns (where a tab
/// occupies <see cref="TabSize"/> cells).
/// </summary>
/// <remarks>
/// The editor used to call <c>content.Replace("\t", "    ")</c> when loading a file so the renderer
/// could cope. That put spaces into the <see cref="TextBuffer"/>, so the next save silently rewrote
/// the file with a different indentation style. Expansion now happens only at render time: the
/// buffer keeps the bytes that are on disk.
/// <para>
/// Expansion is fixed-width rather than true tab stops. For leading indentation - the case that
/// matters for source code - the two are identical, and fixed-width keeps the column mapping a pure
/// per-character function.
/// </para>
/// </remarks>
public static class TabExpansion
{
    public const int TabSize = 4;
    private const char Tab = '\t';

    /// <summary>Expands tabs to spaces. Returns the same instance when there is nothing to do.</summary>
    public static string Expand(string? text)
    {
        if (string.IsNullOrEmpty(text)) return string.Empty;
        if (text.IndexOf(Tab) < 0) return text;

        var sb = new StringBuilder(text.Length + TabSize);
        foreach (char c in text)
        {
            if (c == Tab) sb.Append(' ', TabSize);
            else sb.Append(c);
        }
        return sb.ToString();
    }

    /// <summary>True when expanding would change the string, i.e. it contains a tab.</summary>
    public static bool ContainsTab(string? text) => text?.IndexOf(Tab) >= 0;

    /// <summary>Converts a 0-based buffer column to the equivalent display column.</summary>
    public static int ToDisplayColumn(string? line, int bufferColumn)
    {
        if (string.IsNullOrEmpty(line) || bufferColumn <= 0) return Math.Max(0, bufferColumn);

        int display = 0;
        int limit = Math.Min(bufferColumn, line.Length);

        for (int i = 0; i < limit; i++)
            display += line[i] == Tab ? TabSize : 1;

        // Columns past the end of the line advance one cell per character.
        if (bufferColumn > line.Length) display += bufferColumn - line.Length;

        return display;
    }

    /// <summary>
    /// Converts a 0-based display column back to the nearest buffer column, clamped to the line.
    /// </summary>
    public static int ToBufferColumn(string? line, int displayColumn)
    {
        if (string.IsNullOrEmpty(line)) return 0;
        if (displayColumn <= 0) return 0;

        int display = 0;
        for (int i = 0; i < line.Length; i++)
        {
            int width = line[i] == Tab ? TabSize : 1;
            if (display + width > displayColumn) return i;
            display += width;
        }

        // Beyond the line's rendered width: land on the end of the buffer text.
        return line.Length;
    }
}

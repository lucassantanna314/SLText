namespace SLText.Core.Engine.Model;

public class TabInfo
{
    public string Id { get; } = Guid.NewGuid().ToString();
    public string? FilePath { get; set; }
    public string Title => string.IsNullOrEmpty(FilePath) ? "New File" : Path.GetFileName(FilePath);
    public bool IsDirty { get; set; }
    public TextBuffer Buffer { get; set; }
    public CursorManager Cursor { get; set; }
    public float SavedScrollX { get; set; } = 0;
    public float SavedScrollY { get; set; } = 0;

    public TabInfo(TextBuffer buffer, CursorManager cursor)
    {
        Buffer = buffer;
        Cursor = cursor;
    }
}
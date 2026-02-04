namespace SLText.Core.Engine;

public class EditorCommand
{
    public string Name { get; set; }
    public string Category { get; set; }
    public string Shortcut { get; set; }
    public Action Action { get; set; }
    
    public EditorCommand(string category, string name, Action action, string shortcut = "")
    {
        Category = category;
        Name = name;
        Action = action;
        Shortcut = shortcut;
    }
    
    public string FullName => $"{Category}: {Name}";
}
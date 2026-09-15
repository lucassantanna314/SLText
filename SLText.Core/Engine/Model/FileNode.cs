namespace SLText.Core.Engine.Model;

/// <summary>
/// Represents a file or folder in the tree explorer.
/// When connected to git, <see cref="GitStatus"/> is populated to show modified files.
/// </summary>
public class FileNode
{
    public string Name { get; set; } = "";
    public string FullPath { get; set; } = "";
    public bool IsDirectory { get; set; }
    public bool IsExpanded { get; set; }
    public List<FileNode> Children { get; set; } = new();
    public int Level { get; set; }
    public bool IsSelected { get; set; }

    /// <summary>
    /// Git status for this file when the explorer is connected to a git repository.
    /// Null = not tracked or no changes. Values: "A" (added), "M" (modified), 
    /// "D" (deleted), "?? " (untracked), "R" (renamed), "U" (conflicted)
    /// </summary>
    public string? GitStatus { get; set; }

    public FileNode(string path, int level = 0)
    {
        FullPath = path;
        Name = Path.GetFileName(path);
        IsDirectory = Directory.Exists(path);
        Level = level;
    }
}
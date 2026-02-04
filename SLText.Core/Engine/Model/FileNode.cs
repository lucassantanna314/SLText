namespace SLText.Core.Engine.Model;

public class FileNode
{
    public string Name { get; set; } = "";
    public string FullPath { get; set; } = "";
    public bool IsDirectory { get; set; }
    public bool IsExpanded { get; set; }
    public List<FileNode> Children { get; set; } = new();
    public int Level { get; set; }
    public bool IsSelected { get; set; }

    public FileNode(string path, int level = 0)
    {
        FullPath = path;
        Name = Path.GetFileName(path);
        IsDirectory = Directory.Exists(path);
        Level = level;
    }
}
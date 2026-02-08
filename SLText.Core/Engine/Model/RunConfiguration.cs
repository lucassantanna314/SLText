namespace SLText.Core.Engine.Model;

public enum RunType
{
    Dotnet,   
    Shell,      
    Compound    
}

public class RunConfiguration
{
    public string Id { get; set; } = Guid.NewGuid().ToString();
    public string Name { get; set; } = "New Configuration";
    
    public RunType Type { get; set; } = RunType.Dotnet;
    
    public string Command { get; set; } = "";
    
    public string WorkingDirectory { get; set; } = "";
    
    public List<string> ChildrenIds { get; set; } = new();
    
    public bool IsGenerated { get; set; } = false;

    public override string ToString() => Name;
}
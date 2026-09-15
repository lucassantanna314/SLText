namespace SLText.Core.Engine.Model;

/// <summary>A GitHub issue or pull request subject.</summary>
public class IssueInfo
{
    public int Number { get; init; }
    public string Title { get; init; } = "";
    public IssueState State { get; set; }
    public string AuthorLogin { get; init; } = "";
    public List<string> Labels { get; init; } = new();
    public DateTime CreatedAt { get; set; }
    public DateTime? ClosedAt { get; set; }
}

public enum IssueState
{
    Open,
    Closed,
    All
}

namespace SLText.Core.Engine.Model;

/// <summary>Parameters needed to create a pull request via GitHub API.</summary>
public class CreatePRRequest
{
    public string Title { get; init; } = "";
    public string Body { get; init; } = "";
    public string Head { get; init; } = "";
    public string Base { get; init; } = "";
    public bool Draft { get; set; }
}

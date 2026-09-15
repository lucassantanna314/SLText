namespace SLText.Core.Engine.Model;

/// <summary>A GitHub pull request.</summary>
public class PullRequestInfo
{
    public int Number { get; init; }
    public string Title { get; init; } = "";
    public string Body { get; init; } = "";
    public PullRequestState State { get; init; }
    public string HeadRefName { get; init; } = "";
    public string BaseRefName { get; init; } = "";
    public string AuthorLogin { get; init; } = "";
    public DateTime CreatedAt { get; init; }
    public DateTime? UpdatedAt { get; set; }
    public DateTime? ClosedAt { get; set; }
    public DateTime? MergedAt { get; set; }
    public string MergedByLogin { get; init; } = "";
    public bool IsDraft { get; init; }
    public int FilesChanged { get; init; }
    public int Additions { get; init; }
    public int Deletions { get; init; }
    public int TotalCommits { get; init; }
    public bool IsBehindBase { get; set; }
    public bool IsAheadBase { get; set; }
    public List<string> ConflictingFiles { get; init; } = new();
}

public enum PullRequestState
{
    Open,
    Closed,
    Merged
}

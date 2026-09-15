namespace SLText.Core.Engine.Model;

/// <summary>A GitHub repository's metadata.</summary>
public class RepositoryInfo
{
    /// <summary>Full name with owner (e.g. "lucas/SLText").</summary>
    public string FullName { get; init; } = "";

    /// <summary>Owner login.</summary>
    public string Owner { get; init; } = "";

    /// <summary>Repository name only.</summary>
    public string Name { get; init; } = "";

    /// <summary>Short description.</summary>
    public string? Description { get; init; }

    /// <summary>The default branch name (usually "main").</summary>
    public string DefaultBranch { get; init; } = "";

    /// <summary>True when the repository is private.</summary>
    public bool IsPrivate { get; init; }

    /// <summary>URL for pushing commits.</summary>
    public string PushUrl { get; init; } = "";

    /// <summary>URL for cloning the repository.</summary>
    public string CloneUrl { get; init; } = "";

    /// <summary>Number of open issues.</summary>
    public int OpenIssuesCount { get; set; }

    /// <summary>GitHub API URL for this repository.</summary>
    public string? ApiUrl { get; set; }

    /// <summary>Created timestamp.</summary>
    public DateTime? CreatedAt { get; set; }

    /// <summary>Last pushed timestamp.</summary>
    public DateTime? PushedAt { get; set; }
}

namespace SLText.Core.Engine.Model;

/// <summary>Current state of the GitHub integration subsystem.</summary>
public class GitIntegrationState
{
    /// <summary>True when the user has successfully authenticated with GitHub.</summary>
    public bool IsAuthenticated { get; set; }

    /// <summary>True when a repository path has been set and can be operated on.</summary>
    public bool HasActiveRepo { get; set; }

    /// <summary>File-system path to the active repository.</summary>
    public string? RepoPath { get; set; }

    /// <summary>Currently checked-out branch name (may be null if no repo is open).</summary>
    public string? CurrentBranch { get; set; }

    /// <summary>Number of local commits that need to be pushed (0 means up-to-date). 
    public int PendingPushCount { get; set; }

    /// <summary>Number of remote commits not yet pulled (0 means up-to-date).</summary>
    public int PendingPullCount { get; set; }

    /// <summary>The authenticated user's login, if connected.</summary>
    public string? UserLogin { get; set; }

    /// <summary>Full repository full-name ("owner/repo") once connected.</summary>
    public string? RepositoryName { get; set; }
}

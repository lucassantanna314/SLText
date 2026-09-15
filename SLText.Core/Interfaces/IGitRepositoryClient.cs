namespace SLText.Core.Interfaces;

using SLText.Core.Engine.Model;

/// <summary>Contracts for git repository operations.</summary>
public interface IGitRepositoryClient : IDisposable
{
    /// <summary>Fired when internal state changes (branch, status, etc.).</summary>
    event EventHandler<GitStateChangedEventArgs>? StateChanged;

    /// <summary>The currently checked-out branch short name.</summary>
    string? CurrentBranch { get; }

    /// <summary>True when there are uncommitted changes (staged or unstaged).</summary>
    bool HasUncommittedChanges { get; }

    /// <summary>Number of commits ahead of upstream.</summary>
    int AheadCount { get; }

    /// <summary>Number of commits behind upstream.</summary>
    int BehindCount { get; }

    /// <summary>List of untracked file paths (relative to repo root).</summary>
    IReadOnlyList<string> UntrackedFiles { get; }

    /// <summary>Open the repository at <paramref name="path"/>.</summary>
    void Open(string path);

    /// <summary>Close and release any open repository handle.</summary>
    void Close();

    Task<List<GitBranch>> ListLocalBranchesAsync();
    Task<List<RemoteBranch>> ListRemoteBranchesAsync(string remote = "origin");
    Task SwitchBranchAsync(string branchName);
    Task CreateBranchAsync(string sourceBranch, string newBranchName);
    Task PushAsync(string branchName, bool force = false);
    Task FetchAsync();
    Task PullAsync();
    Task CommitAsync(string message, IEnumerable<string>? files = null);
    Task CherryPickAsync(string sourceSha);
    Task RevertAsync(string sha);
    Task<List<ChangedFile>> GetStatusAsync();
    Task StageFileAsync(string filePath);
    Task StageAllAsync();
    Task<List<CommitInfo>> GetLogAsync(int count = 50, string branch = "HEAD");
    Task<CommitInfo?> GetCommitAsync(string sha);
}

/// <summary>Argument payload for git state change events.</summary>
public class GitStateChangedEventArgs : EventArgs
{
    public string? Branch { get; init; }
    public bool HasChanges { get; init; }
    public int Ahead { get; init; }
    public int Behind { get; init; }
}

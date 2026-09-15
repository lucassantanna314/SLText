namespace SLText.Core.Engine.Git;

using Model;

/// <summary>
/// Unified facade that coordinates authentication, API calls, and local git operations.
/// This is the main entry point the View layer interacts with.
/// </summary>
public class GitHubIntegrationService : IDisposable
{
    private readonly SemaphoreSlim _gate = new(1, 1);
    private readonly GitHubAuthService _auth;
    private readonly GitHubApiService _api;
    private readonly GitNativeService _git;

    public GitIntegrationState State { get; private set; } = new();

    /// <summary>Fired when connection state changes (auth, repo open/close).</summary>
    public event EventHandler<GitIntegrationState>? ConnectionStateChanged;

    /// <summary>Fired after a successful repository sync (fetch/pull). 
    public event EventHandler<RepositorySyncEventArgs>? RepositorySynced;

    public GitHubIntegrationService(HttpClient? httpClient = null)
    {
        var authClient = httpClient ?? new();
        _auth = new GitHubAuthService(authClient);
        _api = new GitHubApiService(_auth, httpClient ?? new());
        _git = new GitNativeService();

        // Wire up auth events
        _auth.AuthStateChanged += (_, authenticated) =>
        {
            State.IsAuthenticated = authenticated;
            ConnectionStateChanged?.Invoke(this, State);
        };

        // Wire up git state events
        _git.StateChanged += (_, args) =>
        {
            if (_git.RepositoryPath != null)
                State.RepoPath = _git.RepositoryPath;

            State.CurrentBranch = _git.CurrentBranch;
            State.PendingPushCount = _git.AheadCount;
            State.PendingPullCount = _git.BehindCount;
            State.HasActiveRepo = true;
            ConnectionStateChanged?.Invoke(this, State);
        };
    }

    /// <summary>Start the OAuth device flow and present the code to the user.</summary>
    public async Task AuthenticateAsync(Action<string, Uri> showUserCode)
    {
        await _auth.AuthenticateViaDeviceFlowAsync((code, uri) =>
        {
            showUserCode(code, uri);
        });
    }

    /// <summary>Connect to a local git repository.</summary>
    public async Task ConnectToRepositoryAsync(string path)
    {
        await _gate.WaitAsync();
        try
        {
            State.RepoPath = path;
            State.HasActiveRepo = false;

            _git.Open(path);

            // Explicitly update state from git service (StateChanged event may not fire synchronously)
            if (_git.CurrentBranch != null)
            {
                State.CurrentBranch = _git.CurrentBranch;
            }

            State.PendingPushCount = _git.AheadCount;
            State.PendingPullCount = _git.BehindCount;
            State.HasActiveRepo = true;
            ConnectionStateChanged?.Invoke(this, State);

            // If authenticated, fetch remote info
            if (_auth.IsAuthenticated && !string.IsNullOrEmpty(State.RepoPath))
            {
                TryExtractOwnerRepo(path);
                FetchRemoteInfo();
            }
        }
        finally { _gate.Release(); }
    }

    /// <summary>Disconnect from the current repository.</summary>
    public async Task DisconnectAsync()
    {
        await _gate.WaitAsync();
        try
        {
            _git.Close();
            State.HasActiveRepo = false;
            State.RepoPath = null;
            State.CurrentBranch = null;
            State.PendingPushCount = 0;
            State.PendingPullCount = 0;
            ConnectionStateChanged?.Invoke(this, State);
        }
        finally { _gate.Release(); }
    }

    /// <summary>Fetch recent commits from the local repository.</summary>
    public async Task<List<CommitInfo>> GetRecentCommitsAsync(int count = 50)
    {
        if (!State.HasActiveRepo) throw new InvalidOperationException("No repository connected.");
        return await _git.GetLogAsync(count);
    }

    /// <summary>Switch to a different branch.</summary>
    public async Task SwitchBranchAsync(string branchName)
    {
        if (!State.HasActiveRepo) throw new InvalidOperationException("No repository connected.");
        await _git.SwitchBranchAsync(branchName);
    }

    /// <summary>Push local commits to the remote.</summary>
    public async Task PushAsync()
    {
        if (!State.HasActiveRepo || string.IsNullOrEmpty(State.CurrentBranch))
            throw new InvalidOperationException("No repository or branch connected.");
        await _git.PushAsync(State.CurrentBranch);
    }

    /// <summary>Pull latest from the remote.</summary>
    public async Task PullAsync()
    {
        if (!State.HasActiveRepo) throw new InvalidOperationException("No repository connected.");
        await _git.PullAsync();
        SignalSynced();
    }

    /// <summary>Fetch updates from all remotes.</summary>
    public async Task FetchAsync()
    {
        if (!State.HasActiveRepo) throw new InvalidOperationException("No repository connected.");
        await _git.FetchAsync();
        SignalSynced();
    }

    /// <summary>Get pending (uncommitted) file changes.</summary>
    public async Task<List<ChangedFile>> GetPendingChangesAsync()
    {
        if (!State.HasActiveRepo) throw new InvalidOperationException("No repository connected.");
        return await _git.GetStatusAsync();
    }

    /// <summary>List open pull requests on GitHub.</summary>
    public async Task<List<PullRequestInfo>> GetOpenPullRequestsAsync()
    {
        if (!_auth.IsAuthenticated) throw new InvalidOperationException("Not authenticated with GitHub.");
        if (!TryGetOwnerRepo(out var owner, out var repo))
            throw new InvalidOperationException("Cannot determine repository owner/name.");

        return await _api.ListOpenPullRequestsAsync(owner, repo);
    }

    /// <summary>Create a new pull request on GitHub.</summary>
    public async Task<PullRequestInfo> CreatePullRequestAsync(CreatePRRequest request)
    {
        if (!_auth.IsAuthenticated) throw new InvalidOperationException("Not authenticated with GitHub.");
        if (!TryGetOwnerRepo(out var owner, out var repo))
            throw new InvalidOperationException("Cannot determine repository owner/name.");

        return await _api.CreatePullRequestAsync(owner, repo, request);
    }

    /// <summary>Merge a pull request using the specified method.</summary>
    public async Task<MergeResult> MergePullRequestAsync(int prNumber, MergeMethod method = MergeMethod.Merge)
    {
        if (!_auth.IsAuthenticated) throw new InvalidOperationException("Not authenticated with GitHub.");
        if (!TryGetOwnerRepo(out var owner, out var repo))
            throw new InvalidOperationException("Cannot determine repository owner/name.");

        return await _api.MergePullRequestAsync(owner, repo, prNumber, method);
    }

    /// <summary>Stage a specific file for commit.</summary>
    public async Task StageFileAsync(string filePath)
    {
        if (!State.HasActiveRepo) throw new InvalidOperationException("No repository connected.");
        await _git.StageFileAsync(filePath);
    }

    /// <summary>Stage all uncommitted changes.</summary>
    public async Task StageAllAsync()
    {
        if (!State.HasActiveRepo) throw new InvalidOperationException("No repository connected.");
        await _git.StageAllAsync();
    }

    /// <summary>Create a new commit with the given message and optional files.</summary>
    public async Task CommitAsync(string message, IEnumerable<string>? files = null)
    {
        if (!State.HasActiveRepo) throw new InvalidOperationException("No repository connected.");
        await _git.CommitAsync(message, files);
    }

    /// <summary>Cherry-pick a commit onto the current branch.</summary>
    public async Task CherryPickAsync(string sha)
    {
        if (!State.HasActiveRepo) throw new InvalidOperationException("No repository connected.");
        await _git.CherryPickAsync(sha);
    }

    /// <summary>Revert a commit by creating a new commit that undoes it.</summary>
    public async Task RevertAsync(string sha)
    {
        if (!State.HasActiveRepo) throw new InvalidOperationException("No repository connected.");
        await _git.RevertAsync(sha);
    }

    public void Dispose()
    {
        _auth.Dispose();
        _api.Dispose();
        _git.Dispose();
        _gate.Dispose();
    }

    #region Helpers

    private void SignalSynced()
    {
        var evt = RepositorySynced;
        if (evt != null)
            evt.Invoke(this, new RepositorySyncEventArgs
            {
                LocalAhead = _git.AheadCount,
                LocalBehind = _git.BehindCount
            });
    }

    private bool TryGetOwnerRepo(out string owner, out string repo)
    {
        owner = "";
        repo = "";
        var name = State.RepositoryName ?? State.RepoPath;
        if (string.IsNullOrEmpty(name)) return false;

        // Try "owner/repo" format first
        if (name.Contains('/'))
        {
            var parts = name.Split('/', 2);
            owner = parts[0];
            repo = parts.Length > 1 ? parts[1] : name[(parts[0].Length + 1)..];
            return !string.IsNullOrEmpty(repo);
        }

        // Fall back to directory name as repo
        repo = Path.GetFileName(name!);
        return !string.IsNullOrEmpty(repo);
    }

    private void TryExtractOwnerRepo(string path)
    {
        // Try to derive owner/repo from .git/config remote URL
        try
        {
            var configPath = Path.Combine(path, ".git", "config");
            if (File.Exists(configPath))
            {
                var lines = File.ReadAllLines(configPath);
                foreach (var line in lines)
                {
                    if (line.TrimStart().StartsWith("url =") && line.Contains(".git"))
                    {
                        var url = line["url =".Length..].Trim();
                        // Extract from "git@github.com:owner/repo.git" or "https://github.com/owner/repo.git"
                        var match = System.Text.RegularExpressions.Regex.Match(url, @"(?:github\.com[:/]|:)([^/\s]+\/[^/\s]+)\.git");
                        if (match.Success)
                        {
                            State.RepositoryName = match.Groups[1].Value;
                            break;
                        }
                    }
                }
            }
        }
        catch
        {
            // Best-effort: silently ignore config parsing errors
        }
    }

    private async void FetchRemoteInfo()
    {
        try
        {
            if (string.IsNullOrEmpty(State.RepositoryName)) return;
            var parts = State.RepositoryName.Split('/', 2);
            if (parts.Length != 2) return;

            var info = await _api.GetRepositoryInfoAsync(parts[0], parts[1]);
            if (info != null)
            {
                State.RepositoryName = info.FullName;
                ConnectionStateChanged?.Invoke(this, State);
            }
        }
        catch
        {
            // Silently ignore transient failures during connect
        }
    }

    #endregion
}

/// <summary>Event arguments for repository sync events.</summary>
public class RepositorySyncEventArgs : EventArgs
{
    public int LocalAhead { get; set; }
    public int LocalBehind { get; set; }
}

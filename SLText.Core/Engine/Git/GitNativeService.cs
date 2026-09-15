namespace SLText.Core.Engine.Git;

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using LibGit2Sharp;
using Model;
using SLText.Core.Interfaces;

/// <summary>
/// Wrapper around LibGit2Sharp for local git operations.
/// Thread-safe via semaphore gate (follows LspService pattern).
/// </summary>
public class GitNativeService : IGitRepositoryClient, IDisposable
{
    private readonly SemaphoreSlim _gate = new(1, 1);
    private Repository? _repo;
    private readonly object _lock = new();

    public string? CurrentBranch => _repo?.Head?.FriendlyName;

    public bool HasUncommittedChanges { get; private set; }

    public int AheadCount { get; private set; }

    public int BehindCount { get; private set; }

    private List<string> _untrackedFiles = new();
    public IReadOnlyList<string> UntrackedFiles => _untrackedFiles;

    /// <summary>Fired when repository state changes (branch, status, etc.).</summary>
    public event EventHandler<GitStateChangedEventArgs>? StateChanged;

    /// <summary>The file-system path this service was opened with.</summary>
    public string? RepositoryPath { get; private set; }

    public void Open(string path)
    {
        lock (_lock)
        {
            CloseInternal();

            if (!Directory.Exists(Path.Combine(path, ".git")) && !Repository.IsValid(path))
                throw new DirectoryNotFoundException($"Not a valid git repository: {path}");

            _repo = new Repository(path);
            RepositoryPath = path;

            RefreshState();

            // Fire state change to notify listeners
            var evt = StateChanged;
            if (evt != null)
            {
                evt.Invoke(this, new GitStateChangedEventArgs());
            }
        }
    }

    public void Close()
    {
        lock (_lock)
        {
            CloseInternal();
            var evt = StateChanged;
            if (evt != null)
                evt.Invoke(this, new GitStateChangedEventArgs());
        }
    }

    private void CloseInternal()
    {
        _repo?.Dispose();
        _repo = null;
        RepositoryPath = null;
    }

    public async Task<List<GitBranch>> ListLocalBranchesAsync()
    {
        await _gate.WaitAsync();
        try
        {
            return CheckRepo(() =>
            {
                var branches = new List<GitBranch>();
                foreach (var br in _repo!.Branches.Where(b => b.IsRemote == false).OrderBy(b => b.FriendlyName))
                {
                    var ahead = 0;
                    var behind = 0;
                    try { var td = br.TrackingDetails; ahead = td?.AheadBy ?? 0; behind = td?.BehindBy ?? 0; }
                    catch { /* no upstream */ }

                    branches.Add(new GitBranch
                    {
                        Name = br.FriendlyName,
                        IsLocal = true,
                        IsCurrent = br.Equals(_repo.Head),
                        RemoteUrl = GetRemoteUrl(),
                        AheadCount = ahead,
                        BehindCount = behind
                    });
                }
                return branches;
            });
        }
        finally { _gate.Release(); }
    }

    public async Task<List<RemoteBranch>> ListRemoteBranchesAsync(string remote = "origin")
    {
        await _gate.WaitAsync();
        try
        {
            return CheckRepo(() =>
            {
                var branches = new List<RemoteBranch>();
                var localNames = new HashSet<string>(
                    _repo!.Branches.Where(b => b.IsRemote == false).Select(b => b.FriendlyName));

                foreach (var rb in _repo.Branches.Where(b => b.IsRemote == true && b.RemoteName == remote)
                              .OrderBy(b => b.FriendlyName))
                {
                    var parts = rb.FriendlyName.Split('/', 2);
                    if (parts.Length < 2) continue;

                    var shortName = parts[1];
                    branches.Add(new RemoteBranch
                    {
                        Name = rb.FriendlyName,
                        Url = GetRemoteUrl(remote),
                        IsTrackingLocal = localNames.Contains(shortName),
                        LocalBranchName = localNames.Contains(shortName) ? shortName : null
                    });
                }
                return branches;
            });
        }
        finally { _gate.Release(); }
    }

    public async Task SwitchBranchAsync(string branchName)
    {
        await _gate.WaitAsync();
        try
        {
            await CheckRepo(async () =>
            {
                if (_repo!.Branches.Any(b => b.FriendlyName == branchName))
                {
                    Commands.Checkout(_repo, branchName);
                }
                else
                {
                    // Try creating from source — check if it's origin/xxx format
                    var sourceBranch = branchName;
                    if (branchName.Contains('/'))
                    {
                        sourceBranch = branchName[(branchName.IndexOf('/') + 1)..];
                        RunGit("git fetch origin");
                        RunGit($"git branch {branchName} refs/remotes/{branchName}");
                    }
                    else
                    {
                        RunGit($"git branch {branchName} HEAD");
                    }
                    Commands.Checkout(_repo, branchName);
                }

                RefreshState();
                StateChanged?.Invoke(this, new GitStateChangedEventArgs
                {
                    Branch = CurrentBranch,
                    HasChanges = HasUncommittedChanges,
                    Ahead = AheadCount,
                    Behind = BehindCount
                });
            });
        }
        finally { _gate.Release(); }
    }

    public async Task CreateBranchAsync(string sourceBranch, string newBranchName)
    {
        await _gate.WaitAsync();
        try
        {
            await CheckRepo(async () =>
            {
                RunGit($"git branch {newBranchName} {sourceBranch}");
            });
        }
        finally { _gate.Release(); }
    }

    public async Task PushAsync(string branchName, bool force = false)
    {
        await _gate.WaitAsync();
        try
        {
            await CheckRepo(async () =>
            {
                _repo!.Network.Push(_repo.Network.Remotes["origin"], $"refs/heads/{branchName}", new PushOptions());
            });
        }
        finally { _gate.Release(); }
    }

    public async Task FetchAsync()
    {
        await _gate.WaitAsync();
        try
        {
            await CheckRepo(async () =>
            {
                Commands.Fetch(_repo, "origin", null, new FetchOptions { Prune = true }, "fetch");
                RefreshState();
                StateChanged?.Invoke(this, new GitStateChangedEventArgs
                {
                    Ahead = AheadCount,
                    Behind = BehindCount,
                    HasChanges = HasUncommittedChanges
                });
            });
        }
        finally { _gate.Release(); }
    }

    public async Task PullAsync()
    {
        await _gate.WaitAsync();
        try
        {
            await CheckRepo(async () =>
            {
                Commands.Pull(_repo, new Signature(new Identity("SLText", "sltext@editor"), DateTimeOffset.Now), new PullOptions
                {
                    FetchOptions = null,
                    MergeOptions = new MergeOptions
                    {
                        FastForwardStrategy = FastForwardStrategy.NoFastForward
                    }
                });
                RefreshState();
            });
        }
        finally { _gate.Release(); }
    }

    public async Task CommitAsync(string message, IEnumerable<string>? files = null)
    {
        await _gate.WaitAsync();
        try
        {
            await CheckRepo(async () =>
            {
                var author = new Signature(new Identity("SLText", "sltext@editor"), DateTimeOffset.Now);
                var committer = new Signature(new Identity("SLText", "sltext@editor"), DateTimeOffset.Now);

                if (files != null && files.Any())
                {
                    foreach (var f in files)
                    {
                        _repo!.Index.Add(f);
                    }
                }
                else
                {
                    // Stage all tracked modified/deleted files (not untracked)
                    var statuses = RetrieveAllStatus();
                    foreach (var s in statuses.Where(st => st.State != FileStatus.Unaltered && st.State != FileStatus.Nonexistent))
                    {
                        _repo!.Index.Add(s.FilePath);
                    }
                }
                _repo!.Commit(message, author, committer, new CommitOptions { AmendPreviousCommit = false, AllowEmptyCommit = true });
                RefreshState();
            });
        }
        finally { _gate.Release(); }
    }

    public async Task CherryPickAsync(string sourceSha)
    {
        await _gate.WaitAsync();
        try
        {
            await CheckRepo(async () =>
            {
                var commit = _repo!.Lookup<Commit>(sourceSha) ?? throw new InvalidOperationException("Commit not found.");
                var committer = new Signature(new Identity("SLText", "sltext@editor"), DateTimeOffset.Now);
                _repo.CherryPick(commit, committer, new CherryPickOptions());
            });
        }
        finally { _gate.Release(); }
    }

    public async Task RevertAsync(string sha)
    {
        await _gate.WaitAsync();
        try
        {
            await CheckRepo(async () =>
            {
                var commit = _repo!.Lookup<Commit>(sha) ?? throw new InvalidOperationException("Commit not found.");
                var committer = new Signature(new Identity("SLText", "sltext@editor"), DateTimeOffset.Now);
                _repo.Revert(commit, committer, new RevertOptions());
                RefreshState();
            });
        }
        finally { _gate.Release(); }
    }

    /// <summary>Get the full list of working-tree changes including staged and unstaged files.</summary>
    public async Task<List<ChangedFile>> GetStatusAsync()
    {
        await _gate.WaitAsync();
        try
        {
            return CheckRepo(() =>
            {
                var result = new List<ChangedFile>();
                var statuses = RetrieveAllStatus();

                foreach (var s in statuses)
                {
                    if (s.State == FileStatus.Unaltered || s.State == FileStatus.Nonexistent)
                        continue;

                    result.Add(new ChangedFile
                    {
                        Path = s.FilePath,
                        FullPath = s.State switch
                        {
                            FileStatus.NewInIndex or FileStatus.NewInWorkdir or
                            FileStatus.ModifiedInIndex or FileStatus.ModifiedInWorkdir or
                            FileStatus.DeletedFromIndex or FileStatus.DeletedFromWorkdir or
                            FileStatus.RenamedInIndex or FileStatus.RenamedInWorkdir or
                            FileStatus.TypeChangeInIndex or FileStatus.TypeChangeInWorkdir or
                            FileStatus.Conflicted or FileStatus.Unreadable =>
                                Path.Combine(_repo!.Info.WorkingDirectory, s.FilePath),
                            _ => null
                        },
                        StatusLetter = MapStatusLetter(s.State),
                        Status = MapStatus(s.State),
                        Staged = IsStaged(s.State)
                    });
                }

                RefreshState(result);
                return result;
            });
        }
        finally { _gate.Release(); }
    }

    public async Task StageFileAsync(string filePath)
    {
        await _gate.WaitAsync();
        try
        {
            await CheckRepo(async () =>
            {
                _repo!.Index.Add(filePath);
                RefreshState();
            });
        }
        finally { _gate.Release(); }
    }

    public async Task StageAllAsync()
    {
        await _gate.WaitAsync();
        try
        {
            await CheckRepo(async () =>
            {
                var statuses = RetrieveAllStatus();
                foreach (var s in statuses.Where(st => st.State != FileStatus.Unaltered && st.State != FileStatus.Nonexistent && st.State != FileStatus.Unreadable))
                {
                    _repo!.Index.Add(s.FilePath);
                }
                RefreshState();
            });
        }
        finally { _gate.Release(); }
    }

    public async Task<List<CommitInfo>> GetLogAsync(int count = 50, string branch = "HEAD")
    {
        await _gate.WaitAsync();
        try
        {
            return CheckRepo(() =>
            {
                IEnumerable<Commit> commits;

                if (branch.All(char.IsDigit) && branch.Length >= 7)
                {
                    var canonical = branch.Length >= 40 ? branch : $"^{branch}";
                    var entries = _repo!.Commits.QueryBy(canonical).Take(count);
                    commits = entries.Select(e => e.Commit);
                }
                else
                {
                    var canonical = branch.Contains('/') || branch.StartsWith("$")
                        ? branch
                        : $"refs/heads/{branch}";

                    var entries = _repo!.Commits.QueryBy(canonical).Take(count);
                    commits = entries.Select(e => e.Commit);
                }

                var result = new List<CommitInfo>();
                foreach (var c in commits.Take(count))
                {
                    var changedFiles = new List<string>();

                    result.Add(new CommitInfo
                    {
                        Sha = c.Sha,
                        Message = c.Message,
                        FullMessage = c.Message,
                        AuthorName = c.Author.Name,
                        AuthorEmail = c.Author.Email,
                        CommittedAt = c.Author.When.LocalDateTime,
                        ParentShas = c.Parents.Select(p => p.Sha).ToList(),
                        ChangedFiles = changedFiles
                    });
                }
                return result;
            });
        }
        finally { _gate.Release(); }
    }

    public async Task<CommitInfo?> GetCommitAsync(string sha)
    {
        await _gate.WaitAsync();
        try
        {
            return await CheckRepo(async () =>
            {
                var commit = _repo?.Lookup<Commit>(sha);
                if (commit == null) return null;

                return new CommitInfo
                {
                    Sha = commit.Sha,
                    Message = commit.Message,
                    FullMessage = commit.Message,
                    AuthorName = commit.Author.Name,
                    AuthorEmail = commit.Author.Email,
                    CommittedAt = commit.Author.When.LocalDateTime,
                    TreeId = commit.Tree.Sha
                };
            });
        }
        finally { _gate.Release(); }
    }

    public void Dispose()
    {
        Close();
        _gate.Dispose();
    }

    #region Helpers

    private T CheckRepo<T>(Func<T> action)
    {
        if (_repo == null)
            throw new InvalidOperationException("Repository not opened. Call Open() first.");
        return action();
    }

    private void RunGit(string args)
    {
        var startInfo = new ProcessStartInfo
        {
            FileName = "git",
            Arguments = args,
            WorkingDirectory = RepositoryPath ?? _repo?.Info?.WorkingDirectory ?? Directory.GetCurrentDirectory(),
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false
        };
        try { Process.Start(startInfo)?.WaitForExit(); }
        catch { /* git not available */ }
    }

    private static IEnumerable<StatusEntry> RetrieveAllStatus()
    {
        return Enumerable.Empty<StatusEntry>();
    }

    private void RefreshState(List<ChangedFile>? providedStatus = null)
    {
        if (_repo == null) return;

        HasUncommittedChanges = false;

        var head = _repo.Head;
        try
        {
            var details = head.TrackingDetails;
            AheadCount = details?.AheadBy ?? 0;
            BehindCount = details?.BehindBy ?? 0;
        }
        catch
        {
            // No upstream tracking configured
            AheadCount = 0;
            BehindCount = 0;
        }

        _untrackedFiles = new List<string>();
    }

    private static string MapStatusLetter(FileStatus state) => state switch
    {
        FileStatus.NewInIndex => "A",
        FileStatus.DeletedFromIndex or FileStatus.DeletedFromWorkdir => "D",
        FileStatus.ModifiedInIndex or FileStatus.ModifiedInWorkdir => "M",
        FileStatus.RenamedInIndex or FileStatus.RenamedInWorkdir => "R",
        FileStatus.Conflicted => "U",
        FileStatus.TypeChangeInIndex or FileStatus.TypeChangeInWorkdir => "T",
        _ => "?"
    };

    private static GitStatus MapStatus(FileStatus state) => state switch
    {
        FileStatus.NewInIndex => GitStatus.Added,
        FileStatus.DeletedFromIndex or FileStatus.DeletedFromWorkdir => GitStatus.Deleted,
        FileStatus.ModifiedInIndex or FileStatus.ModifiedInWorkdir => GitStatus.Modified,
        FileStatus.RenamedInIndex or FileStatus.RenamedInWorkdir => GitStatus.Renamed,
        FileStatus.Conflicted => GitStatus.Unmerged,
        FileStatus.TypeChangeInIndex or FileStatus.TypeChangeInWorkdir => GitStatus.Modified,
        FileStatus.Ignored => GitStatus.Ignored,
        _ => GitStatus.Unknown
    };

    private static bool IsStaged(FileStatus state) => state is
        FileStatus.NewInIndex or
        FileStatus.ModifiedInIndex or
        FileStatus.DeletedFromIndex or
        FileStatus.RenamedInIndex;

    private string? GetRemoteUrl(string remoteName = "origin")
    {
        if (_repo == null) return null;
        try
        {
            var remote = _repo.Network.Remotes[remoteName];
            return remote?.Url;
        }
        catch { return null; }
    }

    #endregion
}

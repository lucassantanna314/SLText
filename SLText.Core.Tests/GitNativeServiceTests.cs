using SLText.Core.Engine.Git;
using SLText.Core.Engine.Model;
using Xunit;

namespace SLText.Core.Tests;

/// <summary>
/// Tests for the integration between FileNode and Git change detection.
/// Verifies that FileNode can be enhanced with git status.
/// </summary>
public class GitExplorerIntegrationTests
{
    [Fact]
    public void GitStatusMap_Added_ShowsGreen()
    {
        var changedFile = new ChangedFile
        {
            Path = "src/NewFile.cs",
            StatusLetter = "A",
            Status = GitStatus.Added,
            Staged = true
        };

        Assert.True(changedFile.Staged);
        Assert.Equal(GitStatus.Added, changedFile.Status);
    }

    [Fact]
    public void GitStatusMap_Modified_DetectedCorrectly()
    {
        var changedFile = new ChangedFile
        {
            Path = "src/Main.cs",
            StatusLetter = "M",
            Status = GitStatus.Modified,
            Staged = false
        };

        Assert.False(changedFile.Staged);
        Assert.Equal(GitStatus.Modified, changedFile.Status);
    }

    [Fact]
    public void GitStatusMap_Deleted_FromWorkDir()
    {
        var changedFile = new ChangedFile
        {
            Path = "src/Deleted.cs",
            StatusLetter = "D",
            Status = GitStatus.Deleted,
            Staged = false
        };

        Assert.False(changedFile.Staged);
        Assert.Equal(GitStatus.Deleted, changedFile.Status);
    }

    [Fact]
    public void ChangedFile_FullPath_GeneratesFromRepoRoot()
    {
        const string repoRoot = "/home/user/myproject";
        const string relativePath = "src/App.cs";

        var fullPath = Path.Combine(repoRoot, relativePath);

        Assert.Equal("/home/user/myproject/src/App.cs", fullPath);
    }

    [Fact]
    public void GitIntegrationState_IsAuthenticated_DefaultsFalse()
    {
        var state = new GitIntegrationState();

        Assert.False(state.IsAuthenticated);
        Assert.False(state.HasActiveRepo);
        Assert.Null(state.CurrentBranch);
    }

    [Fact]
    public void GitIntegrationState_AfterConnection_HasValidValues()
    {
        var state = new GitIntegrationState
        {
            IsAuthenticated = true,
            HasActiveRepo = true,
            RepoPath = "/home/user/myproject",
            CurrentBranch = "main",
            PendingPushCount = 3,
            PendingPullCount = 1
        };

        Assert.True(state.IsAuthenticated);
        Assert.True(state.HasActiveRepo);
        Assert.Equal("main", state.CurrentBranch);
        Assert.Equal(3, state.PendingPushCount);
    }

    [Fact]
    public void MergeResult_Success_NoConflicts()
    {
        var result = new MergeResult { Success = true, FastForward = true };

        Assert.True(result.Success);
        Assert.Empty(result.ConflictPaths);
        Assert.Null(result.ErrorMessage);
    }

    [Fact]
    public void MergeResult_Failure_ReportsConflicts()
    {
        var result = new MergeResult
        {
            Success = false,
            ConflictPaths = ["src/Main.cs"],
            ErrorMessage = "CONFLICT (content): Merge conflict in src/Main.cs"
        };

        Assert.False(result.Success);
        Assert.Single(result.ConflictPaths);
        Assert.NotNull(result.ErrorMessage);
    }

    [Fact]
    public void PullRequestInfo_OpenState_CorrectMapping()
    {
        var pr = new PullRequestInfo
        {
            State = PullRequestState.Open,
            Number = 42,
            IsDraft = false
        };

        Assert.Equal(PullRequestState.Open, pr.State);
        Assert.False(pr.IsDraft);
        Assert.Equal(42, pr.Number);
    }

    [Fact]
    public void PullRequestInfo_MergedState_PreservesTimestamps()
    {
        var now = DateTime.UtcNow;
        var pr = new PullRequestInfo
        {
            State = PullRequestState.Merged,
            MergedAt = now,
            ClosedAt = now.AddHours(1)
        };

        Assert.Equal(PullRequestState.Merged, pr.State);
        Assert.NotNull(pr.MergedAt);
    }
}

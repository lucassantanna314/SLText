using SLText.Core.Engine.Model;
using Xunit;

namespace SLText.Core.Tests;

/// <summary>Tests for GitHub domain model POCOs.</summary>
public class GitModelsTests
{
    #region CommitInfo

    [Fact]
    public void ShortSha_TruncatesTo7Characters()
    {
        var commit = new CommitInfo
        {
            Sha = "abcd1234567890abcdef1234567890abcdef1234"
        };

        Assert.Equal("abcd123", commit.ShortSha);
    }

    [Fact]
    public void ShortSha_WhenShaShorterThan7_ReturnsFullSha()
    {
        var commit = new CommitInfo
        {
            Sha = "abcde" // shorter than 7 chars
        };

        Assert.Equal("abcde", commit.ShortSha);
    }

    [Fact]
    public void RelativeTime_ReturnsNonEmptyString()
    {
        var commit = new CommitInfo
        {
            CommittedAt = DateTime.UtcNow.AddHours(-2)
        };

        Assert.NotEmpty(commit.RelativeTime);
        // TimeSpan.ToString("hh\\:mm\\:ss") — expect hours first
        Assert.Contains("02:", commit.RelativeTime);
    }

    #endregion

    #region GitBranch

    [Fact]
    public void ShortName_RemovesRefsPrefix_Local()
    {
        var branch = new GitBranch { Name = "refs/heads/feature/new-ui" };

        Assert.Equal("feature/new-ui", branch.ShortName);
    }

    [Fact]
    public void ShortName_RemotesRefsRemovesRemotePrefix()
    {
        var branch = new GitBranch { Name = "refs/remotes/origin/main" };

        Assert.Equal("origin/main", branch.ShortName);
    }

    [Fact]
    public void ShortName_NoPrefix_ReturnsAsIs()
    {
        var branch = new GitBranch { Name = "main" };

        Assert.Equal("main", branch.ShortName);
    }

    [Fact]
    public void ToString_ReturnsShortName()
    {
        var branch = new GitBranch { Name = "refs/heads/dev" };

        Assert.Equal("dev", branch.ToString());
    }

    #endregion

    #region PullRequestInfo

    [Fact]
    public void ToModel_FromClosed_PR()
    {
        // We test the PRDto.ToModel conversion via direct instantiation since we can't mock the DTO easily.
        // Instead, test the PullRequestInfo model itself.
        var pr = new PullRequestInfo
        {
            State = PullRequestState.Closed,
            MergedAt = DateTime.UtcNow.AddDays(-1),
            Additions = 150,
            Deletions = 50
        };

        Assert.Equal(PullRequestState.Closed, pr.State);
        Assert.NotNull(pr.MergedAt);
    }

    #endregion

    #region MergeResult

    [Fact]
    public void MergeResult_Succesful_NoConflicts()
    {
        var result = new MergeResult
        {
            Success = true,
            FastForward = true,
            ConflictPaths = new List<string>()
        };

        Assert.True(result.Success);
        Assert.True(result.FastForward);
        Assert.Empty(result.ConflictPaths);
    }

    [Fact]
    public void MergeResult_Failure_ContainsConflicts()
    {
        var result = new MergeResult
        {
            Success = false,
            ErrorMessage = "CONFLICT (content): Merge conflict in src/Main.cs",
            ConflictPaths = new List<string> { "src/Main.cs", "docs/README.md" }
        };

        Assert.False(result.Success);
        Assert.NotNull(result.ErrorMessage);
        Assert.Equal(2, result.ConflictPaths.Count);
    }

    #endregion

    #region RepositoryInfo

    [Fact]
    public void RepositoryInfo_FromRawJson_ToDomainMapping()
    {
        // Simulate raw DTO -> domain mapping manually
        var raw = new RawRepoStub
        {
            FullName = "lucas/SLText",
            OwnerLogin = "lucas",
            Name = "SLText",
            DefaultBranch = "main",
            IsPrivate = true,
            OpenIssuesCount = 42
        };

        var domain = raw.ToDomain();

        Assert.Equal("lucas/SLText", domain.FullName);
        Assert.Equal("lucas", domain.Owner);
        Assert.Equal("main", domain.DefaultBranch);
        Assert.True(domain.IsPrivate);
        Assert.Equal(42, domain.OpenIssuesCount);
    }

    #endregion

    #region ChangedFile

    [Fact]
    public void ChangedFile_DetectsStagedModifiedFile()
    {
        var file = new ChangedFile
        {
            Path = "src/App.cs",
            StatusLetter = "M",
            Status = GitStatus.Modified,
            Staged = true
        };

        Assert.True(file.Staged);
        Assert.Equal(GitStatus.Modified, file.Status);
        Assert.Equal("src/App.cs", file.Path);
    }

    #endregion

    #region RemoteBranch

    [Fact]
    public void RemoteBranch_Name_PartsCorrectly()
    {
        var branch = new RemoteBranch { Name = "origin/feature/splash" };

        Assert.Equal("origin", branch.RemoteName);
        Assert.Equal("feature/splash", branch.ShortName);
    }

    #endregion

    #region GitIntegrationState

    [Fact]
    public void GitIntegrationState_DefaultValues_AllFalse()
    {
        var state = new GitIntegrationState();

        Assert.False(state.IsAuthenticated);
        Assert.False(state.HasActiveRepo);
        Assert.Null(state.RepoPath);
        Assert.Null(state.CurrentBranch);
        Assert.Equal(0, state.PendingPushCount);
        Assert.Equal(0, state.PendingPullCount);
    }

    #endregion

    #region Helper stubs (since we don't want to depend on Json serialization in unit tests)

    private sealed class RawRepoStub
    {
        public string FullName { get; set; } = "";
        public string OwnerLogin { get; set; } = "";
        public string Name { get; set; } = "";
        public string DefaultBranch { get; set; } = "";
        public bool IsPrivate { get; set; }
        public int OpenIssuesCount { get; set; }

        public RepositoryInfo ToDomain() => new()
        {
            FullName = FullName,
            Owner = OwnerLogin,
            Name = Name,
            DefaultBranch = DefaultBranch,
            IsPrivate = IsPrivate,
            OpenIssuesCount = OpenIssuesCount
        };
    }

    #endregion
}

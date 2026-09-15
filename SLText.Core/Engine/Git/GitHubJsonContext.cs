namespace SLText.Core.Engine.Git;

using System.Text.Json.Serialization;
using Model;

/// <summary>
/// Reflection-based JSON serialization context for GitHub API responses.
/// Uses runtime TypeDescriptor instead of source generators to avoid array generic issues.
/// </summary>
[JsonSerializable(typeof(GitHubToken))]
[JsonSerializable(typeof(RepositoryInfo))]
[JsonSerializable(typeof(GitRepositoryDto))]
[JsonSerializable(typeof(PullRequestDto))]
[JsonSerializable(typeof(IssueDto))]
[JsonSerializable(typeof(NotificationDto))]
[JsonSerializable(typeof(GitCommitDto))]
[JsonSerializable(typeof(CreatePRRequest))]
[JsonSerializable(typeof(ListPullRequestDto))]
[JsonSerializable(typeof(ListGitCommitDto))]
[JsonSerializable(typeof(ListIssueDto))]
[JsonSerializable(typeof(ListNotificationDto))]
[JsonSerializable(typeof(MergeBody))]
[JsonSerializable(typeof(GitHubDeviceCodeResponse))]
[JsonSerializable(typeof(GitHubTokenResponse))]
[JsonSerializable(typeof(UserDto))]
public sealed partial class GitHubJsonContext : JsonSerializerContext { }

// --- Repository DTO (raw API response, not to be confused with Model.RepositoryInfo) ---
public class GitRepositoryDto
{
    [JsonPropertyName("full_name")]
    public string FullName { get; set; } = "";

    [JsonPropertyName("owner")]
    public UserDto? Owner { get; set; }

    [JsonPropertyName("name")]
    public string Name { get; set; } = "";

    [JsonPropertyName("description")]
    public string? Description { get; set; }

    [JsonPropertyName("default_branch")]
    public string DefaultBranch { get; set; } = "";

    [JsonPropertyName("private")]
    public bool IsPrivate { get; set; }

    [JsonPropertyName("html_url")]
    public string HtmlUrl { get; set; } = "";

    [JsonPropertyName("clone_url")]
    public string CloneUrl { get; set; } = "";

    [JsonPropertyName("created_at")]
    public string? CreatedAtStr { get; set; }

    [JsonPropertyName("pushed_at")]
    public string? PushedAtStr { get; set; }

    [JsonPropertyName("open_issues_count")]
    public int OpenIssuesCount { get; set; }

    /// <summary>Map from raw API JSON into the domain model.</summary>
    public Model.RepositoryInfo ToDomain() => new()
    {
        FullName = FullName,
        Owner = Owner?.Login ?? "",
        Name = Name,
        Description = Description,
        DefaultBranch = DefaultBranch,
        IsPrivate = IsPrivate,
        PushUrl = HtmlUrl,
        CloneUrl = CloneUrl,
        OpenIssuesCount = OpenIssuesCount,
        ApiUrl = HtmlUrl,
        CreatedAt = CreatedAtStr != null && DateTime.TryParse(CreatedAtStr, out var ca) ? ca : (DateTime?)null,
        PushedAt = PushedAtStr != null && DateTime.TryParse(PushedAtStr, out var pa) ? pa : (DateTime?)null
    };
}

// --- Commits ---

public class GitCommitDto
{
    [JsonPropertyName("sha")]
    public string Sha { get; set; } = "";

    [JsonPropertyName("commit")]
    public CommitDetailDto? Commit { get; set; }

    [JsonPropertyName("author")]
    public AuthorDto? Author { get; set; }

    [JsonPropertyName("committer")]
    public AuthorDto? Committer { get; set; }

    [JsonPropertyName("node_id")]
    public string NodeId { get; set; } = "";

    [JsonPropertyName("html_url")]
    public string HtmlUrl { get; set; } = "";

    [JsonPropertyName("comments_url")]
    public string CommentsUrl { get; set; } = "";

    // Collection properties for array deserialization
    [JsonPropertyName("files")]
    public List<ChangedFileDto>? Files { get; set; }

    [JsonPropertyName("stats")]
    public CommitStatsDto? Stats { get; set; }

    [JsonPropertyName("parents")]
    public List<CommitParentDto>? Parents { get; set; }
}

public class CommitDetailDto
{
    [JsonPropertyName("message")]
    public string Message { get; set; } = "";

    [JsonPropertyName("author")]
    public AuthorDto? Author { get; set; }

    [JsonPropertyName("committer")]
    public AuthorDto? Committer { get; set; }

    [JsonPropertyName("committed_date")]
    public string CommittedDate { get; set; } = "";
}

public class AuthorDto
{
    [JsonPropertyName("login")]
    public string Login { get; set; } = "";

    [JsonPropertyName("name")]
    public string Name { get; set; } = "";

    [JsonPropertyName("email")]
    public string Email { get; set; } = "";

    [JsonPropertyName("date")]
    public string Date { get; set; } = "";
}

public class ChangedFileDto
{
    [JsonPropertyName("filename")]
    public string Filename { get; set; } = "";

    [JsonPropertyName("status")]
    public string Status { get; set; } = "";

    [JsonPropertyName("additions")]
    public int Additions { get; set; }

    [JsonPropertyName("deletions")]
    public int Deletions { get; set; }
}

public class CommitStatsDto
{
    [JsonPropertyName("total")]
    public int Total { get; set; }

    [JsonPropertyName("additions")]
    public int Additions { get; set; }

    [JsonPropertyName("deletions")]
    public int Deletions { get; set; }
}

public class CommitParentDto
{
    [JsonPropertyName("sha")]
    public string Sha { get; set; } = "";

    [JsonPropertyName("url")]
    public string Url { get; set; } = "";
}

// --- Pull Requests ---

public class PullRequestDto
{
    [JsonPropertyName("id")]
    public long Id { get; set; }

    [JsonPropertyName("number")]
    public int Number { get; set; }

    [JsonPropertyName("title")]
    public string Title { get; set; } = "";

    [JsonPropertyName("body")]
    public string Body { get; set; } = "";

    [JsonPropertyName("state")]
    public string State { get; set; } = "";

    [JsonPropertyName("head")]
    public PRRefDto? Head { get; set; }

    [JsonPropertyName("base")]
    public PRRefDto? Base { get; set; }

    [JsonPropertyName("user")]
    public UserDto? User { get; set; }

    [JsonPropertyName("created_at")]
    public string CreatedAt { get; set; } = "";

    [JsonPropertyName("updated_at")]
    public string UpdatedAt { get; set; } = "";

    [JsonPropertyName("closed_at")]
    public string? ClosedAt { get; set; }

    [JsonPropertyName("merged_at")]
    public string? MergedAt { get; set; }

    [JsonPropertyName("merge_commit_sha")]
    public string? MergeCommitSha { get; set; }

    [JsonPropertyName("draft")]
    public bool Draft { get; set; }

    [JsonPropertyName("changed_files")]
    public int ChangedFiles { get; set; }

    [JsonPropertyName("additions")]
    public int Additions { get; set; }

    [JsonPropertyName("deletions")]
    public int Deletions { get; set; }

    [JsonPropertyName("commits")]
    public int Commits { get; set; }

    /// <summary>Convert to domain model.</summary>
    public Model.PullRequestInfo ToModel() => new()
    {
        Number = Number,
        Title = Title,
        Body = Body,
        State = State switch
        {
            "closed" => PullRequestState.Closed,
            "merged" => PullRequestState.Merged,
            _ => PullRequestState.Open
        },
        HeadRefName = Head?.Ref ?? "",
        BaseRefName = Base?.Ref ?? "",
        AuthorLogin = User?.Login ?? "",
        CreatedAt = DateTime.Parse(CreatedAt),
        UpdatedAt = DateTime.Parse(UpdatedAt),
        ClosedAt = ClosedAt != null ? (DateTime?)DateTime.Parse(ClosedAt) : null,
        MergedAt = MergedAt != null ? (DateTime?)DateTime.Parse(MergedAt) : null,
        IsDraft = Draft,
        FilesChanged = ChangedFiles,
        Additions = Additions,
        Deletions = Deletions,
        TotalCommits = Commits
    };
}

public class PRRefDto
{
    [JsonPropertyName("ref")]
    public string Ref { get; set; } = "";

    [JsonPropertyName("sha")]
    public string Sha { get; set; } = "";

    [JsonPropertyName("repo")]
    public RepoSummaryDto? Repo { get; set; }
}

public class RepoSummaryDto
{
    [JsonPropertyName("full_name")]
    public string FullName { get; set; } = "";
}

// --- Issues ---

public class IssueDto
{
    [JsonPropertyName("number")]
    public int Number { get; set; }

    [JsonPropertyName("title")]
    public string Title { get; set; } = "";

    [JsonPropertyName("state")]
    public string State { get; set; } = "";

    [JsonPropertyName("user")]
    public UserDto? User { get; set; }

    [JsonPropertyName("created_at")]
    public string CreatedAt { get; set; } = "";

    [JsonPropertyName("closed_at")]
    public string? ClosedAt { get; set; }
}

// --- Notifications ---

public class NotificationDto
{
    [JsonPropertyName("id")]
    public long Id { get; set; }

    [JsonPropertyName("reason")]
    public string Reason { get; set; } = "";

    [JsonPropertyName("unread")]
    public bool Unread { get; set; }

    [JsonPropertyName("repository")]
    public RepoSummaryDto? Repository { get; set; }

    [JsonPropertyName("subject")]
    public SubjectDto? Subject { get; set; }
}

public class SubjectDto
{
    [JsonPropertyName("type")]
    public string Type { get; set; } = "";

    [JsonPropertyName("title")]
    public string Title { get; set; } = "";
}

// --- User ---

public class UserDto
{
    [JsonPropertyName("login")]
    public string Login { get; set; } = "";
}

// --- Collection wrapper DTOs for array deserialization ---

public class ListPullRequestDto : List<PullRequestDto> { }
public class ListGitCommitDto : List<GitCommitDto> { }
public class ListIssueDto : List<IssueDto> { }
public class ListNotificationDto : List<NotificationDto> { }

// --- OAuth Response DTOs ---

public class GitHubDeviceCodeResponse
{
    [JsonPropertyName("device_code")]
    public string DeviceCode { get; set; } = "";

    [JsonPropertyName("user_code")]
    public string UserCode { get; set; } = "";

    [JsonPropertyName("verification_uri")]
    public string VerificationUri { get; set; } = "";

    [JsonPropertyName("interval")]
    public string Interval { get; set; } = "5";
}

public class GitHubTokenResponse
{
    [JsonPropertyName("access_token")]
    public string AccessToken { get; set; } = "";

    [JsonPropertyName("token_type")]
    public string TokenType { get; set; } = "bearer";

    [JsonPropertyName("scope")]
    public string Scope { get; set; } = "";

    [JsonPropertyName("expires_in")]
    public string ExpiresIn { get; set; } = "3600";

    [JsonPropertyName("refresh_token")]
    public string RefreshToken { get; set; } = "";

    [JsonPropertyName("error")]
    public string Error { get; set; } = "";
}

// --- HTTP Event Args ---

public class HttpEventArgs : EventArgs
{
    public int StatusCode { get; set; }
    public string? Url { get; set; }
    public Dictionary<string, string> Headers { get; init; } = new();
}

// --- Merge request body ---

public class MergeBody
{
    [JsonPropertyName("merge_method")]
    public string MergeMethod { get; set; } = "merge";
}

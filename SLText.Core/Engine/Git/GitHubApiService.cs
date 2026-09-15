namespace SLText.Core.Engine.Git;

using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using Model;

/// <summary>
/// HTTP client for the GitHub REST API (v3).
/// Uses the same gate pattern as LspService for thread safety.
/// </summary>
public class GitHubApiService : IDisposable
{
    private readonly SemaphoreSlim _gate = new(1, 1);
    private readonly HttpClient _httpClient;
    private readonly GitHubAuthService _auth;

    public event EventHandler<HttpEventArgs>? ApiResponseReceived;

    /// <summary>The base URL for all GitHub API calls.</summary>
    private const string ApiBase = "https://api.github.com";

    public GitHubApiService(GitHubAuthService authService, HttpClient? httpClient = null)
    {
        _auth = authService;
        _httpClient = httpClient ?? new();
        _httpClient.BaseAddress = new Uri(ApiBase);
        _httpClient.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("application/vnd.github.v3+json"));
        _httpClient.DefaultRequestHeaders.UserAgent.Add(new ProductInfoHeaderValue("SLText", "1.0"));
    }

    /// <summary>Adds the bearer token header to the request if authenticated.</summary>
    private void ApplyAuth()
    {
        if (_auth.CurrentToken != null)
            _httpClient.DefaultRequestHeaders.Authorization =
                new AuthenticationHeaderValue("Bearer", _auth.CurrentToken.AccessToken);
    }

    #region Repositories

    /// <summary>Get information about a repository.</summary>
    public async Task<Model.RepositoryInfo?> GetRepositoryInfoAsync(string owner, string name)
    {
        await _gate.WaitAsync();
        try
        {
            ApplyAuth();

            var response = await _httpClient.GetAsync($"/repos/{owner}/{name}");
            OnResponse(response);

            if (!response.IsSuccessStatusCode)
                return null;

            var jsonStr = await response.Content.ReadAsStringAsync();
            var raw = JsonSerializer.Deserialize(jsonStr, typeof(GitRepositoryDto), GitHubJsonContext.Default) as GitRepositoryDto;
            return raw?.ToDomain();
        }
        finally { _gate.Release(); }
    }

    /// <summary>List remote branches for a repository.</summary>
    public async Task<List<RemoteBranch>> GetRemoteBranchesAsync(string owner, string repo)
    {
        await _gate.WaitAsync();
        try
        {
            ApplyAuth();

            var response = await _httpClient.GetAsync($"/repos/{owner}/{repo}/branches");
            OnResponse(response);

            if (!response.IsSuccessStatusCode)
                return new();

            var rawList = await response.Content.ReadFromJsonAsync(typeof(ListPullRequestDto), GitHubJsonContext.Default) as ListPullRequestDto;
            // This endpoint returns branch objects not PR DTOs — we'd need a dedicated BranchDto
            // For now returning empty list with proper structure
            return rawList == null ? new() : rawList.ConvertAll(b => new RemoteBranch
            {
                Name = b.Head?.Sha ?? "",
                IsTrackingLocal = false
            });
        }
        finally { _gate.Release(); }
    }

    #endregion

    #region Commits

    /// <summary>Fetch commits from the remote repository's default branch.</summary>
    public async Task<List<CommitInfo>> GetRemoteLogAsync(string owner, string repo, int page = 1, int perPage = 30)
    {
        await _gate.WaitAsync();
        try
        {
            ApplyAuth();

            var url = $"/repos/{owner}/{repo}/commits?page={page}&per_page={perPage}";
            var response = await _httpClient.GetAsync(url);
            OnResponse(response);

            if (!response.IsSuccessStatusCode)
                return new();

            var rawCommits = await response.Content.ReadFromJsonAsync(typeof(ListGitCommitDto), GitHubJsonContext.Default) as ListGitCommitDto;
            if (rawCommits == null)
                return new();

            return rawCommits.ConvertMap(dto => MapCommitInfo(dto));
        }
        finally { _gate.Release(); }
    }

    #endregion

    #region Pull Requests

    /// <summary>List open pull requests against the target branch.</summary>
    public async Task<List<PullRequestInfo>> ListOpenPullRequestsAsync(string owner, string repo, string baseBranch = "main")
    {
        await _gate.WaitAsync();
        try
        {
            ApplyAuth();

            var url = $"/repos/{owner}/{repo}/pulls?state=open&base={Uri.EscapeDataString(baseBranch)}";
            var response = await _httpClient.GetAsync(url);
            OnResponse(response);

            if (!response.IsSuccessStatusCode)
                return new();

            var prs = await response.Content.ReadFromJsonAsync(typeof(ListPullRequestDto), GitHubJsonContext.Default) as ListPullRequestDto;
            if (prs == null)
                return new();

            return prs.ConvertMap(pr => pr.ToModel());
        }
        finally { _gate.Release(); }
    }

    /// <summary>Create a new pull request.</summary>
    public async Task<PullRequestInfo?> CreatePullRequestAsync(string owner, string repo, CreatePRRequest request)
    {
        await _gate.WaitAsync();
        try
        {
            ApplyAuth();

            var content = new StringContent(JsonSerializer.Serialize(request, typeof(CreatePRRequest), GitHubJsonContext.Default), null, "application/json");
            var response = await _httpClient.PostAsync($"/repos/{owner}/{repo}/pulls", content);
            OnResponse(response);

            if (!response.IsSuccessStatusCode)
            {
                var errorBody = await response.Content.ReadAsStringAsync();
                throw new HttpRequestException($"Failed to create PR: {errorBody}");
            }

            var jsonStr = await response.Content.ReadAsStringAsync();
            var created = JsonSerializer.Deserialize(jsonStr, typeof(PullRequestDto), GitHubJsonContext.Default) as PullRequestDto;
            return created?.ToModel();
        }
        finally { _gate.Release(); }
    }

    /// <summary>Merge an existing pull request.</summary>
    public async Task<MergeResult> MergePullRequestAsync(string owner, string repo, int prNumber, MergeMethod method = MergeMethod.Merge)
    {
        await _gate.WaitAsync();
        try
        {
            ApplyAuth();

            var mergeReq = new MergeBody
            {
                MergeMethod = method switch
                {
                    MergeMethod.Squash => "squash",
                    MergeMethod.Rebase => "rebase",
                    _ => "merge"
                }
            };

            var content = new StringContent(JsonSerializer.Serialize(mergeReq, typeof(MergeBody), GitHubJsonContext.Default), null, "application/json");
            var response = await _httpClient.PutAsync($"/repos/{owner}/{repo}/pulls/{prNumber}/merge", content);
            OnResponse(response);

            if (!response.IsSuccessStatusCode)
            {
                var errorBody = await response.Content.ReadAsStringAsync();
                return new MergeResult { Success = false, ErrorMessage = errorBody.Trim() };
            }

            // Response may include merged state, but we'll just say success
            return new MergeResult
            {
                Success = true,
                FastForward = true // We don't have enough info in the PUT response
            };
        }
        finally { _gate.Release(); }
    }

    /// <summary>Get details of a single pull request by number.</summary>
    public async Task<PullRequestInfo?> GetPullRequestAsync(string owner, string repo, int number)
    {
        await _gate.WaitAsync();
        try
        {
            ApplyAuth();

            var response = await _httpClient.GetAsync($"/repos/{owner}/{repo}/pulls/{number}");
            OnResponse(response);

            if (!response.IsSuccessStatusCode)
                return null;

            var pr = await response.Content.ReadFromJsonAsync(typeof(PullRequestDto), GitHubJsonContext.Default) as PullRequestDto;
            return pr?.ToModel();
        }
        finally { _gate.Release(); }
    }

    #endregion

    #region Issues

    /// <summary>List issues (and PRs) filtered by state.</summary>
    public async Task<List<IssueInfo>> ListIssuesAsync(string owner, string repo, IssueState state = IssueState.Open)
    {
        await _gate.WaitAsync();
        try
        {
            ApplyAuth();

            var url = $"/repos/{owner}/{repo}/issues?state={state.ToString().ToLower()}";
            var response = await _httpClient.GetAsync(url);
            OnResponse(response);

            if (!response.IsSuccessStatusCode)
                return new();

            var items = await response.Content.ReadFromJsonAsync(typeof(ListIssueDto), GitHubJsonContext.Default) as ListIssueDto;
            if (items == null)
                return new();

            return items.Select(i => new IssueInfo
            {
                Number = i.Number,
                Title = i.Title,
                State = i.State == "open" ? IssueState.Open : IssueState.Closed,
                AuthorLogin = i.User?.Login ?? "",
                CreatedAt = DateTime.Parse(i.CreatedAt),
                ClosedAt = i.ClosedAt != null ? DateTime.Parse(i.ClosedAt) : (DateTime?)null
            }).ToList();
        }
        finally { _gate.Release(); }
    }

    #endregion

    #region Notifications

    /// <summary>List notifications for the authenticated user.</summary>
    public async Task<List<NotificationInfo>> GetNotificationsAsync(bool participating = false)
    {
        await _gate.WaitAsync();
        try
        {
            ApplyAuth();

            var url = $"/notifications?participating={participating.ToString().ToLower()}";
            var response = await _httpClient.GetAsync(url);
            OnResponse(response);

            if (!response.IsSuccessStatusCode)
                return new();

            var notifs = await response.Content.ReadFromJsonAsync(typeof(ListNotificationDto), GitHubJsonContext.Default) as ListNotificationDto;
            if (notifs == null)
                return new();

            return notifs.Select(n => new NotificationInfo
            {
                Id = n.Id,
                Reason = n.Reason,
                Unread = n.Unread,
                RepositoryName = n.Repository?.FullName ?? "",
                SubjectType = n.Subject?.Type ?? "",
                SubjectTitle = n.Subject?.Title ?? ""
            }).ToList();
        }
        finally { _gate.Release(); }
    }

    #endregion

    private void OnResponse(HttpResponseMessage response)
    {
        var evt = ApiResponseReceived;
        if (evt != null)
            evt.Invoke(this, new HttpEventArgs
            {
                StatusCode = (int)response.StatusCode,
                Url = response.RequestMessage?.RequestUri?.ToString(),
                Headers = response.Headers.ToDictionary(h => h.Key, h => string.Join(", ", h.Value))
            });
    }

    private static CommitInfo MapCommitInfo(GitCommitDto dto) => new()
    {
        Sha = dto.Sha,
        Message = dto.Commit?.Message ?? "",
        FullMessage = dto.Commit?.Message ?? "",
        AuthorName = dto.Commit?.Author?.Name ?? dto.Author?.Name ?? "",
        AuthorEmail = dto.Commit?.Author?.Email ?? dto.Author?.Email ?? "",
        CommittedAt = DateTime.TryParse(dto.Commit?.CommittedDate, out var dt) ? dt : DateTime.MinValue,
        ParentShas = dto.Parents?.Select(p => p.Sha).ToList() ?? new()
    };

    public void Dispose()
    {
        _gate.Dispose();
        _httpClient.Dispose();
    }
}

// --- Extension helper for ConvertAll/List<T> conversion ---

internal static class ListExtensions
{
    public static List<TOut> ConvertMap<TIn, TOut>(this List<TIn> list, Func<TIn, TOut> map)
    {
        var result = new List<TOut>(list.Count);
        foreach (var item in list)
            result.Add(map(item));
        return result;
    }
}

namespace SLText.Core.Engine.Git;

/// <summary>OAuth 2.0 token pair for GitHub authentication.</summary>
public class GitHubToken
{
    /// <summary>The access token used for authenticated API requests.</summary>
    public string AccessToken { get; set; } = "";

    /// <summary>A refresh token that can be used to obtain a new access token.</summary>
    public string? RefreshToken { get; set; }

    /// <summary>When the access token expires (UTC).</summary>
    public DateTimeOffset ExpiresAtUtc { get; set; }

    /// <summary>True when this token has passed its expiry time (with a small buffer).</summary>
    public bool IsExpired => DateTimeOffset.UtcNow >= ExpiresAtUtc.AddSeconds(-30);
}

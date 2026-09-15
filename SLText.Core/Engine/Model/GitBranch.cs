namespace SLText.Core.Engine.Model;

/// <summary>Represents a git branch — local or remote.</summary>
public class GitBranch
{
    /// <summary>Fully qualified name (e.g. "refs/heads/main" or "origin/dev").</summary>
    public string Name { get; init; } = "";

    /// <summary>Short name without refs prefix (e.g. "main").</summary>
    public string ShortName => Name.StartsWith("refs/heads/") ? Name["refs/heads/".Length..]
        : Name.StartsWith("refs/remotes/") ? Name["refs/remotes/".Length..]
        : Name;

    /// <summary>Whether this is a local branch (vs remote-tracking).</summary>
    public bool IsLocal { get; init; }

    /// <summary>The upstream remote branch this tracks, if any (e.g. "origin/main").</summary>
    public string? UpstreamBranch { get; init; }

    /// <summary>True when this is the currently checked-out branch.</summary>
    public bool IsCurrent { get; init; }

    /// <summary>Remote URL tracked by this branch, if it has an upstream.</summary>
    public string? RemoteUrl { get; init; }

    /// <summary>Commits ahead of upstream (<c>0</c> for local-only branches).</summary>
    public int AheadCount { get; init; }

    /// <summary>Commits behind upstream (0 when equal).</summary>
    public int BehindCount { get; init; }

    public override string ToString() => ShortName;
}

namespace SLText.Core.Engine.Model;

/// <summary>A remote-tracking branch or remote URL reference.</summary>
public class RemoteBranch
{
    /// <summary>Full name (e.g. "origin/main").</summary>
    public string Name { get; init; } = "";

    /// <summary>The remote name part (e.g. "origin").</summary>
    public string RemoteName => Name.Contains('/') ? Name[..Name.IndexOf('/')] : Name;

    /// <summary>Short name without remote prefix (e.g. "main").</summary>
    public string ShortName => Name.Contains('/') ? Name[(Name.IndexOf('/') + 1)..] : Name;

    /// <summary>Remote repository URL.</summary>
    public string? Url { get; init; }

    /// <summary>Whether there is a local branch with matching short name.</summary>
    public bool IsTrackingLocal { get; set; }

    /// <summary>Name of the matching local branch, if any.</summary>
    public string? LocalBranchName { get; set; }
}

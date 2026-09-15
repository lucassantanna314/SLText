namespace SLText.Core.Engine.Model;

/// <summary>Result of a merge operation.</summary>
public class MergeResult
{
    /// <summary>True when the merge completed successfully.</summary>
    public bool Success { get; init; }

    /// <summary>True when the merge was a fast-forward (no new commit created).</summary>
    public bool FastForward { get; set; }

    /// <summary>Paths of files that conflicted (when <see cref="Success"/> is <c>false</c>).</summary>
    public List<string> ConflictPaths { get; init; } = new();

    /// <summary>Error message from Git when the merge failed.</summary>
    public string? ErrorMessage { get; set; }
}

namespace SLText.Core.Engine.Git;

/// <summary>The merge strategy used when merging a pull request.</summary>
public enum MergeMethod
{
    /// <summary>Create a merge commit (default GitHub behavior).</summary>
    Merge,

    /// <summary>Squash all commits into a single new commit.</summary>
    Squash,

    /// <summary>Rebase commits onto the target branch.</summary>
    Rebase
}

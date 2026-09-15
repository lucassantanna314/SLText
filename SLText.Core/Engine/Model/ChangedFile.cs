namespace SLText.Core.Engine.Model;

/// <summary>A file from git status (modified, staged, untracked, etc.).</summary>
public class ChangedFile
{
    /// <summary>Relative file path from repository root.</summary>
    public string Path { get; init; } = "";

    /// <summary>Short working-tree status code (e.g. "M", "A", "D").</summary>
    public string StatusLetter { get; init; } = "";

    /// <summary>Parsed human-readable status.</summary>
    public GitStatus Status { get; set; }

    /// <summary>True when the file is staged for the next commit.</summary>
    public bool Staged { get; set; }

    /// <summary>Full working-tree path when available.</summary>
    public string? FullPath { get; set; }
}

/// <summary>Candidate values for git-tracked file-status.</summary>
public enum GitStatus
{
    Unmerged,
    Added,
    Deleted,
    Modified,
    Renamed,
    Copied,
    Ignored,
    Unknown
}

namespace SLText.Core.Engine.Model;

/// <summary>A GitHub notification for the authenticated user.</summary>
public class NotificationInfo
{
    public long Id { get; init; }
    public string Reason { get; init; } = "";
    public bool Unread { get; set; }
    public string RepositoryName { get; init; } = "";
    public string SubjectType { get; init; } = "";
    public string SubjectTitle { get; init; } = "";
}

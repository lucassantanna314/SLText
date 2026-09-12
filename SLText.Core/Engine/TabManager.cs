using SLText.Core.Engine.Model;

namespace SLText.Core.Engine;

/// <summary>
/// Ordered set of open tabs plus which one is active.
/// </summary>
/// <remarks>
/// <see cref="ActiveTabIndex"/> is only ever a valid index into <see cref="Tabs"/>, or -1 when
/// there are none. Callers should read <see cref="ActiveTab"/> rather than indexing directly.
/// </remarks>
public class TabManager
{
    public List<TabInfo> Tabs { get; } = new();
    public int ActiveTabIndex { get; private set; } = -1;

    public TabInfo? ActiveTab =>
        ActiveTabIndex >= 0 && ActiveTabIndex < Tabs.Count ? Tabs[ActiveTabIndex] : null;

    public void AddTab(TextBuffer buffer, CursorManager cursor, string? path = null)
    {
        var tab = new TabInfo(buffer, cursor) { FilePath = path };
        Tabs.Add(tab);
        ActiveTabIndex = Tabs.Count - 1;
    }

    /// <summary>
    /// Removes the tab at <paramref name="index"/>, keeping the same tab active where possible.
    /// </summary>
    /// <remarks>
    /// Closing a tab that sits *before* the active one used to leave the index pointing at whatever
    /// slid into that slot, so the editor silently jumped to a different file.
    /// </remarks>
    public void CloseTab(int index)
    {
        if (index < 0 || index >= Tabs.Count) return;

        int previousActive = ActiveTabIndex;
        Tabs.RemoveAt(index);

        if (Tabs.Count == 0)
        {
            ActiveTabIndex = -1;
            return;
        }

        ActiveTabIndex = index < previousActive
            ? previousActive - 1
            : Math.Min(previousActive, Tabs.Count - 1);
    }

    public void NextTab()
    {
        if (Tabs.Count <= 1) return;
        SelectTab((ActiveTabIndex + 1) % Tabs.Count);
    }

    public void PreviousTab()
    {
        if (Tabs.Count <= 1) return;
        SelectTab((ActiveTabIndex - 1 + Tabs.Count) % Tabs.Count);
    }

    /// <summary>
    /// Activates the tab at <paramref name="index"/>, clamped to the valid range.
    /// </summary>
    /// <remarks>
    /// <c>Math.Clamp(index, 0, Tabs.Count - 1)</c> throws when the list is empty because min &gt; max,
    /// so the empty case is handled explicitly.
    /// </remarks>
    public void SelectTab(int index)
    {
        if (Tabs.Count == 0)
        {
            ActiveTabIndex = -1;
            return;
        }

        ActiveTabIndex = Math.Clamp(index, 0, Tabs.Count - 1);
    }

    public int IndexOf(TabInfo tab) => Tabs.IndexOf(tab);
}

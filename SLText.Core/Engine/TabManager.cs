using SLText.Core.Engine.Model;

namespace SLText.Core.Engine;

public class TabManager
{
    public List<TabInfo> Tabs { get; } = new();
    public int ActiveTabIndex { get; private set; } = -1;
    public TabInfo? ActiveTab => ActiveTabIndex >= 0 ? Tabs[ActiveTabIndex] : null;

    public void AddTab(TextBuffer buffer, CursorManager cursor, string? path = null)
    {
        var tab = new TabInfo(buffer, cursor) { FilePath = path };
        Tabs.Add(tab);
        ActiveTabIndex = Tabs.Count - 1;
    }

    public void CloseTab(int index)
    {
        if (index < 0 || index >= Tabs.Count) return;

        Tabs.RemoveAt(index);

        if (Tabs.Count == 0)
        {
            ActiveTabIndex = -1;
        }
        else
        {
            if (ActiveTabIndex >= Tabs.Count)
            {
                ActiveTabIndex = Tabs.Count - 1;
            }
        }
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

    public void SelectTab(int index) => ActiveTabIndex = Math.Clamp(index, 0, Tabs.Count - 1);
}
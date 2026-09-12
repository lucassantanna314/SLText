using SLText.Core.Engine;
using SLText.Core.Engine.Model;
using Xunit;

namespace SLText.Core.Tests;

/// <summary>
/// Regression coverage for tab bookkeeping.
///
/// These encode the bugs that made switching and closing tabs unreliable: the active index was not
/// adjusted when a tab before it was removed (so the editor silently jumped to a different file),
/// and <c>SelectTab</c> threw when the last tab was closed because <c>Math.Clamp(0, 0, -1)</c> has
/// min &gt; max.
/// </summary>
public class TabManagerTests
{
    private static (TabInfo Tab, TextBuffer Buffer) NewTab(string? path = null)
    {
        var buffer = new TextBuffer();
        buffer.LoadText(path ?? "untitled");
        return (new TabInfo(buffer, new CursorManager(buffer)) { FilePath = path }, buffer);
    }

    private static TabManager WithTabs(int count)
    {
        var manager = new TabManager();
        for (int i = 0; i < count; i++)
        {
            var (tab, _) = NewTab($"file{i}.cs");
            manager.AddTab(tab.Buffer, tab.Cursor, tab.FilePath);
        }
        return manager;
    }

    [Fact]
    public void AddTab_MakesTheNewTabActive()
    {
        var manager = WithTabs(3);

        Assert.Equal(2, manager.ActiveTabIndex);
        Assert.Equal("file2.cs", manager.ActiveTab?.FilePath);
    }

    [Fact]
    public void CloseTab_BeforeActive_KeepsTheSameTabActive()
    {
        // The active tab must be the *middle* one: when it is the last tab the old clamping branch
        // happened to produce the right answer, which is why this bug went unnoticed.
        var manager = WithTabs(3);
        manager.SelectTab(1);
        var expected = manager.ActiveTab;
        Assert.Equal("file1.cs", expected?.FilePath);

        manager.CloseTab(0);

        Assert.Same(expected, manager.ActiveTab);
        Assert.Equal("file1.cs", manager.ActiveTab?.FilePath);
        Assert.Equal(0, manager.ActiveTabIndex);
    }

    [Fact]
    public void CloseTab_BeforeActive_DoesNotDriftByOneForEachRemoval()
    {
        var manager = WithTabs(5);
        manager.SelectTab(2);
        var expected = manager.ActiveTab;

        manager.CloseTab(0);
        manager.CloseTab(0);

        Assert.Same(expected, manager.ActiveTab);
        Assert.Equal("file2.cs", manager.ActiveTab?.FilePath);
    }

    [Fact]
    public void CloseTab_AfterActive_LeavesActiveUnchanged()
    {
        var manager = WithTabs(3);
        manager.SelectTab(0);
        var expected = manager.ActiveTab;

        manager.CloseTab(2);

        Assert.Same(expected, manager.ActiveTab);
        Assert.Equal(0, manager.ActiveTabIndex);
    }

    [Fact]
    public void CloseTab_ActiveTab_SelectsANeighbourWithoutGoingOutOfRange()
    {
        var manager = WithTabs(3);
        manager.SelectTab(1);

        manager.CloseTab(1);

        Assert.Equal(2, manager.Tabs.Count);
        Assert.InRange(manager.ActiveTabIndex, 0, manager.Tabs.Count - 1);
        Assert.NotNull(manager.ActiveTab);
    }

    [Fact]
    public void CloseTab_LastRemainingTab_ClearsActiveIndex()
    {
        var manager = WithTabs(1);

        manager.CloseTab(0);

        Assert.Empty(manager.Tabs);
        Assert.Equal(-1, manager.ActiveTabIndex);
        Assert.Null(manager.ActiveTab);
    }

    [Fact]
    public void CloseTab_OutOfRangeIndex_IsIgnored()
    {
        var manager = WithTabs(2);

        manager.CloseTab(-1);
        manager.CloseTab(99);

        Assert.Equal(2, manager.Tabs.Count);
    }

    [Fact]
    public void SelectTab_WhenNoTabs_DoesNotThrow()
    {
        var manager = new TabManager();

        // Math.Clamp(index, 0, Count - 1) throws ArgumentException when Count == 0.
        var exception = Record.Exception(() => manager.SelectTab(0));

        Assert.Null(exception);
        Assert.Equal(-1, manager.ActiveTabIndex);
    }

    [Fact]
    public void SelectTab_ClampsOutOfRangeIndex()
    {
        var manager = WithTabs(3);

        manager.SelectTab(99);
        Assert.Equal(2, manager.ActiveTabIndex);

        manager.SelectTab(-5);
        Assert.Equal(0, manager.ActiveTabIndex);
    }

    [Fact]
    public void ActiveTab_IsNullSafeAfterEveryTabIsRemoved()
    {
        var manager = WithTabs(2);

        manager.CloseTab(0);
        manager.CloseTab(0);

        Assert.Null(manager.ActiveTab);
    }

    [Fact]
    public void NextAndPreviousTab_WrapAround()
    {
        var manager = WithTabs(3);
        manager.SelectTab(0);

        manager.PreviousTab();
        Assert.Equal(2, manager.ActiveTabIndex);

        manager.NextTab();
        Assert.Equal(0, manager.ActiveTabIndex);
    }

    [Fact]
    public void NextTab_WithSingleTab_IsANoOp()
    {
        var manager = WithTabs(1);

        manager.NextTab();

        Assert.Equal(0, manager.ActiveTabIndex);
    }
}

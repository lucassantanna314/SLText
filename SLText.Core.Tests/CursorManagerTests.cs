using SLText.Core.Engine;
using Xunit;

namespace SLText.Core.Tests;

public class CursorManagerTests
{
    private static (TextBuffer Buffer, CursorManager Cursor) Create(params string[] lines)
    {
        var buffer = new TextBuffer();
        buffer.LoadText(string.Join("\n", lines));
        return (buffer, new CursorManager(buffer));
    }

    [Fact]
    public void MoveToLineStart_GoesToColumnZero()
    {
        var (_, cursor) = Create("    indented");
        cursor.SetPosition(0, 7);

        cursor.MoveToLineStart();

        Assert.Equal(0, cursor.Column);
    }

    [Fact]
    public void MoveToLineEnd_GoesToLastColumn()
    {
        var (_, cursor) = Create("abcdef");
        cursor.SetPosition(0, 1);

        cursor.MoveToLineEnd();

        Assert.Equal(6, cursor.Column);
    }

    [Fact]
    public void MoveToLineEnd_OnEmptyLine_StaysAtZero()
    {
        var (_, cursor) = Create("first", "", "third");
        cursor.SetPosition(1, 0);

        cursor.MoveToLineEnd();

        Assert.Equal(0, cursor.Column);
    }

    [Fact]
    public void MovePageDown_AdvancesByTheRequestedLineCount()
    {
        var (_, cursor) = Create(Enumerable.Range(0, 50).Select(i => $"line{i}").ToArray());
        cursor.SetPosition(0, 3);

        cursor.MovePageDown(10);

        Assert.Equal(10, cursor.Line);
    }

    [Fact]
    public void MovePageDown_ClampsAtLastLine()
    {
        var (_, cursor) = Create("a", "b", "c", "d", "e");
        cursor.SetPosition(0, 0);

        cursor.MovePageDown(100);

        Assert.Equal(4, cursor.Line);
    }

    [Fact]
    public void MovePageUp_ClampsAtFirstLine()
    {
        var (_, cursor) = Create("a", "b", "c");
        cursor.SetPosition(1, 0);

        cursor.MovePageUp(100);

        Assert.Equal(0, cursor.Line);
    }

    [Fact]
    public void MovePageDown_TreatsNonPositivePageSizeAsOneLine()
    {
        // Guards against a viewport that has not been laid out yet reporting zero lines.
        var (_, cursor) = Create("a", "b", "c");
        cursor.SetPosition(0, 0);

        cursor.MovePageDown(0);

        Assert.Equal(1, cursor.Line);
    }

    [Fact]
    public void MovePageUp_PreservesTheDesiredColumnAcrossShortLines()
    {
        var (_, cursor) = Create("0123456789", "ab", "0123456789");
        cursor.SetPosition(0, 8);

        cursor.MovePageDown(1); // lands on the short line, column clamps
        Assert.Equal(2, cursor.Column);

        cursor.MovePageDown(1); // back to a long line, desired column is restored
        Assert.Equal(8, cursor.Column);
    }

    [Fact]
    public void HomeEndAndPagingKeepSelectionAnchorSoShiftVariantsExtend()
    {
        var (_, cursor) = Create("hello world", "second line");
        cursor.SetPosition(0, 0);

        cursor.StartSelection();
        cursor.MoveToLineEnd();

        Assert.True(cursor.HasSelection);
        Assert.Equal(0, cursor.SelectionAnchorColumn);
        Assert.Equal(11, cursor.Column);
    }
}

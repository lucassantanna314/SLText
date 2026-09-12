using SLText.Core.Engine;
using Xunit;

namespace SLText.Core.Tests;

public class TabExpansionTests
{
    [Theory]
    [InlineData("", "")]
    [InlineData("no tabs here", "no tabs here")]
    [InlineData("\t", "    ")]
    [InlineData("\tint x;", "    int x;")]
    [InlineData("\t\treturn;", "        return;")]
    [InlineData("a\tb", "a    b")]
    public void Expand_ReplacesEachTabWithFixedWidth(string input, string expected)
    {
        Assert.Equal(expected, TabExpansion.Expand(input));
    }

    [Fact]
    public void Expand_ReturnsSameInstanceWhenNoTabs()
    {
        const string text = "already spaces";

        // Avoids a per-line allocation in the render hot path.
        Assert.Same(text, TabExpansion.Expand(text));
    }

    [Fact]
    public void Expand_HandlesNull()
    {
        Assert.Equal(string.Empty, TabExpansion.Expand(null));
    }

    [Theory]
    [InlineData("\tint x;", 0, 0)]
    [InlineData("\tint x;", 1, 4)]
    [InlineData("\tint x;", 2, 5)]
    [InlineData("ab\tcd", 0, 0)]
    [InlineData("ab\tcd", 2, 2)]
    [InlineData("ab\tcd", 3, 6)]
    [InlineData("ab\tcd", 4, 7)]
    [InlineData("plain", 3, 3)]
    public void ToDisplayColumn_AccountsForTabWidth(string line, int bufferColumn, int expected)
    {
        Assert.Equal(expected, TabExpansion.ToDisplayColumn(line, bufferColumn));
    }

    [Theory]
    [InlineData("\tint x;", 0, 0)]
    [InlineData("\tint x;", 4, 1)]
    [InlineData("\tint x;", 5, 2)]
    [InlineData("ab\tcd", 2, 2)]
    [InlineData("ab\tcd", 6, 3)]
    [InlineData("plain", 3, 3)]
    public void ToBufferColumn_RoundTripsWithDisplayColumn(string line, int displayColumn, int expected)
    {
        Assert.Equal(expected, TabExpansion.ToBufferColumn(line, displayColumn));
    }

    [Theory]
    [InlineData("\tint x;", 1)]
    [InlineData("\tint x;", 2)]
    [InlineData("ab\tcd", 3)]
    [InlineData("plain", 3)]
    public void ToDisplayAndBack_RoundTripsOnRealBufferColumns(string line, int bufferColumn)
    {
        int display = TabExpansion.ToDisplayColumn(line, bufferColumn);

        Assert.Equal(bufferColumn, TabExpansion.ToBufferColumn(line, display));
    }

    [Fact]
    public void ToDisplayColumn_ClampsColumnsPastEndOfLine()
    {
        // The caret can sit past the last character while typing. "\tab" renders as 6 cells
        // (4 + 1 + 1), and two further buffer columns add two more cells.
        Assert.Equal(8, TabExpansion.ToDisplayColumn("\tab", 5));
    }

    [Fact]
    public void ToBufferColumn_ClampsToLineLength()
    {
        Assert.Equal("abc".Length, TabExpansion.ToBufferColumn("abc", 999));
    }

    [Theory]
    [InlineData("", 5, 0)]
    [InlineData(null, 5, 0)]
    public void ToBufferColumn_HandlesEmptyAndNull(string? line, int displayColumn, int expected)
    {
        Assert.Equal(expected, TabExpansion.ToBufferColumn(line, displayColumn));
    }

    [Fact]
    public void TextBuffer_PreservesTabsOnLoadAndRoundTrip()
    {
        // Guards the data-loss bug: loading a tab-indented file must not rewrite it as spaces.
        var buffer = new TextBuffer();
        buffer.LoadText("\tint x = 1;\n\t\treturn x;");

        Assert.Equal("\tint x = 1;\n\t\treturn x;", buffer.GetAllText());
        Assert.Equal('\t', buffer.GetLine(0)[0]);
    }
}

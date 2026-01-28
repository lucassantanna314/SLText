using SLText.Core.Commands.Helper;
using SLText.Core.Engine;
using SLText.Core.Interfaces;

namespace SLText.Core.Commands;

public class InsertTabCommand : ICommand
{
    private readonly TextBuffer _buffer;
    private readonly CursorManager _cursor;
    private const string TabSpaces = "    ";

    public InsertTabCommand(TextBuffer buffer, CursorManager cursor)
    {
        _buffer = buffer;
        _cursor = cursor;
    }
    
    public void Execute()
{
    string lineText = _buffer.GetLines().ElementAt(_cursor.Line);
    string word = GetWordAtCursor(lineText, _cursor.Column);
    
    var snippetTemplate = SnippetProvider.GetSnippet(word);

    if (snippetTemplate != null)
    {
        for (int i = 0; i < word.Length; i++) _cursor.Backspace();

        int stopOffset = snippetTemplate.IndexOf('|');
        string cleanSnippet = snippetTemplate.Replace("|", "");

        if (cleanSnippet.Contains('\n'))
        {
            string normalizedSnippet = cleanSnippet.Replace("\r\n", "\n");
            string[] snippetLines = normalizedSnippet.Split('\n');

            int currentGlobalOffset = 0;
            int targetLine = -1;
            int targetCol = -1;

            for (int i = 0; i < snippetLines.Length; i++)
            {
                if (targetLine == -1 && stopOffset != -1)
                {
                    if (currentGlobalOffset + snippetLines[i].Length + 1 > stopOffset)
                    {
                        targetLine = _cursor.Line;
                        targetCol = _cursor.Column + (stopOffset - currentGlobalOffset);
                    }
                    currentGlobalOffset += snippetLines[i].Length + 1;
                }

                _buffer.Insert(_cursor.Line, _cursor.Column, snippetLines[i]);

                if (i < snippetLines.Length - 1)
                {
                    _buffer.BreakLine(_cursor.Line, _cursor.Column + snippetLines[i].Length);
                    _cursor.SetPosition(_cursor.Line + 1, 0);
                }
            }

            if (targetLine != -1)
                _cursor.SetPosition(targetLine, targetCol);
        }
        else
        {
            _buffer.Insert(_cursor.Line, _cursor.Column, cleanSnippet);
            
            if (stopOffset != -1)
            {
                for (int i = 0; i < stopOffset; i++) _cursor.MoveRight();
            }
        }
    }
    else
    {
        _buffer.Insert(_cursor.Line, _cursor.Column, "    ");
        for (int i = 0; i < 4; i++) _cursor.MoveRight();
    }
}
    
    private string GetWordAtCursor(string text, int col)
    {
        int start = col - 1;
        while (start >= 0 && char.IsLetterOrDigit(text[start])) start--;
        return text.Substring(start + 1, col - (start + 1));
    }

    public void Undo()
    {
       
    }
}
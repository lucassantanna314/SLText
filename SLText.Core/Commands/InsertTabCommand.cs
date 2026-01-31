using SLText.Core.Commands.Helper;
using SLText.Core.Engine;
using SLText.Core.Interfaces;

namespace SLText.Core.Commands;

public class InsertTabCommand : ICommand
{
    private readonly TextBuffer _buffer;
    private readonly CursorManager _cursor;
    private TextMemento? _snapshot; 
    private const string TabSpaces = "    ";

    public InsertTabCommand(TextBuffer buffer, CursorManager cursor)
    {
        _buffer = buffer;
        _cursor = cursor;
    }
    
    public void Execute()
    {
        _snapshot = _buffer.TakeSnapshot(_cursor.Line, _cursor.Column);

        string lineText = _buffer.GetLines().ElementAt(_cursor.Line);
        string word = GetWordAtCursor(lineText, _cursor.Column);
        
        var snippetTemplate = SnippetProvider.GetSnippet(word);

        if (snippetTemplate != null)
        {
            _buffer.RemoveRange(_cursor.Line, _cursor.Column - word.Length, _cursor.Line, _cursor.Column);
            _cursor.SetPosition(_cursor.Line, _cursor.Column - word.Length);

            int stopOffset = snippetTemplate.IndexOf('|');
            string cleanSnippet = snippetTemplate.Replace("|", "");

            if (cleanSnippet.Contains('\n'))
            {
                InsertMultilineSnippet(cleanSnippet, stopOffset);
            }
            else
            {
                _buffer.Insert(_cursor.Line, _cursor.Column, cleanSnippet);
                if (stopOffset != -1)
                {
                    _cursor.SetPosition(_cursor.Line, _cursor.Column + stopOffset);
                }
                else
                {
                    _cursor.SetPosition(_cursor.Line, _cursor.Column + cleanSnippet.Length);
                }
            }
        }
        else
        {
            // Inserção de Tab comum
            _buffer.Insert(_cursor.Line, _cursor.Column, TabSpaces);
            _cursor.SetPosition(_cursor.Line, _cursor.Column + TabSpaces.Length);
        }
    }

    private void InsertMultilineSnippet(string cleanSnippet, int stopOffset)
    {
        string normalizedSnippet = cleanSnippet.Replace("\r\n", "\n");
        string[] snippetLines = normalizedSnippet.Split('\n');

        int currentGlobalOffset = 0;
        int targetLine = -1;
        int targetCol = -1;

        for (int i = 0; i < snippetLines.Length; i++)
        {
            // Lógica para encontrar a posição do cursor (|) no snippet
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
    
    private string GetWordAtCursor(string text, int col)
    {
        int start = col - 1;
        while (start >= 0 && char.IsLetterOrDigit(text[start])) start--;
        return text.Substring(start + 1, col - (start + 1));
    }

    public void Undo()
    {
        if (_snapshot != null)
        {
            _buffer.RestoreSnapshot(_snapshot);
            _cursor.SetPosition(_snapshot.CursorLine, _snapshot.CursorColumn);
        }
    }
}
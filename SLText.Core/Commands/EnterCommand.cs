using SLText.Core.Engine;
using SLText.Core.Engine.Strategies;
using SLText.Core.Interfaces;

namespace SLText.Core.Commands;

public class EnterCommand : ICommand
{
    private readonly TextBuffer _buffer;
    private readonly CursorManager _cursor;
    private readonly string? _filePath;
    private TextMemento _snapshot;

    public EnterCommand(TextBuffer buffer, CursorManager cursor, string? filePath)
    {
        _buffer = buffer;
        _cursor = cursor;
        _filePath = filePath;
    }

    public void Execute()
    {
        _snapshot = _buffer.TakeSnapshot(_cursor.Line, _cursor.Column);

        int line = _cursor.Line;
        int col = _cursor.Column;
        
        var lines = _buffer.GetLines().ToList();
        if (line >= lines.Count) return;

        string currentLineText = lines[line];
        
        string extension = System.IO.Path.GetExtension(_filePath ?? "");
        var strategy = IndentationProvider.GetStrategy(extension);
        
        string baseIndent = strategy.GetIndentation(currentLineText, col);
        
        if (strategy.ShouldExpandBraces(currentLineText, col))
        {
            ExecuteExpansion(line, col, baseIndent);
        }
        else
        {
            ExecuteStandardNewline(line, col, baseIndent);
        }
    }

    private void ExecuteExpansion(int line, int col, string baseIndent)
    {
        _buffer.BreakLine(line, col);
    
        line++; 
        string midIndent = baseIndent + "    ";
        _buffer.InsertLine(line, midIndent); 
        
        _cursor.SetPosition(line, midIndent.Length);
    
        _buffer.Insert(line + 1, 0, baseIndent);
    }
    
    private void ExecuteStandardNewline(int line, int col, string baseIndent)
    {
        _buffer.BreakLine(line, col);
        line++;
        
        if (!string.IsNullOrEmpty(baseIndent))
        {
            _buffer.Insert(line, 0, baseIndent);
            _cursor.SetPosition(line, baseIndent.Length);
        }
        else
        {
            _cursor.SetPosition(line, 0);
        }
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
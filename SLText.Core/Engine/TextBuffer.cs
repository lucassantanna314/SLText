namespace SLText.Core.Engine;

public class TextBuffer
{
    private List<List<char>> _lines = new() { new List<char>() };
    
    public int LineCount => _lines.Count;
    
    public int GetLineLength(int index) => 
        (index >= 0 && index < _lines.Count) ? _lines[index].Count : 0;
    
    public IEnumerable<string> GetLines() => 
        _lines.Select(l => new string(l.ToArray()));

    public string GetAllText() => string.Join(Environment.NewLine, GetLines());


    public void Insert(int line, int column, char c)
    {
        EnsureLineExists(line);
        column = Math.Clamp(column, 0, _lines[line].Count);
        _lines[line].Insert(column, c);
    }
    
    public void Insert(int line, int column, string text)
    {
        EnsureLineExists(line);
        column = Math.Clamp(column, 0, _lines[line].Count);
        _lines[line].InsertRange(column, text.ToCharArray());
    }
    
    public void InsertLine(int index, string text)
    {
        index = Math.Clamp(index, 0, _lines.Count);
        _lines.Insert(index, text.ToList());
    }

    
    public void RemoveLine(int index)
    {
        if (index >= 0 && index < _lines.Count)
        {
            if (_lines.Count > 1) _lines.RemoveAt(index);
            else _lines[0].Clear();
        }
    }
    
    public void RemoveRange(int startLine, int startCol, int endLine, int endCol)
    {
        if (startLine == endLine)
        {
            int count = endCol - startCol;
            if (count > 0) _lines[startLine].RemoveRange(startCol, count);
            return;
        }

        var suffix = _lines[endLine].Skip(endCol).ToList();
        _lines[startLine].RemoveRange(startCol, _lines[startLine].Count - startCol);
        
        int linesToRemove = endLine - startLine;
        _lines.RemoveRange(startLine + 1, linesToRemove);
        
        _lines[startLine].AddRange(suffix);
    }

    public void BreakLine(int line, int column)
    {
        EnsureLineExists(line);
        column = Math.Clamp(column, 0, _lines[line].Count);
        
        var restOfLine = _lines[line].Skip(column).ToList();
        _lines[line].RemoveRange(column, restOfLine.Count);
        _lines.Insert(line + 1, restOfLine);
    }

    public void Backspace(int line, int column)
    {
        if (column > 0) _lines[line].RemoveAt(column - 1);
        else if (line > 0)
        {
            _lines[line - 1].AddRange(_lines[line]);
            _lines.RemoveAt(line);
        }
    }

    public void Delete(int line, int column)
    {
        if (column < _lines[line].Count) _lines[line].RemoveAt(column);
        else if (line < _lines.Count - 1)
        {
            _lines[line].AddRange(_lines[line + 1]);
            _lines.RemoveAt(line + 1);
        }
    }

    
    public TextMemento TakeSnapshot(int line, int col)
    {
        var linesCopy = _lines.Select(l => l.ToList()).ToList();
        return new TextMemento(linesCopy, line, col);
    }

    public void RestoreSnapshot(TextMemento memento)
    {
        this._lines = memento.Lines.Select(l => l.ToList()).ToList();
        if (this._lines.Count == 0) _lines.Add(new List<char>());
    }

    
    private void EnsureLineExists(int line)
    {
        while (_lines.Count <= line) _lines.Add(new List<char>());
    }
    
    public void LoadText(string content)
    {
        _lines = content.Replace("\r", "")
            .Split('\n')
            .Select(line => line.ToList())
            .ToList();
    
        if (_lines.Count == 0) _lines.Add(new List<char>());
    }
}
using System.Text;
using SLText.Core.Engine.Model;

namespace SLText.Core.Engine;

public class TextBuffer
{
    private List<List<char>> _lines = new() { new List<char>() };

    public int LineCount => _lines.Count;
    
    public int GetLineLength(int index) => 
        (index >= 0 && index < _lines.Count) ? _lines[index].Count : 0;
    
    public IEnumerable<string> GetLines() => 
        _lines.Select(l => new string(l.ToArray()));

    public string GetAllText() => string.Join("\n", GetLines());
    
    public string GetTextInRange(int startOffset, int length)
    {
        if (length <= 0 || startOffset < 0) return string.Empty;

        var sb = new StringBuilder();
        int currentCount = 0;     
        int charsCollected = 0;    

        int currentLine = 0;
        int currentCol = 0;
        
        for (int i = 0; i < _lines.Count; i++)
        {
            int lineLengthWithNewline = _lines[i].Count + 1; 

            if (currentCount + lineLengthWithNewline > startOffset)
            {
                currentLine = i;
                currentCol = startOffset - currentCount;
                break;
            }
            currentCount += lineLengthWithNewline;
        }

        while (charsCollected < length && currentLine < _lines.Count)
        {
            var lineChars = _lines[currentLine];

            if (currentCol < lineChars.Count)
            {
                sb.Append(lineChars[currentCol]);
                currentCol++;
                charsCollected++;
            }
            else if (currentCol == lineChars.Count)
            {
                if (currentLine < _lines.Count - 1)
                {
                    sb.Append('\n');
                    charsCollected++;
                }
                else 
                {
                    break; 
                }
                
                currentLine++;
                currentCol = 0;
            }
            else
            {
                currentLine++;
                currentCol = 0;
            }
        }

        return sb.ToString();
    }
    
    public List<SearchResult> SearchAll(string term)
    {
        var results = new List<SearchResult>();
        if (string.IsNullOrEmpty(term)) return results;

        var lines = GetLines().ToList();
        for (int i = 0; i < lines.Count; i++)
        {
            int index = 0;
            string currentLine = lines[i];
            while ((index = currentLine.IndexOf(term, index, StringComparison.OrdinalIgnoreCase)) != -1)
            {
                results.Add(new SearchResult(i, index, term.Length));
                index += term.Length;
            }
        }
        return results;
    }
    
    public (int line, int col)? FindNext(string query, int startLine, int startCol)
    {
        if (string.IsNullOrEmpty(query)) return null;

        var lines = GetLines().ToList();
    
        for (int l = startLine; l < lines.Count; l++)
        {
            string currentLine = lines[l];
            int colToStart = (l == startLine) ? Math.Min(startCol, currentLine.Length) : 0;
        
            if (colToStart < currentLine.Length || currentLine.Length == 0)
            {
                int foundIndex = currentLine.IndexOf(query, colToStart, StringComparison.OrdinalIgnoreCase);
                if (foundIndex >= 0) return (l, foundIndex);
            }
        }

        for (int l = 0; l <= startLine; l++)
        {
            string currentLine = lines[l];
            int limit = (l == startLine) ? Math.Min(startCol, currentLine.Length) : currentLine.Length;
        
            int foundIndex = currentLine.IndexOf(query, 0, StringComparison.OrdinalIgnoreCase);
            if (foundIndex >= 0 && (l < startLine || foundIndex < startCol)) 
                return (l, foundIndex);
        }

        return null;
    }
    
    public int GetFlatOffset(int line, int column)
    {
        int offset = 0;
        for (int i = 0; i < line && i < _lines.Count; i++)
        {
            offset += _lines[i].Count + 1; 
        }
        offset += column;
        return offset;
    }

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
        if (line < 0 || line >= _lines.Count) return;
    
        if (column > _lines[line].Count) 
        {
            column = _lines[line].Count; 
        }

        if (column > 0)
        {
            _lines[line].RemoveAt(column - 1);
        }
        else if (line > 0)
        {
            _lines[line - 1].AddRange(_lines[line]);
            _lines.RemoveAt(line);
        }
    }

    public void Delete(int line, int column)
    {
        if (column < _lines[line].Count)
        {
            _lines[line].RemoveAt(column);
        }
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
    
    public string GetLine(int index)
    {
        if (index < 0 || index >= _lines.Count) 
            return string.Empty;
        
        return new string(_lines[index].ToArray());
    }
}
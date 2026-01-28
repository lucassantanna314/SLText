namespace SLText.Core.Engine;

public class TextBuffer
{
    private List<List<char>> _lines = new() {new List<char>()};
    
    public int LineCount => _lines.Count;
    public int GetLineLength(int index) => _lines[index].Count;
    
    public IEnumerable<string> GetLines() 
    {
        return _lines.Select(l => new string(l.ToArray()));
    }

    public string GetAllText() => string.Join(Environment.NewLine, GetLines());
    
    //Insert
    public void Insert(char c, int line, int column)
    {
        _lines[line].Insert(column, c);
    }
    
    public void Insert(int line, int column, string text)
    {
        _lines[line].InsertRange(column, text.ToCharArray());
    }
    
    //actions
    
    public void RemoveRange(int startLine, int startCol, int endLine, int endCol)
    {
        // 1. Se a seleção for na mesma linha
        if (startLine == endLine)
        {
            int count = endCol - startCol;
            if (count > 0)
            {
                _lines[startLine].RemoveRange(startCol, count);
            }
            return;
        }

        // Seleção multi-linha
        // Salva o que sobrou da última linha selecionada
        var suffix = _lines[endLine].Skip(endCol).ToList();

        // Remove o conteúdo do início da seleção na primeira linha
        int charsToRemoveFirstLine = _lines[startLine].Count - startCol;
        _lines[startLine].RemoveRange(startCol, charsToRemoveFirstLine);

        // Remove as linhas intermediárias e a linha final
        int linesToRemove = endLine - startLine;
        _lines.RemoveRange(startLine + 1, linesToRemove);

        // "Costura" o sufixo da antiga última linha na primeira linha
        _lines[startLine].AddRange(suffix);
    }
    
    public void BreakLine(int line, int column)
    {
        var restOfLine = _lines[line].Skip(column).ToList();
        _lines[line].RemoveRange(column, restOfLine.Count);
        _lines.Insert(line + 1, restOfLine);
    }

    public void Backspace(int line, int column)
    {
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
        this._lines = memento.Lines;
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
using SLText.Core.Engine;
using SLText.Core.Interfaces;

namespace SLText.Core.Commands;

public class EnterCommand : ICommand
{
    private readonly TextBuffer _buffer;
    private readonly CursorManager _cursor;
    private TextMemento _snapshot;

    public EnterCommand(TextBuffer buffer, CursorManager cursor)
    {
        _buffer = buffer;
        _cursor = cursor;
    }

    public void Execute()
    {
        _snapshot = _buffer.TakeSnapshot(_cursor.Line, _cursor.Column);

        int line = _cursor.Line;
        int col = _cursor.Column;
        
        var lines = _buffer.GetLines().ToList();
        
        if (line >= lines.Count) return;

        string currentLineText = lines[line];
        int safeColumn = Math.Clamp(col, 0, currentLineText.Length);

        // Calcula indentação base
        string baseIndentation = "";
        foreach (char c in currentLineText)
        {
            if (c == ' ' || c == '\t') baseIndentation += c;
            else break;
        }

        // Verifica cenário: enter entre chaves {|}
        bool isBetweenBraces = safeColumn > 0 && safeColumn < currentLineText.Length &&
                               currentLineText[safeColumn - 1] == '{' && 
                               currentLineText[safeColumn] == '}';

        if (isBetweenBraces)
        {
            // Remove o '}' da posição atual para movê-lo para baixo depois
            _buffer.Delete(line, safeColumn); 

            // Quebra a linha após o '{'
            _buffer.BreakLine(line, safeColumn);
            line++; // Simula o movimento para a próxima linha

            // Insere a linha do meio com indentação extra
            string midIndentation = baseIndentation + "    ";
            _buffer.Insert(line, 0, midIndentation);

            // Quebra novamente para jogar o '}' para a linha de baixo
            _buffer.BreakLine(line, midIndentation.Length);
            
            // Insere o '}' na indentação correta
            _buffer.Insert(line + 1, 0, baseIndentation + "}");

            // Posiciona o cursor na linha do meio, no fim da indentação
            _cursor.SetPosition(line, midIndentation.Length);
        }
        else
        {
            // Cenário padrão: Quebra linha e mantêm indentação
            _buffer.BreakLine(line, safeColumn);
            line++;
        
            if (!string.IsNullOrEmpty(baseIndentation))
            {
                _buffer.Insert(line, 0, baseIndentation);
                _cursor.SetPosition(line, baseIndentation.Length);
            }
            else
            {
                _cursor.SetPosition(line, 0);
            }
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
using SLText.Core.Interfaces;
using SLText.Core.Engine;

namespace SLText.Core.Commands;

public class MoveFourLinesUpCommand : ICommand
{
    private readonly CursorManager _cursor;

    public MoveFourLinesUpCommand(CursorManager cursor)
    {
        _cursor = cursor;
    }

    public void Execute()
    {
        for (int i = 0; i < 4; i++)
        {
            _cursor.MoveUp();
        }
    }

    public void Undo()
    {
       
    }
}
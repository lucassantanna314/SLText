using SLText.Core.Interfaces;
using SLText.Core.Engine;

namespace SLText.Core.Commands;

public class MoveFourLinesDownCommand : ICommand
{
    private readonly CursorManager _cursor;

    public MoveFourLinesDownCommand(CursorManager cursor)
    {
        _cursor = cursor;
    }

    public void Execute()
    {
        for (int i = 0; i < 4; i++)
        {
            _cursor.MoveDown();
        }
    }

    public void Undo()
    {
       
    }
}
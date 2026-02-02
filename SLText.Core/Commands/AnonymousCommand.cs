using SLText.Core.Interfaces;

namespace SLText.Core.Commands;

public class AnonymousCommand : ICommand
{
    private readonly Action _action;
    public AnonymousCommand(Action action) => _action = action;
    public void Execute() => _action.Invoke();

    public void Undo()
    {
    }
}
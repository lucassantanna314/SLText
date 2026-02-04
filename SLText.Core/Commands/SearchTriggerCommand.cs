using SLText.Core.Interfaces;

namespace SLText.Core.Commands;

public class SearchTriggerCommand : ICommand
{
    private readonly Action _onSearch;

    public SearchTriggerCommand(Action onSearch)
    {
        _onSearch = onSearch;
    }

    public void Execute()
    {
        _onSearch?.Invoke();
    }
    public void Undo() { }
}
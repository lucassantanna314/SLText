namespace SLText.Core.Interfaces;

public interface ICommand
{
    void Execute();
    void Undo();
}
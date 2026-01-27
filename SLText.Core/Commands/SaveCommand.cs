using SLText.Core.Interfaces;

namespace SLText.Core.Commands;

public class SaveCommand : ICommand
{
    public void Execute()
    {
        Console.WriteLine("Arquivo salvo com sucesso!");
    }

    public void Undo()
    {
        
    }
}
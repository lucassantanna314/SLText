using SLText.Core.Engine;

// 1. Setup da Engine
var buffer = new TextBuffer();
var cursor = new CursorManager(buffer);
var undoManager = new UndoManager();
var inputHandler = new InputHandler(cursor, buffer, undoManager);

bool rodando = true;

Console.Clear();
Console.WriteLine("--- SLText Editor (CTRL+Z: Undo | CTRL+Y: Redo | ESC: Sair) ---");

while (rodando)
{
    // Renderização Simples no Console
    Console.SetCursorPosition(0, 2);
    Console.WriteLine("Conteúdo do Buffer:");
    Console.WriteLine("-------------------");
    Console.WriteLine(buffer.GetAllText() + " "); 
    Console.WriteLine("-------------------");
    Console.WriteLine($"Linha: {cursor.Line} | Coluna: {cursor.Column} | Histórico: {undoManager.HistoryCount}    ");
    
    var tecla = Console.ReadKey(true);

    // Verifica modificadores (Control, Shift)
    bool ctrl = tecla.Modifiers.HasFlag(ConsoleModifiers.Control);
    bool shift = tecla.Modifiers.HasFlag(ConsoleModifiers.Shift);

    switch (tecla.Key)
    {
        case ConsoleKey.Escape:
            rodando = false;
            break;

        // Comandos de Escrita e Atalhos via InputHandler
        case ConsoleKey.Z:
        case ConsoleKey.Y:
        case ConsoleKey.S:
            inputHandler.HandleShortcut(ctrl, shift, tecla.Key.ToString());
            break;

        case ConsoleKey.LeftArrow:
        case ConsoleKey.RightArrow:
            if (ctrl) 
                inputHandler.HandleShortcut(ctrl, shift, tecla.Key.ToString());
            else 
                cursor.MoveLeft(); // Navegação simples não precisa de comando/undo
            break;

        // Ações que ainda não transformamos em Comandos (podemos fazer depois)
        case ConsoleKey.Enter:
            cursor.Enter();
            break;

        case ConsoleKey.Backspace:
            cursor.Backspace(); 
            break;
        
        case ConsoleKey.Delete:
            cursor.Delete();
            break;

        case ConsoleKey.UpArrow:
            cursor.MoveUp();
            break;

        case ConsoleKey.DownArrow:
            cursor.MoveDown();
            break;

        default:
            // Digitação normal agrupada por palavras no InputHandler
            if (!char.IsControl(tecla.KeyChar))
            {
                inputHandler.HandleTextInput(tecla.KeyChar);
            }
            break;
    }
}

Console.Clear();
Console.WriteLine("Projeto SLText encerrado.");
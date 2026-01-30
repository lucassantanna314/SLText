using SLText.Core.Engine;
using SLText.View.Services;
using SLText.View.UI;

string? fileToOpen = args.Length > 0 ? args[0] : null;

var buffer = new TextBuffer();
var cursor = new CursorManager(buffer);
var undo = new UndoManager();

WindowManager windowManager = null!;

Action<string, bool> onFileAction = (path, isNewFile) => {
    windowManager.SetCurrentFile(path, isNewFile); 
};

var input = new InputHandler(
    cursor, 
    buffer, 
    undo, 
    new NativeDialogService(),
    onFileAction
);

windowManager = new WindowManager(buffer, cursor, input, fileToOpen);

windowManager.Run();
using SLText.Core.Engine;
using SLText.View.Services;
using SLText.View.UI;

string? fileToOpen = args.Length > 0 ? args[0] : null;

var buffer = new TextBuffer();
var cursor = new CursorManager(buffer);
var undo = new UndoManager();

WindowManager windowManager = null!;
InputHandler input = null!;

Action<string?, bool> onFileAction = (path, isOpening) => {
    if (path != null) 
    {
        input.UpdateLastDirectory(path);
    }
    if (isOpening) {
        windowManager.SetCurrentFile(path); 
    } else {
        windowManager.OnSaveSuccess(path);
    }
};

input = new InputHandler(
    cursor, 
    buffer, 
    undo, 
    new NativeDialogService(),
    () => windowManager.IsDirty, 
    onFileAction,
    () => windowManager.OpenSearch()
);

windowManager = new WindowManager(buffer, cursor, input, fileToOpen);

windowManager.Run();
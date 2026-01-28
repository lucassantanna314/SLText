using SLText.Core.Engine;
using SLText.View.UI;

string? fileToOpen = args.Length > 0 ? args[0] : null;

var buffer = new TextBuffer();
var cursor = new CursorManager(buffer);
var undo = new UndoManager();
var input = new InputHandler(cursor, buffer, undo);

var app = new WindowManager(buffer, cursor, input, fileToOpen);

app.Run();
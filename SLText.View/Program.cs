using SLText.Core.Engine;
using SLText.View.Services;
using SLText.View.UI;

//StartupLog.Write("Main entry", $"args={args.Length}");

// Any failure during startup is written to the log file before it escapes, so a silent
// abort still leaves a trail.
AppDomain.CurrentDomain.UnhandledException += (_, e) =>
    StartupLog.Write("UNHANDLED EXCEPTION", e.ExceptionObject?.ToString());

var settings = SettingsService.Load();
//StartupLog.Write("settings loaded", $"LastRootDirectory={settings.LastRootDirectory ?? "<null>"}, OpenTabs={settings.OpenTabs?.Count ?? 0}");

string? fileToOpen = args.Length > 0 ? args[0] : null;

var buffer = new TextBuffer();
var cursor = new CursorManager(buffer);
var undo = new UndoManager();

WindowManager windowManager = null!;
InputHandler input = null!;

Action<string?, bool> onFileAction = (path, isOpening) =>
{
    if (path != null)
    {
        input.UpdateLastDirectory(path);
        settings.LastRootDirectory = input.GetLastDirectory();
        SettingsService.SaveImmediate(settings);
    }
    if (isOpening)
    {
        windowManager.SetCurrentFile(path);
    }
    else
    {
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
//StartupLog.Write("InputHandler created");

windowManager = new WindowManager(buffer, cursor, input, fileToOpen, settings);
//StartupLog.Write("WindowManager constructed");

//StartupLog.Write("calling WindowManager.Run() - entering the GLFW event loop");
windowManager.Run();
//StartupLog.Write("Run() returned - exiting normally");

namespace SLText.View.Services;

/// <summary>
/// Temporary startup tracer used to find where launch stalls.
/// </summary>
/// <remarks>
/// Writes to a file as well as to stdout/stderr, flushing every line: if the process is suspended by
/// a job-control signal the console output can be lost, but the file still shows how far it got.
/// The log is truncated at the start of each run.
/// </remarks>
internal static class StartupLog
{
    public static readonly string FilePath = Path.Combine(Path.GetTempPath(), "sltext-startup.log");

    private static readonly object Gate = new();
    private static readonly DateTime StartedAt = DateTime.UtcNow;

    static StartupLog()
    {
        try
        {
            File.WriteAllText(FilePath,
                $"=== SLText startup {DateTime.Now:O} pid={Environment.ProcessId} ==={Environment.NewLine}");
        }
        catch { /* logging must never be the reason the app fails */ }
    }

    public static void Write(string stage, string? detail = null)
    {
        var line = $"[{(DateTime.UtcNow - StartedAt).TotalMilliseconds,8:F0} ms] {stage}" +
                   (detail is null ? string.Empty : $" :: {detail}");

        lock (Gate)
        {
            Try(() => File.AppendAllText(FilePath, line + Environment.NewLine));
            Try(() => { Console.Out.WriteLine(line); Console.Out.Flush(); });
            Try(() => { Console.Error.WriteLine(line); Console.Error.Flush(); });
        }
    }

    private static void Try(Action action)
    {
        try { action(); } catch { }
    }
}

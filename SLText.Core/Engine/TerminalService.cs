using System.Diagnostics;

namespace SLText.Core.Engine;

public class TerminalService
{
    private Process? _process;
    private StreamWriter? _input;
    public event Action<string>? OnDataReceived;

    public void Start(string? workingDirectory = null)
    {
        var isWindows = OperatingSystem.IsWindows();
        var shell = isWindows ? "powershell.exe" : "/bin/bash";

        var startInfo = new ProcessStartInfo
        {
            FileName = shell,
            Arguments = isWindows ? "-NoLogo -NoExit" : "-i",
            RedirectStandardInput = true,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
            CreateNoWindow = true,
            WorkingDirectory = workingDirectory ?? Environment.GetFolderPath(Environment.SpecialFolder.UserProfile),
            StandardOutputEncoding = System.Text.Encoding.UTF8
        };

        _process = new Process { StartInfo = startInfo };
        _process.Start();

        _input = _process.StandardInput;
        _input.AutoFlush = true;

        Task.Run(() => ReadStream(_process.StandardOutput));
        Task.Run(() => ReadStream(_process.StandardError));

    }
    
    private void ReadStream(StreamReader reader)
    {
        char[] buffer = new char[1024];
        while (!_process!.HasExited)
        {
            int count = reader.Read(buffer, 0, buffer.Length);
            if (count > 0)
            {
                string data = new string(buffer, 0, count);
                OnDataReceived?.Invoke(data);
            }
        }
    }

    public void SendCommand(string cmd) 
    {
        if (_input != null)
        {
            _input.Write(cmd);
            _input.Flush();
        }
    }
    
    public void Stop()
    {
        try
        {
            if (_process != null && !_process.HasExited)
            {
                _process.Kill(true); 
                _process.Dispose();
            }
        }
        catch {/* */ }
    }
    
    public void SendInterrupt()
    {
        SendCommand("\x03");
    }
}
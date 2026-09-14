using System.Diagnostics;
using System.Text;

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

        // bash must NOT be started with -i. An interactive shell tries to take control of the
        // process' terminal (tcsetpgrp / termios), and because this process runs with its stdio
        // redirected to pipes it ends up blocked on the tty: the shell is stopped by SIGTTIN and
        // takes the editor's process group down with it. Commands are fed in explicitly through
        // SendCommand, which is all an embedded terminal panel needs.
        var startInfo = new ProcessStartInfo
        {
            FileName = shell,
            Arguments = isWindows ? "-NoLogo -NoExit" : "--norc",
            RedirectStandardInput = true,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
            CreateNoWindow = true,
            WorkingDirectory = workingDirectory ?? Environment.GetFolderPath(Environment.SpecialFolder.UserProfile),
            StandardOutputEncoding = Encoding.UTF8
        };

        _process = new Process { StartInfo = startInfo };

        try
        {
            _process.Start();
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[Terminal] Failed to start shell: {ex.Message}");
            return;
        }

        _input = new StreamWriter(_process.StandardInput.BaseStream, new UTF8Encoding())
        {
            AutoFlush = true
        };

        _input.AutoFlush = true;

        Task.Run(() => ReadStream(_process.StandardOutput));
        Task.Run(() => ReadStream(_process.StandardError));

        // Send initial command to get the shell prompt to display
        // Bash --norc doesn't produce any output on startup
        Task.Run(async () =>
        {
            await Task.Delay(150); // Aguarda o processo inicializar
        
            if (isWindows)
            {
                SendCommand("\n");
            }
            else
            {
                // Configura o prompt para exibir usuario@hostname:diretorio$ no bash
                SendCommand("export PROMPT_COMMAND='printf \"\\n%s@%s:%s$ \" \"$USER\" \"${HOSTNAME:-$(hostname)}\" \"$PWD\"'; echo -n \"$USER@${HOSTNAME:-$(hostname)}:$PWD$ \"\n");
            }
        });

    }

    private void ReadStream(StreamReader reader)
    {
        char[] buffer = new char[1024];
        try
        {
            while (!_process!.HasExited)
            {
                int count = reader.Read(buffer, 0, buffer.Length);
                if (count > 0)
                {
                    string data = new string(buffer, 0, count);
                    OnDataReceived?.Invoke(data);
                }
                else
                {
                    // No data available, small delay to avoid busy-waiting
                    Thread.Sleep(10);
                }
            }
        }
        catch (Exception)
        {
            // Ignore exceptions during read (process may have exited)
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

    public void Restart(string? workingDirectory = null)
    {
        Stop();
        Start(workingDirectory);
    }

    public bool IsProcessRunning()
    {
        if (_process == null || _process.HasExited) return false;

        try
        {
            return !_process.HasExited;
        }
        catch { return false; }
    }

}
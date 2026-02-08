using System.Text.Json;
using SLText.Core.Engine.Model;

namespace SLText.View.Services;

public class RunService
{
    private List<RunConfiguration> _configurations = new();
    public IReadOnlyList<RunConfiguration> Configurations => _configurations;
    
    public void ScanProject(string rootPath)
    {
        _configurations.Clear();

        if (string.IsNullOrEmpty(rootPath) || !Directory.Exists(rootPath)) return;

        var launchFiles = Directory.GetFiles(rootPath, "launchSettings.json", SearchOption.AllDirectories);

        foreach (var file in launchFiles)
        {
            ParseLaunchSettings(file);
        }

        if (_configurations.Count == 0)
        {
            _configurations.Add(new RunConfiguration 
            { 
                Name = "Default Run", 
                Command = "dotnet run", 
                WorkingDirectory = rootPath 
            });
        }
    }
    
    private void ParseLaunchSettings(string filePath)
    {
        try
        {
            string jsonString = File.ReadAllText(filePath);
            using var doc = JsonDocument.Parse(jsonString);
            
            if (doc.RootElement.TryGetProperty("profiles", out var profiles))
            {
                string? workingDir = Path.GetDirectoryName(Path.GetDirectoryName(filePath)); // Sobe 1 nível de 'Properties'

                foreach (var profile in profiles.EnumerateObject())
                {
                    var config = new RunConfiguration
                    {
                        Name = profile.Name,
                        WorkingDirectory = workingDir ?? "",
                        IsGenerated = true
                    };

                    if (profile.Value.TryGetProperty("commandName", out var cmd))
                    {
                        config.Command = cmd.GetString() == "Project" ? "dotnet run" : cmd.GetString() ?? "";
                    }

                    _configurations.Add(config);
                }
            }
        }
        catch { /*  */ }
    }
}
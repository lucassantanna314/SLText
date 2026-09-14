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
        else if (_configurations.Count > 1)
        {
            // Cria uma opção composta para rodar API e View juntos em abas separadas de uma só vez
            var compound = new RunConfiguration
            {
                Name = "Run All (Api + View)",
                Type = RunType.Compound,
                ChildrenIds = [.. _configurations.Select(c => c.Id)]
            };
            _configurations.Insert(0, compound);
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
                string projectName = !string.IsNullOrEmpty(workingDir) ? Path.GetFileName(workingDir) : "";

                foreach (var profile in profiles.EnumerateObject())
                {
                    // Prefixa o nome do projeto (ex: "[Api] http" / "[View] http") para diferenciar no seletor e nas abas do terminal
                    string configName = !string.IsNullOrEmpty(projectName)
                        ? $"[{projectName}] {profile.Name}"
                        : profile.Name;

                    var config = new RunConfiguration
                    {
                        Name = configName,
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
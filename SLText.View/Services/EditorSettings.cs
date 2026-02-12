using System.Text.Json;

namespace SLText.View.Services;

public class EditorSettings
{
    public float FontSize { get; set; } = 16f;
    public string? LastRootDirectory { get; set; }
    public string Theme { get; set; } = "Dark";
    public List<string> OpenTabs { get; set; } = new();
}

public static class SettingsService
{
    private static string GetSettingsPath()
    {
        string userConfigDir = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), 
            "SLText"
        );

#if DEBUG
        var baseDir = AppContext.BaseDirectory;
        var projectRoot = Path.Combine(baseDir, "..", "..", "..");
        var sourceAssets = Path.Combine(projectRoot, "Assets");

        if (Directory.Exists(sourceAssets))
        {
            return Path.Combine(sourceAssets, "settings.json");
        }
#endif

        if (!Directory.Exists(userConfigDir))
        {
            Directory.CreateDirectory(userConfigDir);
        }

        return Path.Combine(userConfigDir, "settings.json");
    }

    private static readonly string SettingsPath = GetSettingsPath();
    private static System.Timers.Timer? _debounceTimer;
    private static EditorSettings? _pendingSettings;
    private static readonly object _lock = new();
    public static EditorSettings Load()
    {
        if (!File.Exists(SettingsPath)) return new EditorSettings();
        try
        {
            var json = File.ReadAllText(SettingsPath);
            return JsonSerializer.Deserialize<EditorSettings>(json) ?? new EditorSettings();
        }
        catch { return new EditorSettings(); }
    }

    public static void SaveDebounced(EditorSettings settings)
    {
        lock (_lock)
        {
            _pendingSettings = settings;
            
            if (_debounceTimer != null)
            {
                _debounceTimer.Stop();
                _debounceTimer.Dispose();
            }

            _debounceTimer = new System.Timers.Timer(1000);
            _debounceTimer.AutoReset = false; 
            _debounceTimer.Elapsed += (sender, e) => 
            {
                if (_pendingSettings != null)
                {
                    SaveImmediate(_pendingSettings);
                }
            };
            _debounceTimer.Start();
        }
    }
    
    public static void SaveImmediate(EditorSettings settings)
    {
        try
        {
            lock (_lock)
            {
                string? dir = Path.GetDirectoryName(SettingsPath);
                if (dir != null && !Directory.Exists(dir)) Directory.CreateDirectory(dir);

                var json = JsonSerializer.Serialize(settings, new JsonSerializerOptions { WriteIndented = true });
                File.WriteAllText(SettingsPath, json);
                Console.WriteLine($"[CONFIG] Salvo com sucesso em: {SettingsPath}");
            }
        }
        catch (Exception ex) { Console.WriteLine($"Erro ao salvar: {ex.Message}"); }
    }
}
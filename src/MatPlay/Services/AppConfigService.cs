using System.Text.Json;

namespace MatPlay.Services;

public class AppConfig
{
    public bool AllowRegistration { get; set; } = true;
    public string AppName { get; set; } = "MatPlay";
}

/// <summary>JSON-Config im Datenverzeichnis (Volume), primäre Verwendung laut Guideline: Configs.</summary>
public class AppConfigService
{
    private static readonly JsonSerializerOptions JsonOpts = new() { WriteIndented = true };
    private readonly string _path;

    public AppConfig Config { get; private set; }

    public AppConfigService(string dataDir)
    {
        var configDir = Path.Combine(dataDir, "config");
        Directory.CreateDirectory(configDir);
        _path = Path.Combine(configDir, "app.json");
        Config = Load();
    }

    private AppConfig Load()
    {
        if (File.Exists(_path))
        {
            try
            {
                return JsonSerializer.Deserialize<AppConfig>(File.ReadAllText(_path)) ?? new AppConfig();
            }
            catch (JsonException)
            {
                // defekte Config nicht überschreiben, mit Defaults weiterlaufen
                return new AppConfig();
            }
        }
        var config = new AppConfig();
        File.WriteAllText(_path, JsonSerializer.Serialize(config, JsonOpts));
        return config;
    }

    public void Save(AppConfig config)
    {
        Config = config;
        File.WriteAllText(_path, JsonSerializer.Serialize(config, JsonOpts));
    }
}

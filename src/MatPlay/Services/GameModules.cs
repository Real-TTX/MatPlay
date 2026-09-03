using System.Text.Json;
using System.Text.Json.Serialization;

namespace MatPlay.Services;

/// <summary>Konfiguration des generischen Punktezählers.</summary>
public class CounterConfig
{
    public int StartScore { get; set; }
    public int Step { get; set; } = 1;
    public int? TargetScore { get; set; }
    public bool LowestWins { get; set; }
    public bool AllowNegative { get; set; } = true;
    public bool UseRounds { get; set; }
}

public class GamePreset
{
    public required string Key { get; init; }
    public required string Name { get; init; }
    public required string Description { get; init; }
    public required string Icon { get; init; }
    public required string Accent { get; init; }
    public required string ModuleKey { get; init; }
    public string ConfigJson { get; init; } = "{}";
}

/// <summary>Ein Spielmodul: rendert Play-UI über ein Partial und liefert Standard-Konfiguration.</summary>
public class GameModule
{
    public required string Key { get; init; }
    public required string Name { get; init; }
    public required string Description { get; init; }
    public required string Icon { get; init; }
    public required string Accent { get; init; }
    /// <summary>Partial unter Pages/Play/Modules/</summary>
    public required string PlayPartial { get; init; }
    public string DefaultConfigJson { get; init; } = "{}";
    public bool HasConfigForm { get; init; }
}

/// <summary>Registry aller Spielmodule – neue Module hier registrieren.</summary>
public static class ModuleRegistry
{
    public static readonly JsonSerializerOptions JsonOpts = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
    };

    public static readonly IReadOnlyList<GameModule> Modules =
    [
        new GameModule
        {
            Key = "counter",
            Name = "Punktezähler",
            Description = "Generischer Zähler für beliebige Karten- und Brettspiele – hoch oder runter.",
            Icon = "🎯",
            Accent = "cyan",
            PlayPartial = "Modules/_Counter",
            DefaultConfigJson = JsonSerializer.Serialize(new CounterConfig(), JsonOpts),
            HasConfigForm = true,
        },
        new GameModule
        {
            Key = "qwixx",
            Name = "Qwixx",
            Description = "Der Qwixx-Zettel als App: vier Farbreihen, Fehlwürfe, automatische Punktzahl.",
            Icon = "🎲",
            Accent = "magenta",
            PlayPartial = "Modules/_Qwixx",
        },
    ];

    public static readonly IReadOnlyList<GamePreset> Presets =
    [
        new GamePreset
        {
            Key = "counter",
            Name = "Punktezähler",
            Description = "Frei konfigurierbar – für alles, was Punkte hat.",
            Icon = "🎯",
            Accent = "cyan",
            ModuleKey = "counter",
            ConfigJson = JsonSerializer.Serialize(new CounterConfig(), JsonOpts),
        },
        new GamePreset
        {
            Key = "20ab",
            Name = "20 Ab",
            Description = "Alle starten bei 20, runter auf 0 – wer zuerst unten ist, gewinnt.",
            Icon = "🃏",
            Accent = "lime",
            ModuleKey = "counter",
            ConfigJson = JsonSerializer.Serialize(new CounterConfig
            {
                StartScore = 20, Step = 1, TargetScore = 0, LowestWins = true, AllowNegative = false,
            }, JsonOpts),
        },
        new GamePreset
        {
            Key = "phase10",
            Name = "Phase 10 (Punkte)",
            Description = "Minuspunkte pro Runde zählen – wenigste Punkte gewinnen.",
            Icon = "🔟",
            Accent = "orange",
            ModuleKey = "counter",
            ConfigJson = JsonSerializer.Serialize(new CounterConfig
            {
                StartScore = 0, Step = 5, LowestWins = true, AllowNegative = false, UseRounds = true,
            }, JsonOpts),
        },
        new GamePreset
        {
            Key = "qwixx",
            Name = "Qwixx",
            Description = "Der schnelle Würfelklassiker mit digitalem Zettel.",
            Icon = "🎲",
            Accent = "magenta",
            ModuleKey = "qwixx",
        },
    ];

    public static GameModule? GetModule(string key) => Modules.FirstOrDefault(m => m.Key == key);
    public static GamePreset? GetPreset(string key) => Presets.FirstOrDefault(p => p.Key == key);
}

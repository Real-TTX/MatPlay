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

/// <summary>Konfiguration des Munchkin-Trackers (Quest-Variante zählt Lebenspunkte mit).</summary>
public class MunchkinConfig
{
    public bool TrackHealth { get; set; }
    public int MaxLevel { get; set; } = 10;
    public int StartLevel { get; set; } = 1;
    public int StartHealth { get; set; } = 4;
}

/// <summary>Qwixx-Konfiguration: Varianten nach "Qwixx gemixxt" (A = Farbsegmente, B = wilde Zahlen).</summary>
public class QwixxConfig
{
    /// <summary>classic | mixedNumbers | mixedColors</summary>
    public string Variant { get; set; } = "classic";
    public List<QwixxRow> Rows { get; set; } = [];
}

public class QwixxRow
{
    public string Key { get; set; } = "";
    /// <summary>Reihenfarbe (classic/mixedNumbers) oder null bei Farbsegmenten.</summary>
    public string? Color { get; set; }
    public List<QwixxCell> Cells { get; set; } = [];
}

public class QwixxCell
{
    public int N { get; set; }
    public string Color { get; set; } = "";
}

public static class QwixxPad
{
    private static readonly string[] Colors = ["red", "yellow", "green", "blue"];

    public static QwixxConfig Generate(string variant)
    {
        var random = Random.Shared;
        var config = new QwixxConfig { Variant = variant };

        switch (variant)
        {
            case "mixedNumbers":
                // Variante B: Zahlen innerhalb der Farbreihen wild gemischt
                foreach (var color in Colors)
                {
                    var numbers = Enumerable.Range(2, 11).OrderBy(_ => random.Next()).ToList();
                    config.Rows.Add(new QwixxRow
                    {
                        Key = color,
                        Color = color,
                        Cells = numbers.Select(n => new QwixxCell { N = n, Color = color }).ToList(),
                    });
                }
                break;

            case "mixedColors":
                // Variante A: Zahlen auf-/absteigend, Farben in Segmenten von 2-4 Feldern
                for (var i = 0; i < 4; i++)
                {
                    var ascending = i < 2;
                    var numbers = ascending
                        ? Enumerable.Range(2, 11).ToList()
                        : Enumerable.Range(2, 11).Reverse().ToList();
                    var cells = new List<QwixxCell>();
                    var colorPool = Colors.OrderBy(_ => random.Next()).ToList();
                    var colorIdx = 0;
                    var pos = 0;
                    while (pos < numbers.Count)
                    {
                        var len = Math.Min(random.Next(2, 5), numbers.Count - pos);
                        // Reststück von 1 vermeiden
                        if (numbers.Count - pos - len == 1) len++;
                        var color = colorPool[colorIdx % colorPool.Count];
                        colorIdx++;
                        for (var c = 0; c < len && pos < numbers.Count; c++, pos++)
                            cells.Add(new QwixxCell { N = numbers[pos], Color = color });
                    }
                    config.Rows.Add(new QwixxRow { Key = $"row{i + 1}", Color = null, Cells = cells });
                }
                break;

            default:
                // Klassisch: rot/gelb aufsteigend, grün/blau absteigend
                foreach (var (color, ascending) in new[] { ("red", true), ("yellow", true), ("green", false), ("blue", false) })
                {
                    var numbers = ascending
                        ? Enumerable.Range(2, 11).ToList()
                        : Enumerable.Range(2, 11).Reverse().ToList();
                    config.Rows.Add(new QwixxRow
                    {
                        Key = color,
                        Color = color,
                        Cells = numbers.Select(n => new QwixxCell { N = n, Color = color }).ToList(),
                    });
                }
                break;
        }
        return config;
    }

    public static readonly (string Key, string Name)[] Variants =
    [
        ("classic", "Klassisch"),
        ("mixedNumbers", "Gemixxt – wilde Zahlen (Variante B)"),
        ("mixedColors", "Gemixxt – Farbsegmente (Variante A)"),
    ];
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
        new GameModule
        {
            Key = "kniffel",
            Name = "Kniffel",
            Description = "Der komplette Kniffel-Block: oberer und unterer Teil, Bonus wird automatisch gerechnet.",
            Icon = "🎰",
            Accent = "lime",
            PlayPartial = "Modules/_Kniffel",
        },
        new GameModule
        {
            Key = "munchkin",
            Name = "Munchkin",
            Description = "Level, Boni und Kampfkraft im Blick – optional mit Lebenspunkten (Munchkin Quest).",
            Icon = "⚔️",
            Accent = "orange",
            PlayPartial = "Modules/_Munchkin",
            DefaultConfigJson = JsonSerializer.Serialize(new MunchkinConfig(), JsonOpts),
            HasConfigForm = true,
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
            Key = "frantic",
            Name = "Frantic",
            Description = "Minuspunkte pro Runde – bei 154 (kurz 137, lang 179) ist Schluss, wenigste Punkte gewinnen.",
            Icon = "😈",
            Accent = "magenta",
            ModuleKey = "counter",
            ConfigJson = JsonSerializer.Serialize(new CounterConfig
            {
                StartScore = 0, Step = 1, TargetScore = 154, LowestWins = true,
                AllowNegative = false, UseRounds = true,
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
        new GamePreset
        {
            Key = "kniffel",
            Name = "Kniffel",
            Description = "Fünf Würfel, dreizehn Felder – der Klassiker als digitaler Block.",
            Icon = "🎰",
            Accent = "lime",
            ModuleKey = "kniffel",
        },
        new GamePreset
        {
            Key = "munchkin",
            Name = "Munchkin",
            Description = "Level und Boni zählen, Kampfkraft immer im Blick – bis Stufe 10.",
            Icon = "⚔️",
            Accent = "orange",
            ModuleKey = "munchkin",
            ConfigJson = JsonSerializer.Serialize(new MunchkinConfig(), JsonOpts),
        },
        new GamePreset
        {
            Key = "munchkin-quest",
            Name = "Munchkin Quest",
            Description = "Die Brettspiel-Variante: Level, Boni und 4 Lebenspunkte pro Munchkin.",
            Icon = "🐉",
            Accent = "orange",
            ModuleKey = "munchkin",
            ConfigJson = JsonSerializer.Serialize(new MunchkinConfig { TrackHealth = true }, JsonOpts),
        },
        new GamePreset
        {
            Key = "qwixx-gemixxt",
            Name = "Qwixx gemixxt",
            Description = "Die Erweiterung: wilde Zahlen oder Farbsegmente – jedes Spiel ein frischer Block.",
            Icon = "🌀",
            Accent = "cyan",
            ModuleKey = "qwixx",
            ConfigJson = """{"variant":"mixedNumbers"}""",
        },
    ];

    public static GameModule? GetModule(string key) => Modules.FirstOrDefault(m => m.Key == key);
    public static GamePreset? GetPreset(string key) => Presets.FirstOrDefault(p => p.Key == key);
}

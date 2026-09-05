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
    /// <summary>Rundenmodus: eingegebene Punkte werden abgezogen statt addiert (z.B. Darts 501).</summary>
    public bool SubtractRounds { get; set; }
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
    /// <summary>Kurzregeln/Spickzettel, angezeigt über den Hilfe-Button im Spiel.</summary>
    public string[] Rules { get; init; } = [];
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
            Icon = "🧮",
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
            Key = "wizard",
            Name = "Wizard",
            Description = "Stiche ansagen und treffen – Punkte werden automatisch berechnet.",
            Icon = "🧙",
            Accent = "cyan",
            PlayPartial = "Modules/_Wizard",
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
            Icon = "🧮",
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
            Rules =
            [
                "Jeder startet mit 20 Punkten, Herz ist immer Trumpf.",
                "Pro gemachtem Stich wird 1 Punkt abgezogen – wer zuerst auf 0 ist, gewinnt.",
                "Wer keinen Stich macht, bekommt 5 Punkte dazu.",
                "Herz blind: Vor dem Kartenaufnehmen angesagt – die Punkte zählen doppelt.",
                "Achtung: Es gibt viele regionale Varianten – einigt euch vor der ersten Runde. 😉",
            ],
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
            Rules =
            [
                "10 Phasen in fester Reihenfolge schaffen (Zwillinge, Folgen, Farben …).",
                "Rundenende: Restkarten der anderen zählen als Minuspunkte (hier eintragen).",
                "Kartenwerte: 1-9 = 5 Punkte, 10-12 = 10 Punkte, Aussetzen = 15, Joker = 25.",
                "Wer zuerst Phase 10 schafft, beendet das Spiel – bei Gleichstand entscheiden die wenigsten Punkte.",
            ],
        },
        new GamePreset
        {
            Key = "flip7",
            Name = "Flip 7",
            Description = "Push your luck! Rundenpunkte sammeln – wer zuerst 200 erreicht, gewinnt.",
            Icon = "7️⃣",
            Accent = "cyan",
            ModuleKey = "counter",
            ConfigJson = JsonSerializer.Serialize(new CounterConfig
            {
                StartScore = 0, Step = 1, TargetScore = 200, LowestWins = false,
                AllowNegative = false, UseRounds = true,
            }, JsonOpts),
            Rules =
            [
                "Karten ziehen, solange du willst – doppelte Zahl = geplatzt, Runde zählt 0 Punkte.",
                "Rundenpunkte = Summe deiner Zahlenkarten + Modifikatoren (+2 … +10, x2).",
                "Flip 7: Sieben verschiedene Zahlenkarten = +15 Bonus, die Runde endet sofort.",
                "Aktionskarten: Freeze (Spieler stoppt), Flip Three (3 Karten ziehen), Second Chance (rettet vor Doppelter).",
                "Wer zuerst 200 Punkte erreicht, gewinnt.",
            ],
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
            Rules =
            [
                "Karten loswerden wie bei UNO – aber mit fiesen Ereignis- und Spezialkarten.",
                "Rundenende: Alle zählen ihre Handkarten als Minuspunkte (hier eintragen).",
                "Spielende beim Punktelimit: kurz 137, normal 154, lang 179 – wenigste Punkte gewinnen.",
            ],
        },
        new GamePreset
        {
            Key = "qwixx",
            Name = "Qwixx",
            Description = "Der schnelle Würfelklassiker mit digitalem Zettel.",
            Icon = "🎲",
            Accent = "magenta",
            ModuleKey = "qwixx",
            Rules =
            [
                "Summe der weißen Würfel darf JEDER ankreuzen, der aktive Spieler zusätzlich weiß + Farbe.",
                "Kreuze nur von links nach rechts – übersprungene Zahlen sind weg.",
                "Die letzte Zahl einer Reihe braucht mindestens 5 Kreuze davor und schließt die Reihe für alle.",
                "Aktiver Spieler ohne Kreuz = Fehlwurf (−5, maximal 4).",
                "Ende bei 2 geschlossenen Reihen oder 4 Fehlwürfen – Punkte je Reihe steigen mit den Kreuzen.",
            ],
        },
        new GamePreset
        {
            Key = "kniffel",
            Name = "Kniffel",
            Description = "Fünf Würfel, dreizehn Felder – der Klassiker als digitaler Block.",
            Icon = "🎰",
            Accent = "lime",
            ModuleKey = "kniffel",
            Rules =
            [
                "Bis zu 3 Würfe pro Zug, beliebig viele Würfel liegen lassen.",
                "Nach dem Zug MUSS ein Feld gewählt werden – notfalls streichen (✖).",
                "Oben ab 63 Punkten gibt es +35 Bonus (wird automatisch gerechnet).",
                "Full House 25, Kleine Straße 30, Große Straße 40, Kniffel 50, Chance = Augensumme.",
            ],
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
            Rules =
            [
                "Kampfkraft = Level + Boni durch Ausrüstung (hier automatisch gerechnet).",
                "Monster besiegt: Level(s) hoch + Schätze; verloren & Weglaufen misslingt = Schlimme Dinge.",
                "Level 9 → 10 nur durch einen Monster-Kill – wer zuerst Level 10 erreicht, gewinnt.",
            ],
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
            Rules =
            [
                "Jeder Munchkin startet mit 4 Lebenspunkten – bei 0 heißt es sterben & wiederauferstehen.",
                "Kampfkraft = Level + Boni (hier automatisch gerechnet).",
                "Level 10 erreichen reicht nicht: Zurück in die Eingangshalle und den Boss (Stufe 20) besiegen!",
            ],
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
            Rules =
            [
                "Alle Qwixx-Grundregeln gelten weiter.",
                "Variante B (wilde Zahlen): Zahlen sind gemischt – angekreuzt wird trotzdem nur von links nach rechts.",
                "Variante A (Farbsegmente): Zahlen laufen auf/ab, aber die Feldfarbe bestimmt den passenden Farbwürfel.",
                "Der Block wird hier für jedes Spiel frisch ausgewürfelt.",
            ],
        },
        new GamePreset
        {
            Key = "wizard",
            Name = "Wizard",
            Description = "Stiche ansagen, exakt treffen – der Punkteblock rechnet automatisch.",
            Icon = "🧙",
            Accent = "cyan",
            ModuleKey = "wizard",
            Rules =
            [
                "Jede Runde eine Karte mehr; vorher sagt jeder seine Stiche an.",
                "Ansage getroffen: 20 Punkte + 10 pro Stich.",
                "Daneben: −10 pro Stich Abweichung.",
                "Zauberer gewinnt immer, Narr verliert immer; Rundenanzahl = 60 / Spielerzahl.",
            ],
        },
        new GamePreset
        {
            Key = "skyjo",
            Name = "Skyjo",
            Description = "Kartenwerte minimieren – bei 100 Punkten ist Schluss, wenigste gewinnen.",
            Icon = "🦩",
            Accent = "lime",
            ModuleKey = "counter",
            ConfigJson = JsonSerializer.Serialize(new CounterConfig
            {
                StartScore = 0, Step = 1, TargetScore = 100, LowestWins = true,
                AllowNegative = true, UseRounds = true,
            }, JsonOpts),
            Rules =
            [
                "Rundenende: Alle decken auf und zählen ihre Kartenwerte (−2 bis 12) – hier eintragen.",
                "Wer die Runde beendet und NICHT die wenigsten Punkte hat, bekommt seine Punkte doppelt!",
                "Drei gleiche Karten in einer Spalte fliegen raus.",
                "Bei 100 Punkten endet das Spiel – wenigste Punkte gewinnen.",
            ],
        },
        new GamePreset
        {
            Key = "sechsnimmt",
            Name = "6 nimmt!",
            Description = "Hornochsen sammeln will keiner – bei 66 ist Schluss.",
            Icon = "🐂",
            Accent = "orange",
            ModuleKey = "counter",
            ConfigJson = JsonSerializer.Serialize(new CounterConfig
            {
                StartScore = 0, Step = 1, TargetScore = 66, LowestWins = true,
                AllowNegative = false, UseRounds = true,
            }, JsonOpts),
            Rules =
            [
                "Karten verdeckt wählen, aufsteigend anlegen – die 6. Karte einer Reihe kassiert die Reihe.",
                "Hornochsen zählen als Minuspunkte: normale Karte 1, 5er-Enden 2, Zehner 3, 55 = 7.",
                "Rundenende: Hornochsen hier eintragen – bei 66 Punkten endet das Spiel, wenigste gewinnen.",
            ],
        },
        new GamePreset
        {
            Key = "uno",
            Name = "Uno",
            Description = "Punkte der Verlierer-Handkarten kassieren – wer zuerst 500 hat, gewinnt.",
            Icon = "🌈",
            Accent = "magenta",
            ModuleKey = "counter",
            ConfigJson = JsonSerializer.Serialize(new CounterConfig
            {
                StartScore = 0, Step = 1, TargetScore = 500, LowestWins = false,
                AllowNegative = false, UseRounds = true,
            }, JsonOpts),
            Rules =
            [
                "Rundensieger bekommt die Handkarten-Punkte aller anderen (hier eintragen).",
                "Zahlenkarten = Wert, Aktionskarten 20, schwarze Karten 50.",
                "Wer zuerst 500 Punkte erreicht, gewinnt.",
            ],
        },
        new GamePreset
        {
            Key = "romme",
            Name = "Rommé",
            Description = "Restkarten zählen minus – wer 1000 reißt, beendet das Spiel.",
            Icon = "🎴",
            Accent = "cyan",
            ModuleKey = "counter",
            ConfigJson = JsonSerializer.Serialize(new CounterConfig
            {
                StartScore = 0, Step = 1, TargetScore = 1000, LowestWins = true,
                AllowNegative = false, UseRounds = true,
            }, JsonOpts),
            Rules =
            [
                "Erstauslage mindestens 40 Punkte, Joker zählt wie die ersetzte Karte.",
                "Rundenende: Restkarten auf der Hand als Minuspunkte eintragen (Bube-König 10, Ass 11, Joker 20).",
                "Klopfer/Hausregeln vorher klären – bei 1000 Punkten ist Schluss, wenigste gewinnen.",
            ],
        },
        new GamePreset
        {
            Key = "canasta",
            Name = "Canasta",
            Description = "Meldungen, Canastas und rote Dreier – erstes Team über 5000 gewinnt.",
            Icon = "🧺",
            Accent = "lime",
            ModuleKey = "counter",
            ConfigJson = JsonSerializer.Serialize(new CounterConfig
            {
                StartScore = 0, Step = 1, TargetScore = 5000, LowestWins = false,
                AllowNegative = true, UseRounds = true,
            }, JsonOpts),
            Rules =
            [
                "Pro Team einen Spieler anlegen und die Rundensummen eintragen.",
                "Echtes Canasta 500, unechtes 300, Ausmachen 100, roter Dreier 100 (alle vier: 800).",
                "Restkarten zählen minus – erstes Team über 5000 Punkte gewinnt.",
            ],
        },
        new GamePreset
        {
            Key = "doppelkopf",
            Name = "Doppelkopf",
            Description = "Spielwerte pro Runde notieren – auch ins Minus.",
            Icon = "🐷",
            Accent = "orange",
            ModuleKey = "counter",
            ConfigJson = JsonSerializer.Serialize(new CounterConfig
            {
                StartScore = 0, Step = 1, LowestWins = false,
                AllowNegative = true, UseRounds = true,
            }, JsonOpts),
            Rules =
            [
                "Gewinner der Runde bekommen den Spielwert plus, Verlierer minus (hier eintragen).",
                "Spielwert: 1 Grundpunkt + je 1 für Ansagen (Re/Kontra), keine 90/60/30, schwarz, Extras (Fuchs, Karlchen, Doppelkopf).",
                "Solo: Solist bekommt/zahlt den dreifachen Wert.",
            ],
        },
        new GamePreset
        {
            Key = "skat",
            Name = "Skat",
            Description = "Spielwerte klassisch anschreiben – verlorene Spiele doppelt minus.",
            Icon = "♠️",
            Accent = "cyan",
            ModuleKey = "counter",
            ConfigJson = JsonSerializer.Serialize(new CounterConfig
            {
                StartScore = 0, Step = 1, LowestWins = false,
                AllowNegative = true, UseRounds = true,
            }, JsonOpts),
            Rules =
            [
                "Nur der Alleinspieler schreibt: gewonnen = Spielwert plus, verloren = doppelter Spielwert minus.",
                "Spielwert = Grundwert (Karo 9, Herz 10, Pik 11, Kreuz 12, Grand 24) × Spitzen+1 (+ Hand/Schneider/Schwarz/Ouvert).",
                "Null 23, Null Hand 35, Null Ouvert 46, Null Ouvert Hand 59.",
            ],
        },
        new GamePreset
        {
            Key = "schocken",
            Name = "Schocken",
            Description = "Deckel zählen in der Kneipenrunde – wer am Ende alle hat, zahlt.",
            Icon = "🍺",
            Accent = "orange",
            ModuleKey = "counter",
            ConfigJson = JsonSerializer.Serialize(new CounterConfig
            {
                StartScore = 0, Step = 1, LowestWins = true, AllowNegative = false,
            }, JsonOpts),
            Rules =
            [
                "13 Deckel in der Mitte; der Rundenverlierer bekommt Deckel vom Gewinner-Wurf (hier hochzählen).",
                "Schock aus! (1-1-1) beendet die Halbzeit sofort – Verlierer bekommt alle Deckel.",
                "Wer eine Halbzeit verliert, spielt im Finale – der Finalverlierer gibt einen aus. 🍻",
            ],
        },
        new GamePreset
        {
            Key = "carcassonne",
            Name = "Carcassonne",
            Description = "Punkte für Straßen, Städte und Klöster direkt mitzählen.",
            Icon = "🏰",
            Accent = "lime",
            ModuleKey = "counter",
            ConfigJson = JsonSerializer.Serialize(new CounterConfig
            {
                StartScore = 0, Step = 1, LowestWins = false, AllowNegative = false,
            }, JsonOpts),
            Rules =
            [
                "Fertige Straße: 1 Punkt pro Plättchen; fertige Stadt: 2 pro Plättchen (+2 je Wappen).",
                "Fertiges Kloster: 9 Punkte.",
                "Schlusswertung: Unfertiges zählt 1 pro Plättchen/Wappen, Wiesen 3 Punkte pro versorgter Stadt.",
            ],
        },
        new GamePreset
        {
            Key = "darts501",
            Name = "Darts 501",
            Description = "Von 501 runter auf exakt 0 – geworfene Punkte einfach eintragen.",
            Icon = "🎯",
            Accent = "magenta",
            ModuleKey = "counter",
            ConfigJson = JsonSerializer.Serialize(new CounterConfig
            {
                StartScore = 501, Step = 1, TargetScore = 0, LowestWins = true,
                AllowNegative = false, UseRounds = true, SubtractRounds = true,
            }, JsonOpts),
            Rules =
            [
                "Pro Aufnahme (3 Darts) die geworfenen Punkte eintragen – sie werden automatisch abgezogen.",
                "Klassisch endet das Leg mit einem Doppel (Double-Out) auf exakt 0.",
                "Überworfen (Bust)? Aufnahme zählt nicht – einfach 0 eintragen.",
                "Für 301 einfach die Startpunkte im Formular anpassen.",
            ],
        },
    ];

    public static GameModule? GetModule(string key) => Modules.FirstOrDefault(m => m.Key == key);
    public static GamePreset? GetPreset(string key) => Presets.FirstOrDefault(p => p.Key == key);
}

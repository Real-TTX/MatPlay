using System.Text.Json;
using MatPlay.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace MatPlay.Pages.Games;

public class CreateModel(GameService games) : PageModel
{
    [BindProperty(SupportsGet = true)] public string? Preset { get; set; }

    [BindProperty] public string Name { get; set; } = "";
    [BindProperty] public string ModuleKey { get; set; } = "counter";
    [BindProperty] public List<string> Players { get; set; } = [];

    // Counter-Konfiguration
    [BindProperty] public int StartScore { get; set; }
    [BindProperty] public int Step { get; set; } = 1;
    [BindProperty] public bool HasTarget { get; set; }
    [BindProperty] public int TargetScore { get; set; }
    [BindProperty] public bool LowestWins { get; set; }
    [BindProperty] public bool AllowNegative { get; set; } = true;
    [BindProperty] public bool UseRounds { get; set; }

    // Qwixx-Konfiguration
    [BindProperty] public string QwixxVariant { get; set; } = "classic";

    // Munchkin-Konfiguration
    [BindProperty] public bool MunchkinTrackHealth { get; set; }

    public string? Error { get; private set; }

    public void OnGet()
    {
        var preset = Preset != null ? ModuleRegistry.GetPreset(Preset) : null;
        if (preset == null) return;

        ModuleKey = preset.ModuleKey;
        Name = preset.Name;
        if (preset.ModuleKey == "qwixx" && preset.ConfigJson.Contains("mixed"))
            QwixxVariant = preset.ConfigJson.Contains("mixedColors") ? "mixedColors" : "mixedNumbers";
        if (preset.ModuleKey == "munchkin")
        {
            var config = JsonSerializer.Deserialize<MunchkinConfig>(preset.ConfigJson, ModuleRegistry.JsonOpts)!;
            MunchkinTrackHealth = config.TrackHealth;
        }
        if (preset.ModuleKey == "counter")
        {
            var config = JsonSerializer.Deserialize<CounterConfig>(preset.ConfigJson, ModuleRegistry.JsonOpts)!;
            StartScore = config.StartScore;
            Step = config.Step;
            HasTarget = config.TargetScore != null;
            TargetScore = config.TargetScore ?? 0;
            LowestWins = config.LowestWins;
            AllowNegative = config.AllowNegative;
            UseRounds = config.UseRounds;
        }
    }

    public async Task<IActionResult> OnPostAsync()
    {
        var playerNames = Players.Where(p => !string.IsNullOrWhiteSpace(p)).ToList();
        if (string.IsNullOrWhiteSpace(Name))
        {
            Error = "Bitte gib dem Spiel einen Namen.";
            return Page();
        }
        if (playerNames.Count == 0)
        {
            Error = "Mindestens ein Spieler wird benötigt.";
            return Page();
        }
        if (ModuleRegistry.GetModule(ModuleKey) == null)
        {
            Error = "Unbekanntes Spielmodul.";
            return Page();
        }

        var configJson = ModuleKey switch
        {
            "counter" => JsonSerializer.Serialize(new CounterConfig
            {
                StartScore = StartScore,
                Step = Step,
                TargetScore = HasTarget ? TargetScore : null,
                LowestWins = LowestWins,
                AllowNegative = AllowNegative,
                UseRounds = UseRounds,
            }, ModuleRegistry.JsonOpts),
            "qwixx" => JsonSerializer.Serialize(QwixxPad.Generate(QwixxVariant), ModuleRegistry.JsonOpts),
            "munchkin" => JsonSerializer.Serialize(new MunchkinConfig { TrackHealth = MunchkinTrackHealth }, ModuleRegistry.JsonOpts),
            _ => "{}",
        };

        var game = await games.CreateAsync(Name.Trim(), ModuleKey, configJson, playerNames);
        return Redirect($"/play/{game.ShareToken}");
    }
}

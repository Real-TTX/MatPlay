using System.Text.Json;
using MatPlay.Data;
using MatPlay.Services;
using Microsoft.EntityFrameworkCore;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace MatPlay.Pages.Games;

public class CreateModel(GameService games, SavedPlayerService savedPlayers) : PageModel
{
    [BindProperty(SupportsGet = true)] public string? Preset { get; set; }
    /// <summary>Spiel-Id als Vorlage ("Nochmal spielen"): übernimmt Modul, Konfiguration und Spieler.</summary>
    [BindProperty(SupportsGet = true)] public long? From { get; set; }

    [BindProperty] public string Name { get; set; } = "";
    [BindProperty] public string ModuleKey { get; set; } = "counter";
    [BindProperty] public List<string> Players { get; set; } = [];
    [BindProperty] public bool SavePlayers { get; set; } = true;
    public List<SavedPlayer> SavedPlayerList { get; private set; } = [];

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

    public async Task OnGetAsync()
    {
        await LoadSavedPlayersAsync();

        // "Nochmal spielen": bestehendes Spiel als Vorlage
        if (From is long fromId)
        {
            var template = await games.GetByIdAsync(fromId);
            if (template != null && games.IsOwner(template))
            {
                Preset = template.PresetKey;
                ModuleKey = template.ModuleKey;
                Name = template.Name;
                Players = (await games.GetPlayersAsync(template.Id)).Select(p => p.Name).ToList();
                ApplyConfig(template.ModuleKey, template.ConfigJson);
                return;
            }
        }

        var preset = Preset != null ? ModuleRegistry.GetPreset(Preset) : null;
        if (preset == null) return;
        ModuleKey = preset.ModuleKey;
        Name = preset.Name;
        ApplyConfig(preset.ModuleKey, preset.ConfigJson);
    }

    private void ApplyConfig(string moduleKey, string configJson)
    {
        switch (moduleKey)
        {
            case "counter":
                var counter = JsonSerializer.Deserialize<CounterConfig>(configJson, ModuleRegistry.JsonOpts) ?? new CounterConfig();
                StartScore = counter.StartScore;
                Step = counter.Step;
                HasTarget = counter.TargetScore != null;
                TargetScore = counter.TargetScore ?? 0;
                LowestWins = counter.LowestWins;
                AllowNegative = counter.AllowNegative;
                UseRounds = counter.UseRounds;
                break;
            case "qwixx":
                var qwixx = JsonSerializer.Deserialize<QwixxConfig>(configJson, ModuleRegistry.JsonOpts);
                QwixxVariant = string.IsNullOrEmpty(qwixx?.Variant) ? "classic" : qwixx.Variant;
                break;
            case "munchkin":
                var munchkin = JsonSerializer.Deserialize<MunchkinConfig>(configJson, ModuleRegistry.JsonOpts) ?? new MunchkinConfig();
                MunchkinTrackHealth = munchkin.TrackHealth;
                break;
        }
    }

    public async Task<IActionResult> OnPostAsync()
    {
        await LoadSavedPlayersAsync();
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

        var presetKey = Preset != null && ModuleRegistry.GetPreset(Preset)?.ModuleKey == ModuleKey ? Preset : null;
        var game = await games.CreateAsync(Name.Trim(), ModuleKey, configJson, playerNames, SavePlayers, presetKey);
        return Redirect($"/play/{game.ShareToken}");
    }

    private async Task LoadSavedPlayersAsync() =>
        SavedPlayerList = await savedPlayers.QueryMine()
            .OrderByDescending(p => p.LastUsedDate)
            .Take(24)
            .ToListAsync();

    /// <summary>Alle Presets als JSON für die clientseitige Preset-Auswahl im Formular.</summary>
    public string PresetsJson => JsonSerializer.Serialize(
        ModuleRegistry.Presets.Select(p => new
        {
            key = p.Key,
            name = p.Name,
            icon = p.Icon,
            moduleKey = p.ModuleKey,
            config = JsonSerializer.Deserialize<JsonElement>(p.ConfigJson),
        }), ModuleRegistry.JsonOpts);
}

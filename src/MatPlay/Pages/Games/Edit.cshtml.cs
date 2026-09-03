using System.Text.Json;
using MatPlay.Data;
using MatPlay.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;

namespace MatPlay.Pages.Games;

public class EditModel(GameService games, AppDbContext db) : PageModel
{
    [BindProperty(SupportsGet = true)] public long Id { get; set; }

    [BindProperty] public string Name { get; set; } = "";
    [BindProperty] public int Status { get; set; }
    [BindProperty] public List<long> PlayerIds { get; set; } = [];
    [BindProperty] public List<string> PlayerNames { get; set; } = [];

    // Counter-Konfiguration
    [BindProperty] public int StartScore { get; set; }
    [BindProperty] public int Step { get; set; } = 1;
    [BindProperty] public bool HasTarget { get; set; }
    [BindProperty] public int TargetScore { get; set; }
    [BindProperty] public bool LowestWins { get; set; }
    [BindProperty] public bool AllowNegative { get; set; }
    [BindProperty] public bool UseRounds { get; set; }

    public Game? Game { get; private set; }
    public List<GamePlayer> Players { get; private set; } = [];
    public string? Error { get; private set; }

    public async Task<IActionResult> OnGetAsync()
    {
        if (!await LoadAsync()) return NotFound();

        Name = Game!.Name;
        Status = Game.Status;
        if (Game.ModuleKey == "counter")
        {
            var config = JsonSerializer.Deserialize<CounterConfig>(Game.ConfigJson, ModuleRegistry.JsonOpts) ?? new CounterConfig();
            StartScore = config.StartScore;
            Step = config.Step;
            HasTarget = config.TargetScore != null;
            TargetScore = config.TargetScore ?? 0;
            LowestWins = config.LowestWins;
            AllowNegative = config.AllowNegative;
            UseRounds = config.UseRounds;
        }
        return Page();
    }

    public async Task<IActionResult> OnPostAsync()
    {
        if (!await LoadAsync()) return NotFound();
        if (string.IsNullOrWhiteSpace(Name))
        {
            Error = "Der Name darf nicht leer sein.";
            return Page();
        }

        var game = Game!;
        game.Name = Name.Trim();
        game.Status = Status;

        if (game.ModuleKey == "counter")
        {
            game.ConfigJson = JsonSerializer.Serialize(new CounterConfig
            {
                StartScore = StartScore,
                Step = Step,
                TargetScore = HasTarget ? TargetScore : null,
                LowestWins = LowestWins,
                AllowNegative = AllowNegative,
                UseRounds = UseRounds,
            }, ModuleRegistry.JsonOpts);
        }

        // Spieler umbenennen bzw. leere Namen = Spieler entfernen (Soft-Delete)
        for (var i = 0; i < PlayerIds.Count && i < PlayerNames.Count; i++)
        {
            var player = Players.FirstOrDefault(p => p.Id == PlayerIds[i]);
            if (player == null) continue;
            if (string.IsNullOrWhiteSpace(PlayerNames[i]))
            {
                player.UpdateState = UpdateStates.Deleted;
            }
            else if (player.Name != PlayerNames[i].Trim())
            {
                player.Name = PlayerNames[i].Trim();
                player.UpdateState = UpdateStates.Updated;
            }
            player.UpdateDate = DateTime.UtcNow;
        }

        game.Version++;
        game.UpdateDate = DateTime.UtcNow;
        game.UpdateState = UpdateStates.Updated;
        await db.SaveChangesAsync();
        return Redirect("/games");
    }

    public async Task<IActionResult> OnPostDeleteAsync()
    {
        if (!await LoadAsync()) return NotFound();
        await games.SoftDeleteAsync(Game!);
        return Redirect("/games");
    }

    private async Task<bool> LoadAsync()
    {
        Game = await games.GetByIdAsync(Id);
        if (Game == null || !games.IsOwner(Game)) return false;
        Players = await games.GetPlayersAsync(Game.Id);
        return true;
    }
}

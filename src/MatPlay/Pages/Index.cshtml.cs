using MatPlay.Data;
using MatPlay.Services;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;

namespace MatPlay.Pages;

/// <summary>Dashboard: aktive Spiele zuerst, darunter die zuletzt gespielten Spielarten.</summary>
public class IndexModel(GameService games) : PageModel
{
    public List<Game> ActiveGames { get; private set; } = [];
    public List<GamePreset> TopPresets { get; private set; } = [];
    public bool HasAnyGames { get; private set; }

    public async Task OnGetAsync()
    {
        ActiveGames = await games.QueryMyGames()
            .Where(g => g.Status == GameStatus.Running)
            .OrderByDescending(g => g.UpdateDate)
            .Take(8)
            .ToListAsync();

        var recentKeys = await games.QueryMyGames()
            .OrderByDescending(g => g.UpdateDate)
            .Select(g => g.PresetKey ?? g.ModuleKey)
            .Take(50)
            .ToListAsync();
        HasAnyGames = recentKeys.Count > 0;

        TopPresets = recentKeys.Distinct()
            .Select(ModuleRegistry.GetPreset)
            .Where(p => p != null)
            .Cast<GamePreset>()
            .Take(3)
            .ToList();

        // Mit Standard-Presets auffüllen, falls noch keine 3 Spielarten gespielt wurden
        foreach (var preset in ModuleRegistry.Presets)
        {
            if (TopPresets.Count >= 3) break;
            if (!TopPresets.Contains(preset)) TopPresets.Add(preset);
        }
    }
}

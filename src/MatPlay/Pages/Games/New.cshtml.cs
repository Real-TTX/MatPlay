using MatPlay.Data;
using MatPlay.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;

namespace MatPlay.Pages.Games;

/// <summary>Spielarten-Katalog: Einstieg zum Starten eines neuen Spiels.</summary>
public class NewModel(AppDbContext db, CurrentContext current) : PageModel
{
    public const int PageSize = 8;

    [BindProperty(SupportsGet = true)] public string? Q { get; set; }
    [BindProperty(SupportsGet = true)] public string? Fav { get; set; }
    [BindProperty(SupportsGet = true)] public int P { get; set; } = 1;

    public List<GamePreset> Presets { get; private set; } = [];
    public HashSet<string> FavoriteKeys { get; private set; } = [];
    public int TotalPages { get; private set; } = 1;
    public bool CanFavorite => current.IsAuthenticated;

    public async Task OnGetAsync()
    {
        await LoadFavoritesAsync();

        var filtered = ModuleRegistry.Presets.AsEnumerable();
        if (!string.IsNullOrWhiteSpace(Q))
            filtered = filtered.Where(p =>
                p.Name.Contains(Q, StringComparison.OrdinalIgnoreCase) ||
                p.Description.Contains(Q, StringComparison.OrdinalIgnoreCase));
        if (Fav == "1")
            filtered = filtered.Where(p => FavoriteKeys.Contains(p.Key));

        // Favoriten zuerst, Rest in Registry-Reihenfolge
        var list = filtered
            .OrderByDescending(p => FavoriteKeys.Contains(p.Key))
            .ToList();

        TotalPages = Math.Max(1, (int)Math.Ceiling(list.Count / (double)PageSize));
        P = Math.Clamp(P, 1, TotalPages);
        Presets = list.Skip((P - 1) * PageSize).Take(PageSize).ToList();
    }

    public async Task<IActionResult> OnPostToggleFavoriteAsync(string presetKey)
    {
        if (!current.IsAuthenticated)
            return Redirect("/account/login?returnUrl=%2Fgames%2Fnew");
        if (ModuleRegistry.GetPreset(presetKey) == null)
            return RedirectToPage(new { Q, Fav, P });

        var existing = await db.UserFavorites.FirstOrDefaultAsync(f =>
            f.UserId == current.UserId && f.PresetKey == presetKey);

        if (existing == null)
        {
            db.UserFavorites.Add(new UserFavorite
            {
                UserId = current.UserId!.Value,
                PresetKey = presetKey,
                CreateUserId = current.UserId,
                UpdateUserId = current.UserId,
            });
        }
        else if (existing.UpdateState == UpdateStates.Deleted)
        {
            existing.UpdateState = UpdateStates.Updated;
            existing.UpdateDate = DateTime.UtcNow;
            existing.UpdateUserId = current.UserId;
        }
        else
        {
            existing.UpdateState = UpdateStates.Deleted;
            existing.UpdateDate = DateTime.UtcNow;
            existing.UpdateUserId = current.UserId;
        }
        await db.SaveChangesAsync();
        return RedirectToPage(new { Q, Fav, P });
    }

    private async Task LoadFavoritesAsync()
    {
        if (!current.IsAuthenticated) return;
        FavoriteKeys = (await db.UserFavorites
                .Where(f => f.UserId == current.UserId && f.UpdateState != UpdateStates.Deleted)
                .Select(f => f.PresetKey)
                .ToListAsync())
            .ToHashSet();
    }

    public string UrlForPage(int page)
    {
        var parts = new List<string> { $"p={page}" };
        if (!string.IsNullOrWhiteSpace(Q)) parts.Add($"q={Uri.EscapeDataString(Q)}");
        if (Fav == "1") parts.Add("fav=1");
        return "/games/new?" + string.Join("&", parts);
    }
}

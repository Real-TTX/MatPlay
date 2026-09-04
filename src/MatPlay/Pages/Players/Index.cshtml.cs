using MatPlay.Data;
using MatPlay.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;

namespace MatPlay.Pages.Players;

public class IndexModel(SavedPlayerService players, AppDbContext db) : PageModel
{
    public const int PageSize = 10;

    [BindProperty(SupportsGet = true)] public string? Q { get; set; }
    [BindProperty(SupportsGet = true)] public string? Sort { get; set; }
    [BindProperty(SupportsGet = true)] public int P { get; set; } = 1;

    public List<SavedPlayer> Items { get; private set; } = [];
    public Dictionary<long, int> GameCounts { get; private set; } = [];
    public int TotalPages { get; private set; } = 1;

    public async Task OnGetAsync()
    {
        var query = BuildQuery();
        var total = await query.CountAsync();
        TotalPages = Math.Max(1, (int)Math.Ceiling(total / (double)PageSize));
        P = Math.Clamp(P, 1, TotalPages);
        Items = await query.Skip((P - 1) * PageSize).Take(PageSize).ToListAsync();

        var ids = Items.Select(i => i.Id).ToList();
        GameCounts = await db.GamePlayers
            .Where(gp => gp.SavedPlayerId != null && ids.Contains(gp.SavedPlayerId.Value) &&
                         gp.UpdateState != UpdateStates.Deleted)
            .GroupBy(gp => gp.SavedPlayerId!.Value)
            .Select(g => new { g.Key, Count = g.Count() })
            .ToDictionaryAsync(x => x.Key, x => x.Count);
    }

    public async Task<IActionResult> OnPostDeleteAsync(long[] selected)
    {
        foreach (var id in selected)
        {
            var player = await db.SavedPlayers.FirstOrDefaultAsync(p => p.Id == id);
            if (player != null && players.IsOwner(player))
                await players.SoftDeleteAsync(player);
        }
        return RedirectToPage(new { Q, Sort, P });
    }

    private IQueryable<SavedPlayer> BuildQuery()
    {
        var query = players.QueryMine();
        if (!string.IsNullOrWhiteSpace(Q))
            query = query.Where(p => EF.Functions.Like(p.Name, $"%{Q}%"));
        return Sort switch
        {
            "name" => query.OrderBy(p => p.Name),
            _ => query.OrderByDescending(p => p.LastUsedDate),
        };
    }

    public string UrlForPage(int page)
    {
        var parts = new List<string> { $"p={page}" };
        if (!string.IsNullOrWhiteSpace(Q)) parts.Add($"q={Uri.EscapeDataString(Q)}");
        if (!string.IsNullOrEmpty(Sort)) parts.Add($"sort={Sort}");
        return "/players?" + string.Join("&", parts);
    }
}

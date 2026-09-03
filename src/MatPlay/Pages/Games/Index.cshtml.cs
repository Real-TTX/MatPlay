using MatPlay.Data;
using MatPlay.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;

namespace MatPlay.Pages.Games;

public class IndexModel(GameService games) : PageModel
{
    public const int PageSize = 10;

    [BindProperty(SupportsGet = true)] public string? Q { get; set; }
    [BindProperty(SupportsGet = true)] public string? Module { get; set; }
    [BindProperty(SupportsGet = true)] public string? Status { get; set; }
    [BindProperty(SupportsGet = true)] public string? Sort { get; set; }
    [BindProperty(SupportsGet = true)] public int P { get; set; } = 1;

    public List<Game> Items { get; private set; } = [];
    public int TotalPages { get; private set; } = 1;

    public async Task OnGetAsync()
    {
        var query = BuildQuery();
        var total = await query.CountAsync();
        TotalPages = Math.Max(1, (int)Math.Ceiling(total / (double)PageSize));
        P = Math.Clamp(P, 1, TotalPages);
        Items = await query.Skip((P - 1) * PageSize).Take(PageSize).ToListAsync();
    }

    public async Task<IActionResult> OnPostDeleteAsync(long[] selected)
    {
        foreach (var id in selected)
        {
            var game = await games.GetByIdAsync(id);
            if (game != null && games.IsOwner(game))
                await games.SoftDeleteAsync(game);
        }
        return RedirectToPage(new { Q, Module, Status, Sort, P });
    }

    private IQueryable<Game> BuildQuery()
    {
        var query = games.QueryMyGames();
        if (!string.IsNullOrWhiteSpace(Q))
            query = query.Where(g => EF.Functions.Like(g.Name, $"%{Q}%"));
        if (!string.IsNullOrEmpty(Module))
            query = query.Where(g => g.ModuleKey == Module);
        if (!string.IsNullOrEmpty(Status) && int.TryParse(Status, out var status))
            query = query.Where(g => g.Status == status);

        return Sort switch
        {
            "name" => query.OrderBy(g => g.Name),
            "created" => query.OrderByDescending(g => g.CreateDate),
            _ => query.OrderByDescending(g => g.UpdateDate),
        };
    }

    public string UrlForPage(int page)
    {
        var parts = new List<string> { $"p={page}" };
        if (!string.IsNullOrWhiteSpace(Q)) parts.Add($"q={Uri.EscapeDataString(Q)}");
        if (!string.IsNullOrEmpty(Module)) parts.Add($"module={Module}");
        if (!string.IsNullOrEmpty(Status)) parts.Add($"status={Status}");
        if (!string.IsNullOrEmpty(Sort)) parts.Add($"sort={Sort}");
        return "/games?" + string.Join("&", parts);
    }
}

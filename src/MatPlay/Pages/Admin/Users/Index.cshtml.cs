using MatPlay.Data;
using MatPlay.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;

namespace MatPlay.Pages.Admin.Users;

public class IndexModel(AppDbContext db, CurrentContext current) : PageModel
{
    public const int PageSize = 10;

    [BindProperty(SupportsGet = true)] public string? Q { get; set; }
    [BindProperty(SupportsGet = true)] public string? Role { get; set; }
    [BindProperty(SupportsGet = true)] public string? Sort { get; set; }
    [BindProperty(SupportsGet = true)] public int P { get; set; } = 1;

    public List<User> Items { get; private set; } = [];
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
            if (id == current.UserId) continue; // sich selbst nicht löschen
            var user = await db.Users.FirstOrDefaultAsync(u => u.Id == id);
            if (user == null) continue;
            user.UpdateState = UpdateStates.Deleted;
            user.UpdateDate = DateTime.UtcNow;
            user.UpdateUserId = current.UserId;
        }
        await db.SaveChangesAsync();
        return RedirectToPage(new { Q, Role, Sort, P });
    }

    private IQueryable<User> BuildQuery()
    {
        var query = db.Users.Where(u => u.UpdateState != UpdateStates.Deleted);
        if (!string.IsNullOrWhiteSpace(Q))
            query = query.Where(u => EF.Functions.Like(u.Username, $"%{Q}%") || EF.Functions.Like(u.DisplayName, $"%{Q}%"));
        if (!string.IsNullOrEmpty(Role))
            query = query.Where(u => u.Role == Role);

        return Sort switch
        {
            "created" => query.OrderByDescending(u => u.CreateDate),
            _ => query.OrderBy(u => u.Username),
        };
    }

    public string UrlForPage(int page)
    {
        var parts = new List<string> { $"p={page}" };
        if (!string.IsNullOrWhiteSpace(Q)) parts.Add($"q={Uri.EscapeDataString(Q)}");
        if (!string.IsNullOrEmpty(Role)) parts.Add($"role={Role}");
        if (!string.IsNullOrEmpty(Sort)) parts.Add($"sort={Sort}");
        return "/admin/users?" + string.Join("&", parts);
    }
}

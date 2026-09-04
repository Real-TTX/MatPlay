using MatPlay.Data;
using MatPlay.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;

namespace MatPlay.Pages.Players;

public class EditModel(SavedPlayerService players, AppDbContext db) : PageModel
{
    [BindProperty(SupportsGet = true)] public long Id { get; set; }
    [BindProperty] public string Name { get; set; } = "";

    public SavedPlayer? Target { get; private set; }
    public List<Game> Games { get; private set; } = [];
    public string? Error { get; private set; }

    public async Task<IActionResult> OnGetAsync()
    {
        if (!await LoadAsync()) return NotFound();
        Name = Target!.Name;
        return Page();
    }

    public async Task<IActionResult> OnPostAsync()
    {
        if (!await LoadAsync()) return NotFound();
        Name = Name.Trim();
        if (Name.Length == 0)
        {
            Error = "Der Name darf nicht leer sein.";
            return Page();
        }
        var existing = await players.FindMineByNameAsync(Name);
        if (existing != null && existing.Id != Target!.Id)
        {
            Error = "Diesen Spieler gibt es schon.";
            return Page();
        }
        Target!.Name = Name;
        Target.UpdateDate = DateTime.UtcNow;
        Target.UpdateState = UpdateStates.Updated;
        await db.SaveChangesAsync();
        return Redirect("/players");
    }

    public async Task<IActionResult> OnPostDeleteAsync()
    {
        if (!await LoadAsync()) return NotFound();
        await players.SoftDeleteAsync(Target!);
        return Redirect("/players");
    }

    private async Task<bool> LoadAsync()
    {
        Target = await db.SavedPlayers.FirstOrDefaultAsync(p =>
            p.Id == Id && p.UpdateState != UpdateStates.Deleted);
        if (Target == null || !players.IsOwner(Target)) return false;
        Games = await players.GetGamesAsync(Target.Id);
        return true;
    }
}

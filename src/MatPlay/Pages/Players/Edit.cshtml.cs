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
    [BindProperty] public string Code { get; set; } = "";
    [BindProperty] public string Color { get; set; } = "#00e5ff";

    public SavedPlayer? Target { get; private set; }
    public List<Game> Games { get; private set; } = [];
    public string? Error { get; private set; }

    public async Task<IActionResult> OnGetAsync()
    {
        if (!await LoadAsync()) return NotFound();
        Name = Target!.Name;
        Code = Target.Code;
        Color = Target.Color;
        return Page();
    }

    public async Task<IActionResult> OnPostAsync()
    {
        if (!await LoadAsync()) return NotFound();
        Name = Name.Trim();
        Code = Code.Trim();
        if (Name.Length == 0) Error = "Der Name darf nicht leer sein.";
        else if (Code.Length is < 1 or > 4) Error = "Das Kürzel braucht 1 bis 4 Zeichen.";
        else if (!System.Text.RegularExpressions.Regex.IsMatch(Color, "^#[0-9a-fA-F]{6}$"))
            Error = "Ungültige Farbe.";

        if (Error == null)
        {
            var existing = await players.FindMineByNameAsync(Name);
            if (existing != null && existing.Id != Target!.Id)
                Error = "Diesen Spieler gibt es schon.";
            else if (await players.QueryMine().AnyAsync(p =>
                         p.Id != Target!.Id && p.Code.ToLower() == Code.ToLower()))
                Error = "Dieses Kürzel ist schon vergeben.";
        }
        if (Error != null) return Page();

        Target!.Name = Name;
        Target.Code = Code;
        Target.Color = Color.ToLowerInvariant();
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

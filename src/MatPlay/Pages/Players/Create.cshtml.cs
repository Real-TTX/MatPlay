using MatPlay.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace MatPlay.Pages.Players;

public class CreateModel(SavedPlayerService players) : PageModel
{
    [BindProperty] public string Name { get; set; } = "";

    public string? Error { get; private set; }

    public void OnGet() { }

    public async Task<IActionResult> OnPostAsync()
    {
        Name = Name.Trim();
        if (Name.Length == 0)
        {
            Error = "Bitte gib einen Namen ein.";
            return Page();
        }
        if (await players.FindMineByNameAsync(Name) != null)
        {
            Error = "Diesen Spieler gibt es schon.";
            return Page();
        }
        await players.CreateAsync(Name);
        return Redirect("/players");
    }
}

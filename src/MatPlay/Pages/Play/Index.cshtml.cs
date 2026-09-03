using MatPlay.Data;
using MatPlay.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace MatPlay.Pages.Play;

public class IndexModel(GameService games) : PageModel
{
    [BindProperty(SupportsGet = true)] public string Token { get; set; } = "";

    public Game? Game { get; private set; }
    public GameModule? Module { get; private set; }
    public bool IsOwner { get; private set; }

    public async Task<IActionResult> OnGetAsync()
    {
        Game = await games.GetByTokenAsync(Token);
        if (Game == null) return NotFound();
        Module = ModuleRegistry.GetModule(Game.ModuleKey);
        if (Module == null) return NotFound();
        IsOwner = games.IsOwner(Game);
        return Page();
    }
}

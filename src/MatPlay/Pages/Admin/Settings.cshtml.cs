using MatPlay.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace MatPlay.Pages.Admin;

public class SettingsModel(AppConfigService config) : PageModel
{
    [BindProperty] public bool AllowRegistration { get; set; }
    [BindProperty] public int PlayerCodeLength { get; set; } = 2;

    public bool Saved { get; private set; }

    public void OnGet()
    {
        AllowRegistration = config.Config.AllowRegistration;
        PlayerCodeLength = config.Config.PlayerCodeLength;
    }

    public IActionResult OnPost()
    {
        PlayerCodeLength = Math.Clamp(PlayerCodeLength, 1, 4);
        var next = config.Config;
        next.AllowRegistration = AllowRegistration;
        next.PlayerCodeLength = PlayerCodeLength;
        config.Save(next);
        Saved = true;
        return Page();
    }
}

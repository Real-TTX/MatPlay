using MatPlay.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace MatPlay.Pages.Account;

public class LoginModel(AuthService auth, AppConfigService config) : PageModel
{
    [BindProperty] public string Username { get; set; } = "";
    [BindProperty] public string Password { get; set; } = "";
    [BindProperty(SupportsGet = true)] public string? ReturnUrl { get; set; }

    public bool AllowRegistration => config.Config.AllowRegistration;
    public string? Error { get; private set; }

    public void OnGet() { }

    public async Task<IActionResult> OnPostAsync()
    {
        var user = await auth.LoginAsync(Username.Trim(), Password);
        if (user == null)
        {
            Error = "Benutzername oder Passwort ist falsch.";
            return Page();
        }
        return LocalRedirect(string.IsNullOrEmpty(ReturnUrl) || !Url.IsLocalUrl(ReturnUrl) ? "/" : ReturnUrl);
    }
}

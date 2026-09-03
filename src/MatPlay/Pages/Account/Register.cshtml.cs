using MatPlay.Data;
using MatPlay.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;

namespace MatPlay.Pages.Account;

public class RegisterModel(AppDbContext db, AuthService auth, AppConfigService config) : PageModel
{
    [BindProperty] public string Username { get; set; } = "";
    [BindProperty] public string DisplayName { get; set; } = "";
    [BindProperty] public string Password { get; set; } = "";
    [BindProperty] public string PasswordRepeat { get; set; } = "";

    public string? Error { get; private set; }

    public IActionResult OnGet() => config.Config.AllowRegistration ? Page() : NotFound();

    public async Task<IActionResult> OnPostAsync()
    {
        if (!config.Config.AllowRegistration) return NotFound();

        Username = Username.Trim();
        DisplayName = DisplayName.Trim();
        if (Username.Length < 3) Error = "Der Benutzername braucht mindestens 3 Zeichen.";
        else if (Password.Length < 6) Error = "Das Passwort braucht mindestens 6 Zeichen.";
        else if (Password != PasswordRepeat) Error = "Die Passwörter stimmen nicht überein.";
        else if (await db.Users.AnyAsync(u => u.Username == Username && u.UpdateState != UpdateStates.Deleted))
            Error = "Dieser Benutzername ist schon vergeben.";

        if (Error != null) return Page();

        db.Users.Add(new User
        {
            Username = Username,
            DisplayName = string.IsNullOrWhiteSpace(DisplayName) ? Username : DisplayName,
            PasswordHash = PasswordHasher.Hash(Password),
            Role = Roles.User,
        });
        await db.SaveChangesAsync();

        await auth.LoginAsync(Username, Password);
        return LocalRedirect("/");
    }
}

using MatPlay.Data;
using MatPlay.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace MatPlay.Pages.Account;

public class ChangePasswordModel(AppDbContext db, CurrentContext current) : PageModel
{
    [BindProperty] public string NewPassword { get; set; } = "";
    [BindProperty] public string PasswordRepeat { get; set; } = "";

    public bool Forced => current.User?.MustChangePassword == true;
    public string? Error { get; private set; }

    public IActionResult OnGet() =>
        current.IsAuthenticated ? Page() : Redirect("/account/login?returnUrl=%2Faccount%2Fchange-password");

    public async Task<IActionResult> OnPostAsync()
    {
        if (!current.IsAuthenticated)
            return Redirect("/account/login");

        var user = current.User!;
        if (NewPassword.Length < 6) Error = "Das Passwort braucht mindestens 6 Zeichen.";
        else if (NewPassword != PasswordRepeat) Error = "Die Passwörter stimmen nicht überein.";
        else if (PasswordHasher.Verify(NewPassword, user.PasswordHash))
            Error = "Das neue Passwort darf nicht das alte sein.";

        if (Error != null) return Page();

        user.PasswordHash = PasswordHasher.Hash(NewPassword);
        user.MustChangePassword = false;
        user.UpdateDate = DateTime.UtcNow;
        user.UpdateUserId = user.Id;
        user.UpdateState = UpdateStates.Updated;
        await db.SaveChangesAsync();
        return Redirect("/");
    }
}

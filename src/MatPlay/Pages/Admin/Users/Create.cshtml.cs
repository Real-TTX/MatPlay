using MatPlay.Data;
using MatPlay.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;

namespace MatPlay.Pages.Admin.Users;

public class CreateModel(AppDbContext db, CurrentContext current) : PageModel
{
    [BindProperty] public string Username { get; set; } = "";
    [BindProperty] public string DisplayName { get; set; } = "";
    [BindProperty] public string Password { get; set; } = "";
    [BindProperty] public string Role { get; set; } = Roles.User;

    public string? Error { get; private set; }

    public void OnGet() { }

    public async Task<IActionResult> OnPostAsync()
    {
        Username = Username.Trim();
        if (Username.Length < 3) Error = "Der Benutzername braucht mindestens 3 Zeichen.";
        else if (Password.Length < 6) Error = "Das Passwort braucht mindestens 6 Zeichen.";
        else if (Role != Roles.Admin && Role != Roles.User) Error = "Ungültige Rolle.";
        else if (await db.Users.AnyAsync(u => u.Username == Username && u.UpdateState != UpdateStates.Deleted))
            Error = "Dieser Benutzername ist schon vergeben.";

        if (Error != null) return Page();

        db.Users.Add(new User
        {
            Username = Username,
            DisplayName = string.IsNullOrWhiteSpace(DisplayName) ? Username : DisplayName.Trim(),
            PasswordHash = PasswordHasher.Hash(Password),
            Role = Role,
            CreateUserId = current.UserId,
            UpdateUserId = current.UserId,
        });
        await db.SaveChangesAsync();
        return Redirect("/admin/users");
    }
}

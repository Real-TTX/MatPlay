using MatPlay.Data;
using MatPlay.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;

namespace MatPlay.Pages.Admin.Users;

public class EditModel(AppDbContext db, CurrentContext current) : PageModel
{
    [BindProperty(SupportsGet = true)] public long Id { get; set; }

    [BindProperty] public string DisplayName { get; set; } = "";
    [BindProperty] public string Role { get; set; } = Roles.User;
    [BindProperty] public string? NewPassword { get; set; }

    public User? Target { get; private set; }
    public string? Error { get; private set; }

    public async Task<IActionResult> OnGetAsync()
    {
        if (!await LoadAsync()) return NotFound();
        DisplayName = Target!.DisplayName;
        Role = Target.Role;
        return Page();
    }

    public async Task<IActionResult> OnPostAsync()
    {
        if (!await LoadAsync()) return NotFound();
        if (Role != Roles.Admin && Role != Roles.User)
        {
            Error = "Ungültige Rolle.";
            return Page();
        }
        if (Target!.Id == current.UserId && Role != Roles.Admin)
        {
            Error = "Du kannst dir nicht selbst die Admin-Rolle entziehen.";
            return Page();
        }
        if (!string.IsNullOrEmpty(NewPassword) && NewPassword.Length < 6)
        {
            Error = "Das neue Passwort braucht mindestens 6 Zeichen.";
            return Page();
        }

        Target.DisplayName = DisplayName.Trim();
        Target.Role = Role;
        if (!string.IsNullOrEmpty(NewPassword))
            Target.PasswordHash = PasswordHasher.Hash(NewPassword);
        Target.UpdateDate = DateTime.UtcNow;
        Target.UpdateUserId = current.UserId;
        Target.UpdateState = UpdateStates.Updated;
        await db.SaveChangesAsync();
        return Redirect("/admin/users");
    }

    public async Task<IActionResult> OnPostDeleteAsync()
    {
        if (!await LoadAsync()) return NotFound();
        if (Target!.Id == current.UserId)
        {
            Error = "Du kannst dich nicht selbst löschen.";
            return Page();
        }
        Target.UpdateState = UpdateStates.Deleted;
        Target.UpdateDate = DateTime.UtcNow;
        Target.UpdateUserId = current.UserId;
        await db.SaveChangesAsync();
        return Redirect("/admin/users");
    }

    private async Task<bool> LoadAsync()
    {
        Target = await db.Users.FirstOrDefaultAsync(u => u.Id == Id && u.UpdateState != UpdateStates.Deleted);
        return Target != null;
    }
}

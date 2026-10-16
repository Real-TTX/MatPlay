using MatPlatform;
using MatPlay.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace MatPlay.Pages.Account;

/// <summary>Abmelden (nur per POST). Nach einer Anmeldung über MatIdentity endet auch die Sitzung dort.</summary>
public class LogoutModel(AuthService auth, MatPlatformSettings platform) : PageModel
{
    public IActionResult OnGet() => Redirect("/");

    public async Task<IActionResult> OnPostAsync()
    {
        // Vor der lokalen Abmeldung lesen: nur eine Anmeldung über MatIdentity hat ein ID-Token.
        var identityToken = platform.IdentityEnabled ? auth.IdentityToken : null;

        await auth.LogoutAsync();

        if (!string.IsNullOrEmpty(identityToken))
        {
            // Dann auch bei MatIdentity; die schickt den Browser über /signout-callback-oidc zurück zu /account/login?signedOut=1.
            return MatIdentitySignIn.SignOutAtIdentity(HttpContext, identityToken);
        }

        return Redirect("/");
    }
}

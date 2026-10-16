using MatPlatform;
using MatPlay.Services;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace MatPlay.Pages.Account;

/// <summary>
/// Lokale Anmeldung mit Benutzername und Passwort. Mit MatIdentity (MATIDENTITY_AUTHORITY) geht ein Aufruf direkt weiter zu MatIdentity;
/// das lokale Formular bleibt mit <c>?local=1</c> erreichbar (der Weg für den Admin, wenn MatIdentity nicht läuft).
/// <c>?signedOut=1</c> (zurück von der Abmeldung) und <c>?ssoError=1</c> (Anmeldung dort fehlgeschlagen) leiten nicht automatisch weiter.
/// </summary>
public class LoginModel(AuthService auth, AppConfigService config, CurrentContext current, MatPlatformSettings platform, ILogger<LoginModel> logger) : PageModel
{
    [BindProperty] public string Username { get; set; } = "";
    [BindProperty] public string Password { get; set; } = "";
    [BindProperty(SupportsGet = true)] public string? ReturnUrl { get; set; }

    public bool AllowRegistration => config.Config.AllowRegistration;
    public string? Error { get; private set; }

    /// <summary><c>?local=1</c>: das lokale Formular, auch wenn MatIdentity eingerichtet ist.</summary>
    public bool Local { get; private set; }

    /// <summary><c>?signedOut=1</c>: zurück von der Abmeldung bei MatIdentity, keine automatische Anmeldung.</summary>
    public bool SignedOut { get; private set; }

    /// <summary><c>?ssoError=1</c>: die Anmeldung bei MatIdentity ist fehlgeschlagen oder MatIdentity ist nicht erreichbar.</summary>
    public bool SsoError { get; private set; }

    /// <summary>True, wenn die Anmeldung über MatIdentity eingerichtet ist.</summary>
    public bool IdentityEnabled => platform.IdentityEnabled;

    /// <summary>True, wenn die Seite das lokale Formular zeigt (ohne MatIdentity immer).</summary>
    public bool ShowLocalForm => !IdentityEnabled || Local;

    /// <summary>Startet die Anmeldung bei MatIdentity und kommt zu <see cref="ReturnUrl"/> zurück.</summary>
    public string IdentitySignInUrl => WithReturnUrl("/account/login");

    /// <summary>Das lokale Formular mit derselben <see cref="ReturnUrl"/>.</summary>
    public string LocalSignInUrl => WithReturnUrl("/account/login?local=1");

    public async Task<IActionResult> OnGetAsync()
    {
        ReadFlags();

        // Automatische Anmeldung: direkt weiter zu MatIdentity, kein lokales Formular im Weg.
        if (IdentityEnabled && !Local && !SignedOut && !SsoError)
        {
            if (current.IsAuthenticated)
                return LocalRedirect(ResolveReturnUrl());

            try
            {
                await HttpContext.ChallengeAsync(MatIdentitySignIn.Scheme, new AuthenticationProperties { RedirectUri = ResolveReturnUrl() });
                return new EmptyResult();
            }
            catch (InvalidOperationException ex)
            {
                // MatIdentity ist nicht erreichbar (Konfiguration nicht lesbar): das lokale Formular ist der Weg hinein.
                logger.LogWarning(ex, "MatIdentity ist nicht erreichbar, zeige die lokale Anmeldung");
                Local = true;
                SsoError = true;
            }
        }

        return Page();
    }

    public async Task<IActionResult> OnPostAsync()
    {
        // Ein abgeschicktes Formular ist die lokale Anmeldung; bei einem Fehler wieder das Formular.
        ReadFlags();
        Local = true;

        var user = await auth.LoginAsync(Username.Trim(), Password);
        if (user == null)
        {
            Error = "Benutzername oder Passwort ist falsch.";
            return Page();
        }
        return LocalRedirect(ResolveReturnUrl());
    }

    private void ReadFlags()
    {
        Local = QueryFlag("local");
        SignedOut = QueryFlag("signedOut");
        SsoError = QueryFlag("ssoError");
    }

    /// <summary>Ein Schalter in der Query: <c>1</c> oder <c>true</c>.</summary>
    private bool QueryFlag(string name) => Request.Query[name].ToString() is "1" or "true" or "True";

    private string ResolveReturnUrl() =>
        string.IsNullOrEmpty(ReturnUrl) || !Url.IsLocalUrl(ReturnUrl) ? "/" : ReturnUrl;

    private string WithReturnUrl(string url) => string.IsNullOrWhiteSpace(ReturnUrl)
        ? url
        : url + (url.Contains('?') ? "&" : "?") + "returnUrl=" + Uri.EscapeDataString(ReturnUrl);
}

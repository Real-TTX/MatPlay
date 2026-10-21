using System.Security.Claims;
using System.Text.Encodings.Web;
using MatPlay.Data;
using Microsoft.AspNetCore.Authentication;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace MatPlay.Services;

/// <summary>Aktuelle Session + User für den Request, wird vom AuthHandler befüllt.</summary>
public class CurrentContext
{
    public UserSession? Session { get; set; }
    public User? User { get; set; }
    public long? UserId => User?.Id;
    public long? SessionId => Session?.Id;
    public bool IsAuthenticated => User != null;
    public bool IsAdmin => User?.Role == Roles.Admin;
    /// <summary>True, wenn die Anmeldung über MatIdentity lief (dann gibt es kein lokales Passwort zu ändern).</summary>
    public bool SignedInByIdentity => User != null && !string.IsNullOrEmpty(Session?.IdentityToken);
}

public static class SessionAuthDefaults
{
    public const string Scheme = "MatPlaySession";
    public const string CookieName = "matplay_session";
}

/// <summary>
/// Cookie mit Session-Token, Session liegt in SQLite (UserSession) und überlebt damit Container-Restarts.
/// Ohne gültige Session wird automatisch eine anonyme Session angelegt (Rolle "Anonymous").
/// </summary>
public class SessionAuthHandler(
    IOptionsMonitor<AuthenticationSchemeOptions> options,
    ILoggerFactory logger,
    UrlEncoder encoder,
    AppDbContext db,
    CurrentContext current)
    : AuthenticationHandler<AuthenticationSchemeOptions>(options, logger, encoder)
{
    protected override async Task<AuthenticateResult> HandleAuthenticateAsync()
    {
        var token = Request.Cookies[SessionAuthDefaults.CookieName];
        UserSession? session = null;

        if (!string.IsNullOrEmpty(token))
        {
            session = await db.UserSessions.FirstOrDefaultAsync(s =>
                s.Token == token && s.UpdateState != UpdateStates.Deleted && s.ExpiresDate > DateTime.UtcNow);
        }

        // Die Plattform-API (/api, /api/health, ...) prüft Schlüssel oder Token selbst; Maschinen ohne Cookie
        // (Docker-HEALTHCHECK, Mattiverse) sollen keine anonymen Sessions anhäufen, ebenso der Back-Channel-Logout von MatIdentity
        // (/signout-backchannel). Die Spiel-API /api/play bleibt wie sie ist.
        if (session == null && IsPlatformPath(Request.Path))
            return AuthenticateResult.NoResult();

        if (session == null)
        {
            session = new UserSession();
            db.UserSessions.Add(session);
            await db.SaveChangesAsync();
            WriteCookie(session.Token);
        }
        else if (session.LastSeenDate < DateTime.UtcNow.AddMinutes(-10))
        {
            session.LastSeenDate = DateTime.UtcNow;
            session.ExpiresDate = DateTime.UtcNow.AddDays(180);
            await db.SaveChangesAsync();
        }

        User? user = null;
        if (session.UserId is long uid)
            user = await db.Users.FirstOrDefaultAsync(u => u.Id == uid && u.UpdateState != UpdateStates.Deleted);

        current.Session = session;
        current.User = user;

        var claims = new List<Claim>
        {
            new(ClaimTypes.NameIdentifier, user?.Id.ToString() ?? $"anon-{session.Id}"),
            new(ClaimTypes.Name, user?.DisplayName ?? "Gast"),
            new(ClaimTypes.Role, user?.Role ?? Roles.Anonymous),
            new("SessionId", session.Id.ToString()),
        };
        var identity = new ClaimsIdentity(claims, Scheme.Name);
        return AuthenticateResult.Success(new AuthenticationTicket(new ClaimsPrincipal(identity), Scheme.Name));
    }

    private static bool IsPlatformPath(PathString path) =>
        (path.StartsWithSegments("/api") && !path.StartsWithSegments("/api/play"))
        || path.StartsWithSegments(MatPlatform.MatIdentitySignIn.BackchannelLogoutPath);

    protected override Task HandleChallengeAsync(AuthenticationProperties properties)
    {
        Response.Redirect("/account/login?returnUrl=" + Uri.EscapeDataString(Request.Path + Request.QueryString));
        return Task.CompletedTask;
    }

    protected override Task HandleForbiddenAsync(AuthenticationProperties properties)
    {
        Response.StatusCode = 403;
        return Task.CompletedTask;
    }

    private void WriteCookie(string token) =>
        Response.Cookies.Append(SessionAuthDefaults.CookieName, token, new CookieOptions
        {
            HttpOnly = true,
            SameSite = SameSiteMode.Lax,
            Secure = Request.IsHttps,
            Expires = DateTimeOffset.UtcNow.AddDays(180),
            Path = "/",
        });
}

/// <summary>Login/Logout-Operationen auf der Session.</summary>
public class AuthService(AppDbContext db, CurrentContext current, IHttpContextAccessor http)
{
    public async Task<User?> LoginAsync(string username, string password)
    {
        var user = await db.Users.FirstOrDefaultAsync(u =>
            u.Username == username && u.UpdateState != UpdateStates.Deleted);
        if (user == null || !PasswordHasher.Verify(password, user.PasswordHash)) return null;

        await SignInAsync(user);
        return user;
    }

    /// <summary>
    /// Meldet <paramref name="user"/> in der aktuellen Session an (Token-Rotation). <paramref name="identityToken"/>: das ID-Token
    /// von MatIdentity bei einer Anmeldung dort, für die spätere Abmeldung bei MatIdentity; <paramref name="identitySessionId"/>: die Session
    /// dort (Claim <c>sid</c>), für den Back-Channel-Logout (<see cref="EndIdentitySessionsAsync"/>).
    /// </summary>
    public async Task SignInAsync(User user, string? identityToken = null, string? identitySessionId = null)
    {
        // Im Rückruf von MatIdentity (/signin-oidc) ist die Session noch nicht gelesen: das holt der Session-Handler nach.
        if (current.Session == null)
            await http.HttpContext!.AuthenticateAsync(SessionAuthDefaults.Scheme);

        var session = current.Session!;
        // Token-Rotation beim Login
        session.Token = Guid.NewGuid().ToString("N");
        session.UserId = user.Id;
        session.IdentityToken = identityToken;
        session.IdentitySessionId = identitySessionId;
        session.UpdateDate = DateTime.UtcNow;
        session.UpdateUserId = user.Id;
        session.UpdateState = UpdateStates.Updated;
        session.ExpiresDate = DateTime.UtcNow.AddDays(180);
        await db.SaveChangesAsync();

        WriteCookie(session.Token);
        current.User = user;
    }

    /// <summary>Das ID-Token von MatIdentity, wenn die aktuelle Anmeldung über MatIdentity lief, sonst null.</summary>
    public string? IdentityToken => current.User != null ? current.Session?.IdentityToken : null;

    public async Task LogoutAsync()
    {
        var session = current.Session;
        if (session == null) return;
        session.UserId = null;
        session.IdentityToken = null;
        session.IdentitySessionId = null;
        session.Token = Guid.NewGuid().ToString("N");
        session.UpdateDate = DateTime.UtcNow;
        session.UpdateState = UpdateStates.Updated;
        await db.SaveChangesAsync();
        WriteCookie(session.Token);
        current.User = null;
    }

    /// <summary>
    /// Back-Channel-Logout von MatIdentity: meldet die Sessions ab, die mit der Session <paramref name="identitySessionId"/> (Claim <c>sid</c>)
    /// angemeldet wurden, oder, nur mit <paramref name="subject"/>, alle Sessions des damit verknüpften Benutzers (abgemeldet überall).
    /// Die Sessions bleiben als anonyme Sessions bestehen (wie nach <see cref="LogoutAsync"/>); der nächste Request des Browsers ist abgemeldet.
    /// Unbekannte <c>sub</c>/<c>sid</c>: nichts zu tun. Liefert die Anzahl der beendeten Sessions.
    /// </summary>
    public async Task<int> EndIdentitySessionsAsync(string? subject, string? identitySessionId, CancellationToken ct = default)
    {
        IQueryable<UserSession> sessions = db.UserSessions.Where(s => s.UserId != null && s.UpdateState != UpdateStates.Deleted);
        if (!string.IsNullOrEmpty(identitySessionId))
        {
            sessions = sessions.Where(s => s.IdentitySessionId == identitySessionId);
        }
        else if (!string.IsNullOrEmpty(subject))
        {
            var userId = await db.Users.Where(u => u.ExternalSubject == subject).Select(u => (long?)u.Id).FirstOrDefaultAsync(ct);
            if (userId is null) return 0;
            sessions = sessions.Where(s => s.UserId == userId);
        }
        else
        {
            return 0;
        }

        var ended = await sessions.ToListAsync(ct);
        foreach (var session in ended)
        {
            session.UserId = null;
            session.IdentityToken = null;
            session.IdentitySessionId = null;
            session.UpdateDate = DateTime.UtcNow;
            session.UpdateState = UpdateStates.Updated;
        }
        await db.SaveChangesAsync(ct);
        return ended.Count;
    }

    private void WriteCookie(string token)
    {
        var ctx = http.HttpContext!;
        ctx.Response.Cookies.Append(SessionAuthDefaults.CookieName, token, new CookieOptions
        {
            HttpOnly = true,
            SameSite = SameSiteMode.Lax,
            Secure = ctx.Request.IsHttps,
            Expires = DateTimeOffset.UtcNow.AddDays(180),
            Path = "/",
        });
    }
}

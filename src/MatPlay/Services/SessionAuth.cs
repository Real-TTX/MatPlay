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

        var session = current.Session!;
        // Token-Rotation beim Login
        session.Token = Guid.NewGuid().ToString("N");
        session.UserId = user.Id;
        session.UpdateDate = DateTime.UtcNow;
        session.UpdateUserId = user.Id;
        session.UpdateState = UpdateStates.Updated;
        session.ExpiresDate = DateTime.UtcNow.AddDays(180);
        await db.SaveChangesAsync();

        WriteCookie(session.Token);
        current.User = user;
        return user;
    }

    public async Task LogoutAsync()
    {
        var session = current.Session;
        if (session == null) return;
        session.UserId = null;
        session.Token = Guid.NewGuid().ToString("N");
        session.UpdateDate = DateTime.UtcNow;
        session.UpdateState = UpdateStates.Updated;
        await db.SaveChangesAsync();
        WriteCookie(session.Token);
        current.User = null;
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

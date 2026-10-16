using MatPlay.Data;
using Microsoft.EntityFrameworkCore;

namespace MatPlay.Services;

/// <summary>Eine Person, die MatIdentity angemeldet hat (Claims <c>sub</c>, <c>email</c>, <c>name</c>, <c>preferred_username</c>, <c>role</c>).</summary>
public sealed record ExternalIdentity(string Subject, string? Email, string? Name, string? PreferredUserName, string? Role);

/// <summary>Ergebnis von <see cref="ExternalSignInService.FindOrCreateAsync"/>: der lokale Benutzer oder ein Grund für die Ablehnung.</summary>
public sealed record ExternalSignInResult(User? User, string? Error)
{
    public bool Succeeded => User is not null;
}

/// <summary>
/// Ordnet eine von MatIdentity angemeldete Person einem lokalen Benutzer zu: zuerst über <see cref="User.ExternalSubject"/> (<c>sub</c>),
/// sonst über die E-Mail-Adresse (ein lokaler Benutzer, dessen Benutzername diese Adresse ist), sonst wird ein Benutzer angelegt.
/// Die Rolle folgt dem Claim <c>role</c> (<c>admin</c>/<c>user</c>), wenn er da ist; ohne Claim verwaltet MatPlay die Rolle selbst.
/// Gelöschte Benutzer bleiben draußen.
/// </summary>
public class ExternalSignInService(AppDbContext db, ILogger<ExternalSignInService> logger)
{
    /// <summary>Die Rollen der Plattform (Claim <c>role</c>) und die von MatPlay.</summary>
    public static string? MapRole(string? claim) => claim?.Trim().ToLowerInvariant() switch
    {
        "admin" => Roles.Admin,
        "user" => Roles.User,
        _ => null,
    };

    public async Task<ExternalSignInResult> FindOrCreateAsync(ExternalIdentity identity, CancellationToken ct = default)
    {
        var subject = identity.Subject?.Trim();
        if (string.IsNullOrEmpty(subject))
        {
            return new ExternalSignInResult(null, "MatIdentity hat keine Kennung (sub) geschickt.");
        }

        var email = string.IsNullOrWhiteSpace(identity.Email) ? null : identity.Email.Trim().ToLowerInvariant();
        var user = await db.Users.FirstOrDefaultAsync(u => u.ExternalSubject == subject, ct);

        if (user is null && email is not null)
        {
            // Ein lokaler Benutzer mit dieser Adresse als Benutzername wird bei der ersten Anmeldung verknüpft.
            var matches = await db.Users.Where(u => u.Username.ToLower() == email).ToListAsync(ct);
            if (matches.Any(u => u.UpdateState == UpdateStates.Deleted))
            {
                return new ExternalSignInResult(null, "Das Konto mit dieser E-Mail-Adresse wurde gelöscht.");
            }

            if (matches is [{ ExternalSubject: null } match])
            {
                user = match;
                user.ExternalSubject = subject;
                Touch(user);
                logger.LogInformation("Benutzer {UserId} mit MatIdentity verknüpft ({Subject})", user.Id, subject);
            }
        }

        if (user is { UpdateState: UpdateStates.Deleted })
        {
            return new ExternalSignInResult(null, "Das Konto wurde gelöscht.");
        }

        var role = MapRole(identity.Role);

        if (user is null)
        {
            user = new User
            {
                Username = await FreeUsernameAsync(email, identity.PreferredUserName, subject, ct),
                DisplayName = DisplayNameOf(identity, email),
                PasswordHash = "",
                Role = role ?? Roles.User,
                ExternalSubject = subject,
            };
            db.Users.Add(user);
            await db.SaveChangesAsync(ct);
            user.CreateUserId = user.UpdateUserId = user.Id;
            await db.SaveChangesAsync(ct);
            logger.LogInformation("Benutzer {UserId} über MatIdentity angelegt ({Subject})", user.Id, subject);
            return new ExternalSignInResult(user, null);
        }

        // Die Rolle folgt dem Claim; den letzten Administrator stuft ein Claim aber nicht herab.
        if (role is not null && role != user.Role)
        {
            var lastAdmin = user.Role == Roles.Admin && await db.Users.CountAsync(
                u => u.Id != user.Id && u.UpdateState != UpdateStates.Deleted && u.Role == Roles.Admin, ct) == 0;
            if (lastAdmin)
            {
                logger.LogWarning("Rolle {Role} für {UserId} aus MatIdentity ignoriert: letzter Administrator", role, user.Id);
            }
            else
            {
                user.Role = role;
                Touch(user);
            }
        }

        await db.SaveChangesAsync(ct);
        return new ExternalSignInResult(user, null);
    }

    private static void Touch(User user)
    {
        user.UpdateDate = DateTime.UtcNow;
        user.UpdateUserId = user.Id;
        user.UpdateState = UpdateStates.Updated;
    }

    private static string DisplayNameOf(ExternalIdentity identity, string? email)
    {
        foreach (var candidate in new[] { identity.Name, identity.PreferredUserName, email?.Split('@')[0] })
        {
            if (!string.IsNullOrWhiteSpace(candidate)) return candidate.Trim();
        }
        return "MatIdentity";
    }

    /// <summary>Ein freier Benutzername: die E-Mail-Adresse, sonst <c>preferred_username</c>, sonst mit Zähler, zuletzt aus <c>sub</c>.</summary>
    private async Task<string> FreeUsernameAsync(string? email, string? preferred, string subject, CancellationToken ct)
    {
        var candidates = new List<string>();
        if (email is not null) candidates.Add(email);
        if (!string.IsNullOrWhiteSpace(preferred))
        {
            var name = preferred.Trim();
            candidates.Add(name);
            candidates.AddRange(Enumerable.Range(2, 98).Select(i => $"{name}-{i}"));
        }
        candidates.Add("matidentity-" + new string(subject.Where(char.IsLetterOrDigit).Take(24).ToArray()).ToLowerInvariant());
        candidates.Add("matidentity-" + Guid.NewGuid().ToString("N")[..12]);

        foreach (var candidate in candidates)
        {
            var lower = candidate.ToLowerInvariant();
            if (!await db.Users.AnyAsync(u => u.Username.ToLower() == lower, ct)) return candidate;
        }
        return "matidentity-" + Guid.NewGuid().ToString("N");
    }
}

using MatPlay.Data;
using Microsoft.EntityFrameworkCore;

namespace MatPlay.Services;

/// <summary>Eine Person, die MatIdentity angemeldet hat (Claims <c>sub</c>, <c>email</c>, <c>name</c>, <c>preferred_username</c>, <c>role</c>).</summary>
public sealed record ExternalIdentity(string Subject, string? Email, string? Name, string? PreferredUserName, string? Role);

/// <summary>
/// Ergebnis von <see cref="ExternalSignInService.FindOrCreateAsync"/>: der lokale Benutzer, ein Grund für die Ablehnung oder
/// (<see cref="LinkUserId"/>) ein vorhandenes Konto, das erst nach seinem Passwort verknüpft wird.
/// </summary>
public sealed record ExternalSignInResult(User? User, string? Error, long? LinkUserId = null)
{
    public bool Succeeded => User is not null;

    /// <summary>Ein Konto passt über die Adresse, ist aber nicht bewiesen: erst die lokale Anmeldung mit seinem Passwort verknüpft es.</summary>
    public bool LinkRequired => LinkUserId is not null;
}

/// <summary>
/// Ordnet eine von MatIdentity angemeldete Person einem lokalen Benutzer zu: zuerst über <see cref="User.ExternalSubject"/> (<c>sub</c>),
/// sonst wird ein Benutzer angelegt. Ein Konto, dessen Benutzername ihre E-Mail-Adresse ist, wird <b>nicht</b> über die Adresse allein
/// verknüpft: den Benutzernamen kann sich bei der Registrierung jeder aussuchen, er beweist nichts. Die Person muss sich dann einmal
/// lokal mit dem Passwort dieses Kontos anmelden (<see cref="LinkAfterLocalSignInAsync"/>).
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

        var email = NormalizeEmail(identity.Email);
        var user = await db.Users.FirstOrDefaultAsync(u => u.ExternalSubject == subject, ct);

        if (user is null && email is not null)
        {
            var matches = await db.Users.Where(u => u.Username.ToLower() == email).ToListAsync(ct);
            if (matches.Any(u => u.UpdateState == UpdateStates.Deleted))
            {
                return new ExternalSignInResult(null, "Das Konto mit dieser E-Mail-Adresse wurde gelöscht.");
            }

            // Ein lokales Konto mit dieser Adresse als Benutzername: nicht verknüpfen, sondern erst sein Passwort verlangen.
            // Ein Konto ohne Passwort kann das nicht beweisen: dann ein neuer Benutzer daneben (wie bei einem schon verknüpften Konto).
            if (matches is [{ ExternalSubject: null } match] && HasPassword(match))
            {
                logger.LogInformation("Benutzer {UserId} passt über die Adresse zu {Subject}: Verknüpfung erst nach der lokalen Anmeldung", match.Id, subject);
                return new ExternalSignInResult(null, null, match.Id);
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

        await ApplyRoleAsync(user, role, ct);
        await db.SaveChangesAsync(ct);
        return new ExternalSignInResult(user, null);
    }

    /// <summary>
    /// Verknüpft das Konto <paramref name="userId"/> mit der Person von MatIdentity, nachdem es sich gerade lokal mit seinem Passwort angemeldet hat
    /// (die ausstehende Verknüpfung galt genau diesem Konto). Prüft noch einmal, dass es aktiv, noch nicht verknüpft und über die Adresse
    /// das passende ist und dass kein anderes Konto den <c>sub</c> schon hat; die Rolle folgt dem Claim wie bei einer Anmeldung über MatIdentity.
    /// </summary>
    public async Task<ExternalSignInResult> LinkAfterLocalSignInAsync(long userId, ExternalIdentity identity, CancellationToken ct = default)
    {
        var subject = identity.Subject?.Trim();
        var email = NormalizeEmail(identity.Email);
        if (string.IsNullOrEmpty(subject) || email is null)
        {
            return new ExternalSignInResult(null, "Die Verknüpfung mit MatIdentity ist unvollständig.");
        }

        var user = await db.Users.FirstOrDefaultAsync(u => u.Id == userId, ct);
        if (user is null || user.UpdateState == UpdateStates.Deleted || user.ExternalSubject is not null
            || !string.Equals(user.Username, email, StringComparison.OrdinalIgnoreCase))
        {
            return new ExternalSignInResult(null, "Dieses Konto kann nicht mit MatIdentity verknüpft werden.");
        }

        if (await db.Users.AnyAsync(u => u.ExternalSubject == subject, ct))
        {
            return new ExternalSignInResult(null, "Diese Person von MatIdentity ist schon mit einem anderen Konto verknüpft.");
        }

        user.ExternalSubject = subject;
        Touch(user);
        await ApplyRoleAsync(user, MapRole(identity.Role), ct);
        await db.SaveChangesAsync(ct);
        logger.LogInformation("Benutzer {UserId} nach der lokalen Anmeldung mit MatIdentity verknüpft ({Subject})", user.Id, subject);
        return new ExternalSignInResult(user, null);
    }

    /// <summary>Die Rolle folgt dem Claim; den letzten Administrator stuft ein Claim aber nicht herab.</summary>
    private async Task ApplyRoleAsync(User user, string? role, CancellationToken ct)
    {
        if (role is null || role == user.Role) return;

        var lastAdmin = user.Role == Roles.Admin && await db.Users.CountAsync(
            u => u.Id != user.Id && u.UpdateState != UpdateStates.Deleted && u.Role == Roles.Admin, ct) == 0;
        if (lastAdmin)
        {
            logger.LogWarning("Rolle {Role} für {UserId} aus MatIdentity ignoriert: letzter Administrator", role, user.Id);
            return;
        }

        user.Role = role;
        Touch(user);
    }

    private static string? NormalizeEmail(string? email) => string.IsNullOrWhiteSpace(email) ? null : email.Trim().ToLowerInvariant();

    /// <summary>Ob sich das Konto lokal anmelden kann (sonst kann es die Verknüpfung nicht beweisen).</summary>
    private static bool HasPassword(User user) => !string.IsNullOrEmpty(user.PasswordHash);

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

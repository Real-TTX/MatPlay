using System.Globalization;
using MatPlatform;
using MatPlay.Services;

namespace MatPlay.Platform;

/// <summary>
/// MatPlays Seite der Anmeldung über MatIdentity (MatPlatform <see cref="IMatIdentityUsers"/>): die Person wird über
/// <see cref="ExternalSignInService"/> einem lokalen Benutzer zugeordnet (sub, sonst neu; Rolle aus dem Claim <c>role</c>)
/// und dann wie bei der lokalen Anmeldung in der MatPlay-Session angemeldet, mit dem ID-Token für die Abmeldung bei MatIdentity.
/// Passt ein Konto nur über die Adresse, wird niemand angemeldet: MatPlatform merkt sich die Verknüpfung
/// (<see cref="MatIdentityPendingLink"/>) und schickt zur lokalen Anmeldung (<c>?local=1&amp;link=1</c>), wo das Passwort des Kontos sie beweist.
/// Meldet MatIdentity eine Person ab (Back-Channel-Logout, <see cref="IMatIdentitySessions"/>), beendet <see cref="AuthService.EndIdentitySessionsAsync"/>
/// die Sessions mit diesem <c>sid</c> bzw. alle des Benutzers mit diesem <c>sub</c>.
/// </summary>
public sealed class PlayIdentityUsers(ExternalSignInService users, AuthService auth, ILogger<PlayIdentityUsers> logger) : IMatIdentityUsers, IMatIdentitySessions
{
    public async Task EndSessionsAsync(string? subject, string? sessionId, CancellationToken ct)
    {
        var ended = await auth.EndIdentitySessionsAsync(subject, sessionId, ct);
        logger.LogInformation("MatIdentity hat abgemeldet (sub {Subject}, sid {SessionId}): {Count} Session(s) beendet", subject, sessionId, ended);
    }

    public async Task<MatIdentitySignInResult> SignInAsync(MatIdentityPerson person, HttpContext http, CancellationToken ct)
    {
        var result = await users.FindOrCreateAsync(
            new ExternalIdentity(person.Subject, person.Email, person.Name, person.PreferredUserName, person.Role), ct);
        if (result.LinkUserId is { } linkUserId)
        {
            return MatIdentitySignInResult.LinkRequired(linkUserId.ToString(CultureInfo.InvariantCulture));
        }

        if (!result.Succeeded)
        {
            return MatIdentitySignInResult.Refused(result.Error ?? "refused");
        }

        var user = result.User!;
        await auth.SignInAsync(user, person.IdToken, person.SessionId);
        logger.LogInformation("\"{DisplayName}\" ({UserId}) hat sich über MatIdentity angemeldet", user.DisplayName, user.Id);
        return MatIdentitySignInResult.Success();
    }
}

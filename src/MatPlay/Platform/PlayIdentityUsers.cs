using MatPlatform;
using MatPlay.Services;

namespace MatPlay.Platform;

/// <summary>
/// MatPlays Seite der Anmeldung über MatIdentity (MatPlatform <see cref="IMatIdentityUsers"/>): die Person wird über
/// <see cref="ExternalSignInService"/> einem lokalen Benutzer zugeordnet (sub, sonst E-Mail, sonst neu; Rolle aus dem Claim <c>role</c>)
/// und dann wie bei der lokalen Anmeldung in der MatPlay-Session angemeldet, mit dem ID-Token für die Abmeldung bei MatIdentity.
/// </summary>
public sealed class PlayIdentityUsers(ExternalSignInService users, AuthService auth, ILogger<PlayIdentityUsers> logger) : IMatIdentityUsers
{
    public async Task<MatIdentitySignInResult> SignInAsync(MatIdentityPerson person, HttpContext http, CancellationToken ct)
    {
        var result = await users.FindOrCreateAsync(
            new ExternalIdentity(person.Subject, person.Email, person.Name, person.PreferredUserName, person.Role), ct);
        if (!result.Succeeded)
        {
            return MatIdentitySignInResult.Refused(result.Error ?? "refused");
        }

        var user = result.User!;
        await auth.SignInAsync(user, person.IdToken);
        logger.LogInformation("\"{DisplayName}\" ({UserId}) hat sich über MatIdentity angemeldet", user.DisplayName, user.Id);
        return MatIdentitySignInResult.Success();
    }
}

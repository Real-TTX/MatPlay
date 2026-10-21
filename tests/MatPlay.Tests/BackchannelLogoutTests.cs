using System.Net;
using MatPlatform;
using MatPlay.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.IdentityModel.JsonWebTokens;
using Microsoft.IdentityModel.Tokens;

namespace MatPlay.Tests;

/// <summary>
/// Der Back-Channel-Logout (MatPlatform 1.4.0): MatIdentity schickt ein signiertes Logout-Token an <c>/signout-backchannel</c>, MatPlay
/// meldet die Sessions mit diesem <c>sid</c> ab (nur mit <c>sub</c>: alle Sessions des Benutzers); der nächste Request ist abgemeldet.
/// </summary>
public sealed class BackchannelLogoutTests(IdentityFactory site) : IClassFixture<IdentityFactory>
{
    private const string Path = "/signout-backchannel";

    [Fact]
    public async Task A_valid_token_signs_out_the_session_it_names_and_only_that()
    {
        var first = await SignInAsync("sub-bc-1", "sid-bc-1a");
        var second = await SignInAsync("sub-bc-1", "sid-bc-1b");

        var response = await PostAsync(Token("sub-bc-1", "sid-bc-1a"));

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal("no-store", response.Headers.CacheControl!.ToString());
        await AssertSignedOutAsync(first);
        await AssertSignedInAsync(second);
    }

    [Fact]
    public async Task A_token_with_the_subject_alone_signs_out_every_session_of_the_person()
    {
        var first = await SignInAsync("sub-bc-2", "sid-bc-2a");
        var second = await SignInAsync("sub-bc-2", "sid-bc-2b");
        var other = await SignInAsync("sub-bc-3", "sid-bc-3");

        Assert.Equal(HttpStatusCode.OK, (await PostAsync(Token("sub-bc-2", null))).StatusCode);

        await AssertSignedOutAsync(first);
        await AssertSignedOutAsync(second);
        await AssertSignedInAsync(other);
    }

    [Fact]
    public async Task An_unknown_person_or_session_is_no_error()
    {
        Assert.Equal(HttpStatusCode.OK, (await PostAsync(Token("sub-unknown", null))).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await PostAsync(Token(null, "sid-unknown"))).StatusCode);
    }

    [Fact]
    public async Task A_token_signed_with_another_key_is_400_and_signs_out_nothing()
    {
        var browser = await SignInAsync("sub-bc-4", "sid-bc-4");
        var forged = new SigningCredentials(new RsaSecurityKey(System.Security.Cryptography.RSA.Create(2048)) { KeyId = "forger" }, SecurityAlgorithms.RsaSha256);

        Assert.Equal(HttpStatusCode.BadRequest, (await PostAsync(Token("sub-bc-4", "sid-bc-4", forged))).StatusCode);

        await AssertSignedInAsync(browser);
    }

    [Fact]
    public async Task The_call_of_matidentity_leaves_no_anonymous_session_behind()
    {
        var before = await SessionCountAsync();
        Assert.Equal(HttpStatusCode.OK, (await PostAsync(Token("sub-unknown-2", null))).StatusCode);
        Assert.Equal(before, await SessionCountAsync());
    }

    /// <summary>Meldet einen neuen Browser über den Rückruf an (Rolle admin, damit <c>/admin/settings</c> zeigt, ob er angemeldet ist).</summary>
    private async Task<HttpClient> SignInAsync(string subject, string sessionId)
    {
        var browser = site.Browser();
        var callback = await browser.GetAsync($"/test/signin-oidc?sub={subject}&email={subject}@example.com&role=admin&sid={sessionId}");
        Assert.Equal(HttpStatusCode.Redirect, callback.StatusCode);
        Assert.Equal("/", callback.Headers.Location!.OriginalString);
        await AssertSignedInAsync(browser);
        return browser;
    }

    private static async Task AssertSignedInAsync(HttpClient browser)
        => Assert.Equal(HttpStatusCode.OK, (await browser.GetAsync("/admin/settings")).StatusCode);

    /// <summary>Abgemeldet ist der Browser wieder ein Gast (anonyme Session, Rolle Anonymous): die Verwaltung ist ihm verboten.</summary>
    private static async Task AssertSignedOutAsync(HttpClient browser)
        => Assert.Equal(HttpStatusCode.Forbidden, (await browser.GetAsync("/admin/settings")).StatusCode);

    private async Task<int> SessionCountAsync()
    {
        await using var scope = site.Services.CreateAsyncScope();
        return await scope.ServiceProvider.GetRequiredService<AppDbContext>().UserSessions.CountAsync();
    }

    private Task<HttpResponseMessage> PostAsync(string token)
        => site.CreateClient().PostAsync(Path, new FormUrlEncodedContent(new Dictionary<string, string> { ["logout_token"] = token }));

    /// <summary>Ein Logout-Token, wie MatIdentity es schreibt (OpenID Connect Back-Channel Logout 1.0).</summary>
    internal static string Token(string? subject, string? sessionId, SigningCredentials? credentials = null)
    {
        var now = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
        var claims = new Dictionary<string, object>
        {
            ["iss"] = IdentityFactory.Authority,
            ["aud"] = "matplay",
            ["iat"] = now,
            ["nbf"] = now,
            ["exp"] = now + 120,
            ["jti"] = Guid.NewGuid().ToString("N"),
            ["events"] = new Dictionary<string, object> { [MatIdentityLogoutToken.EventType] = new Dictionary<string, object>() },
        };
        if (subject is not null) claims["sub"] = subject;
        if (sessionId is not null) claims["sid"] = sessionId;

        return new JsonWebTokenHandler { SetDefaultTimesOnTokenCreation = false }.CreateToken(new SecurityTokenDescriptor
        {
            Claims = claims,
            TokenType = MatIdentityLogoutToken.TokenType,
            SigningCredentials = credentials ?? PlayFactory.IdentityCredentials,
        });
    }
}

/// <summary>Ohne <c>MATIDENTITY_AUTHORITY</c> gibt es den Back-Channel-Logout nicht.</summary>
public sealed class BackchannelWithoutIdentityTests(PlayFactory site) : IClassFixture<PlayFactory>
{
    [Fact]
    public async Task Without_matidentity_there_is_no_endpoint()
    {
        var response = await site.CreateClient().PostAsync("/signout-backchannel", new FormUrlEncodedContent(new Dictionary<string, string>
        {
            ["logout_token"] = BackchannelLogoutTests.Token("sub", "sid"),
        }));

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }
}

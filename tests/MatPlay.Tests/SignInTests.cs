using System.Net;
using System.Text.Json;
using System.Text.RegularExpressions;
using MatPlay.Data;
using Microsoft.AspNetCore.WebUtilities;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace MatPlay.Tests;

internal static partial class Forms
{
    /// <summary>Das Antiforgery-Token aus einer Seite.</summary>
    public static string Token(string html) => TokenRegex().Match(html).Groups[1].Value;

    /// <summary>Meldet <paramref name="browser"/> lokal als admin/admin an (über <c>?local=1</c>, das geht mit und ohne MatIdentity).</summary>
    public static async Task<HttpResponseMessage> SignInLocallyAsync(HttpClient browser)
    {
        var page = await browser.GetAsync("/account/login?local=1");
        Assert.Equal(HttpStatusCode.OK, page.StatusCode);
        return await browser.PostAsync("/account/login?local=1", new FormUrlEncodedContent(new Dictionary<string, string>
        {
            ["__RequestVerificationToken"] = Token(await page.Content.ReadAsStringAsync()),
            ["Username"] = "admin",
            ["Password"] = "admin",
        }));
    }

    [GeneratedRegex(@"name=""__RequestVerificationToken"" type=""hidden"" value=""([^""]+)""")]
    private static partial Regex TokenRegex();
}

/// <summary>Ohne <c>MATIDENTITY_AUTHORITY</c> ändert sich nichts: das lokale Formular wie bisher.</summary>
public sealed class LocalSignInTests(PlayFactory site) : IClassFixture<PlayFactory>
{
    [Fact]
    public async Task The_sign_in_page_is_the_local_form()
    {
        var response = await site.Browser().GetAsync("/account/login");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var html = await response.Content.ReadAsStringAsync();
        Assert.Contains("id=\"login-form\"", html);
        Assert.DoesNotContain("MatIdentity", html);

        // ?local=1 und ?signedOut=1 ändern daran nichts.
        Assert.DoesNotContain("MatIdentity", await site.Browser().GetStringAsync("/account/login?signedOut=1"));
    }

    [Fact]
    public async Task There_is_no_callback_of_matidentity_and_the_definition_names_none()
    {
        Assert.Equal(HttpStatusCode.NotFound, (await site.Browser().GetAsync("/signin-oidc")).StatusCode);
        using var doc = JsonDocument.Parse(await site.CreateClient().GetStringAsync("/api"));
        Assert.False(doc.RootElement.GetProperty("auth").TryGetProperty("identity", out _));
    }

    [Fact]
    public async Task The_local_sign_in_and_sign_out_work_as_before()
    {
        var browser = site.Browser();
        var signedIn = await Forms.SignInLocallyAsync(browser);
        Assert.Equal(HttpStatusCode.Redirect, signedIn.StatusCode);
        Assert.Equal("/", signedIn.Headers.Location!.OriginalString);

        // Der Seed-Admin muss zuerst sein Passwort ändern (wie bisher).
        var home = await browser.GetAsync("/");
        Assert.Equal(HttpStatusCode.Redirect, home.StatusCode);
        Assert.Equal("/account/change-password", home.Headers.Location!.OriginalString);

        var page = await browser.GetStringAsync("/account/change-password");
        var signedOut = await browser.PostAsync("/account/logout", new FormUrlEncodedContent(new Dictionary<string, string>
        {
            ["__RequestVerificationToken"] = Forms.Token(page),
        }));
        Assert.Equal(HttpStatusCode.Redirect, signedOut.StatusCode);
        Assert.Equal("/", signedOut.Headers.Location!.OriginalString);
    }

    [Fact]
    public async Task A_wrong_password_shows_the_form_with_an_error()
    {
        var browser = site.Browser();
        var page = await browser.GetAsync("/account/login");
        var response = await browser.PostAsync("/account/login", new FormUrlEncodedContent(new Dictionary<string, string>
        {
            ["__RequestVerificationToken"] = Forms.Token(await page.Content.ReadAsStringAsync()),
            ["Username"] = "admin",
            ["Password"] = "falsch",
        }));
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Contains("Benutzername oder Passwort ist falsch.", await response.Content.ReadAsStringAsync());
    }
}

/// <summary>Mit <c>MATIDENTITY_AUTHORITY</c>: automatische Anmeldung bei MatIdentity, lokal mit <c>?local=1</c>, Abmeldung auch dort; Gäste bleiben Gäste.</summary>
public sealed class IdentitySignInTests(IdentityFactory site) : IClassFixture<IdentityFactory>
{
    [Fact]
    public async Task The_sign_in_page_goes_straight_to_matidentity_with_code_and_pkce()
    {
        var response = await site.Browser().GetAsync("/account/login?returnUrl=%2Fplayers");
        Assert.Equal(HttpStatusCode.Redirect, response.StatusCode);

        var location = response.Headers.Location!;
        Assert.StartsWith(IdentityFactory.Authority + "/connect/authorize", location.OriginalString);

        var query = QueryHelpers.ParseQuery(location.Query);
        Assert.Equal("matplay", query["client_id"].ToString());
        Assert.Equal("code", query["response_type"].ToString());
        Assert.Equal("S256", query["code_challenge_method"].ToString());
        Assert.False(string.IsNullOrEmpty(query["code_challenge"].ToString()));
        Assert.EndsWith("/signin-oidc", query["redirect_uri"].ToString());
        Assert.Equal(["openid", "profile", "email", "roles", "offline_access"], query["scope"].ToString().Split(' '));
    }

    [Fact]
    public async Task Guests_still_play_without_signing_in()
    {
        var response = await site.Browser().GetAsync("/");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    [Fact]
    public async Task The_local_form_stays_reachable_and_works()
    {
        var page = await site.Browser().GetAsync("/account/login?local=1");
        Assert.Equal(HttpStatusCode.OK, page.StatusCode);
        var html = await page.Content.ReadAsStringAsync();
        Assert.Contains("id=\"login-form\"", html);
        Assert.Contains("Mit MatIdentity anmelden", html);

        var browser = site.Browser();
        var signedIn = await Forms.SignInLocallyAsync(browser);
        Assert.Equal(HttpStatusCode.Redirect, signedIn.StatusCode);
        Assert.Equal("/", signedIn.Headers.Location!.OriginalString);
    }

    [Theory]
    [InlineData("/account/login?signedOut=1")]
    [InlineData("/account/login?ssoError=1")]
    [InlineData("/account/login?local=true")]
    public async Task No_automatic_sign_in_after_the_sign_out_after_an_error_or_with_local(string url)
    {
        var response = await site.Browser().GetAsync(url);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Contains("Mit MatIdentity anmelden", await response.Content.ReadAsStringAsync());
    }

    [Fact]
    public async Task The_sign_out_after_a_sign_in_through_matidentity_ends_the_session_there_too()
    {
        var browser = site.Browser();
        Assert.Equal(HttpStatusCode.Redirect, (await Forms.SignInLocallyAsync(browser)).StatusCode);

        // Wie nach einer Anmeldung über MatIdentity: die Session trägt das ID-Token.
        using (var scope = site.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            foreach (var session in await db.UserSessions.Where(s => s.UserId != null).ToListAsync())
            {
                session.IdentityToken = "the-id-token";
            }
            await db.SaveChangesAsync();
        }

        // Ohne lokales Passwort-Thema: nach MatIdentity gibt es keinen Zwang zur Passwort-Änderung.
        var home = await browser.GetAsync("/");
        Assert.Equal(HttpStatusCode.OK, home.StatusCode);

        var signedOut = await browser.PostAsync("/account/logout", new FormUrlEncodedContent(new Dictionary<string, string>
        {
            ["__RequestVerificationToken"] = Forms.Token(await home.Content.ReadAsStringAsync()),
        }));
        Assert.Equal(HttpStatusCode.Redirect, signedOut.StatusCode);
        var location = signedOut.Headers.Location!;
        Assert.StartsWith(IdentityFactory.Authority + "/connect/logout", location.OriginalString);
        var query = QueryHelpers.ParseQuery(location.Query);
        Assert.Equal("the-id-token", query["id_token_hint"].ToString());
        Assert.EndsWith("/signout-callback-oidc", query["post_logout_redirect_uri"].ToString());

        using (var scope = site.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            Assert.False(await db.UserSessions.AnyAsync(s => s.IdentityToken != null && s.UserId != null));
        }
    }

    [Fact]
    public async Task The_definition_names_matidentity_and_a_bad_token_is_401()
    {
        using var doc = JsonDocument.Parse(await site.CreateClient().GetStringAsync("/api"));
        Assert.Equal(IdentityFactory.Authority, doc.RootElement.GetProperty("auth").GetProperty("identity").GetString());

        var bearer = site.CreateClient();
        bearer.DefaultRequestHeaders.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", "not-a-token");
        Assert.Equal(HttpStatusCode.Unauthorized, (await bearer.GetAsync("/api/state")).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await site.Api().GetAsync("/api/state")).StatusCode);
    }
}

/// <summary>MatIdentity ist eingerichtet, aber aus: die Anmeldeseite zeigt das lokale Formular mit Hinweis statt eines Fehlers.</summary>
public sealed class UnreachableIdentityTests(UnreachableIdentityFactory site) : IClassFixture<UnreachableIdentityFactory>
{
    [Fact]
    public async Task The_sign_in_page_shows_the_local_form_with_a_message()
    {
        var response = await site.Browser().GetAsync("/account/login");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var html = await response.Content.ReadAsStringAsync();
        Assert.Contains("id=\"login-form\"", html);
        Assert.Contains("id=\"login-sso-error\"", html);
    }
}

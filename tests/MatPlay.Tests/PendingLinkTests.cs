using System.Net;
using System.Text.Json;
using MatPlatform;
using MatPlay.Data;
using MatPlay.Services;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace MatPlay.Tests;

/// <summary>
/// Der Angriff über einen vorbelegten Benutzernamen: jemand registriert sich mit der Adresse eines anderen als Benutzernamen. Meldet sich
/// der andere dann über MatIdentity an, landet er nicht in diesem Konto, sondern auf der lokalen Anmeldung (<c>?local=1&amp;link=1</c>);
/// verknüpft wird erst, wenn sich genau dieses Konto mit seinem Passwort anmeldet.
/// </summary>
public sealed class PendingLinkTests(IdentityFactory site) : IClassFixture<IdentityFactory>
{
    private const string LinkPage = "/account/login?local=1&link=1";

    [Fact]
    public async Task A_pre_claimed_address_is_not_entered_and_the_person_lands_on_the_local_sign_in()
    {
        // Der Angreifer registriert sich mit der Adresse des Opfers als Benutzernamen.
        var attacker = site.Browser();
        var register = await attacker.GetStringAsync("/account/register");
        var registered = await attacker.PostAsync("/account/register", new FormUrlEncodedContent(new Dictionary<string, string>
        {
            ["__RequestVerificationToken"] = Forms.Token(register),
            ["Username"] = "victim@example.com",
            ["DisplayName"] = "Victim",
            ["Password"] = "attacker-pw",
            ["PasswordRepeat"] = "attacker-pw",
        }));
        Assert.Equal(HttpStatusCode.Redirect, registered.StatusCode);
        var account = await UserAsync("victim@example.com");

        // Das Opfer meldet sich zum ersten Mal über MatIdentity an.
        var victim = site.Browser();
        var callback = await victim.GetAsync("/test/signin-oidc?sub=sub-victim&email=victim@example.com&name=Victim");

        Assert.Equal(HttpStatusCode.Redirect, callback.StatusCode);
        Assert.Equal(LinkPage, callback.Headers.Location!.OriginalString);
        Assert.Null((await UserAsync("victim@example.com")).ExternalSubject);
        Assert.Equal(1, await SessionsOfAsync(account.Id));
        Assert.False(await UsersAsync(u => u.ExternalSubject == "sub-victim"));

        var page = WebUtility.HtmlDecode(await (await victim.GetAsync(LinkPage)).Content.ReadAsStringAsync());
        Assert.Contains("id=\"login-link\"", page);
        Assert.Contains("victim@example.com", page);
        Assert.Contains("id=\"login-form\"", page);

        // Weiter zu MatIdentity bringt nichts anderes: wieder die lokale Anmeldung, kein zweites Konto.
        var again = await victim.GetAsync("/test/signin-oidc?sub=sub-victim&email=victim@example.com&name=Victim");
        Assert.Equal(LinkPage, again.Headers.Location!.OriginalString);
        Assert.Equal(1, await CountAsync(u => u.Username.ToLower() == "victim@example.com" || u.ExternalSubject == "sub-victim"));
    }

    [Fact]
    public async Task The_local_sign_in_of_the_matched_account_links_it_with_the_role_and_the_id_token()
    {
        var account = await AddUserAsync("link@example.com", "link-pw");
        var browser = site.Browser();
        Assert.Equal(LinkPage, (await browser.GetAsync("/test/signin-oidc?sub=sub-link&email=Link@Example.com&role=admin")).Headers.Location!.OriginalString);

        var signedIn = await SignInAsync(browser, "link@example.com", "link-pw");

        Assert.Equal(HttpStatusCode.Redirect, signedIn.StatusCode);
        Assert.Equal("/", signedIn.Headers.Location!.OriginalString);
        var linked = await UserAsync("link@example.com");
        Assert.Equal("sub-link", linked.ExternalSubject);
        Assert.Equal(Roles.Admin, linked.Role);
        await using (var scope = site.Services.CreateAsyncScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            Assert.True(await db.UserSessions.AnyAsync(s => s.UserId == account.Id && s.IdentityToken == "the.id.token"));
        }

        // Die ausstehende Verknüpfung ist verbraucht; die nächste Anmeldung über MatIdentity findet das Konto über sub.
        Assert.DoesNotContain("id=\"login-link\"", await browser.GetStringAsync(LinkPage));
        var bySub = await site.Browser().GetAsync("/test/signin-oidc?sub=sub-link&email=link@example.com");
        Assert.Equal("/", bySub.Headers.Location!.OriginalString);
    }

    [Fact]
    public async Task Another_account_signing_in_links_nothing_and_the_link_is_gone()
    {
        await AddUserAsync("other@example.com", "other-pw");
        await AddUserAsync("someone-else", "else-pw");
        var browser = site.Browser();
        await browser.GetAsync("/test/signin-oidc?sub=sub-other&email=other@example.com");

        Assert.Equal(HttpStatusCode.Redirect, (await SignInAsync(browser, "someone-else", "else-pw")).StatusCode);

        Assert.Null((await UserAsync("someone-else")).ExternalSubject);
        Assert.Null((await UserAsync("other@example.com")).ExternalSubject);

        // Auch das passende Konto bekommt die Verknüpfung danach nicht mehr.
        await SignInAsync(browser, "other@example.com", "other-pw");
        Assert.Null((await UserAsync("other@example.com")).ExternalSubject);
        Assert.False(await UsersAsync(u => u.ExternalSubject == "sub-other"));
    }

    [Fact]
    public async Task Cancel_deletes_the_pending_link()
    {
        await AddUserAsync("cancel@example.com", "cancel-pw");
        var browser = site.Browser();
        await browser.GetAsync("/test/signin-oidc?sub=sub-cancel&email=cancel@example.com");

        var page = await browser.GetStringAsync(LinkPage);
        var cancelled = await browser.PostAsync(LinkPage + "&handler=CancelLink", new FormUrlEncodedContent(new Dictionary<string, string>
        {
            ["__RequestVerificationToken"] = Forms.Token(page),
        }));

        Assert.Equal(HttpStatusCode.Redirect, cancelled.StatusCode);
        Assert.Equal("/account/login?local=1", cancelled.Headers.Location!.OriginalString);
        Assert.DoesNotContain("id=\"login-link\"", await browser.GetStringAsync(LinkPage));

        await SignInAsync(browser, "cancel@example.com", "cancel-pw");
        Assert.Null((await UserAsync("cancel@example.com")).ExternalSubject);
    }

    [Fact]
    public async Task A_forged_or_expired_pending_link_is_ignored()
    {
        var account = await AddUserAsync("forged@example.com", "forged-pw");
        var json = JsonSerializer.Serialize(
            new MatIdentityPendingLink("sub-forged", "forged@example.com", null, null, "admin", null, account.Id.ToString(System.Globalization.CultureInfo.InvariantCulture)),
            new JsonSerializerOptions(JsonSerializerDefaults.Web));
        var protector = site.Services.GetRequiredService<IDataProtectionProvider>().CreateProtector(MatIdentityPendingLink.Purpose).ToTimeLimitedDataProtector();

        foreach (var value in new[] { json, "CfDJ8forged", protector.Protect(json, DateTimeOffset.UtcNow.AddSeconds(-1)) })
        {
            var browser = site.Browser();
            await browser.GetAsync("/test/pending-link?value=" + Uri.EscapeDataString(value));

            Assert.DoesNotContain("id=\"login-link\"", await browser.GetStringAsync(LinkPage));
            Assert.Equal(HttpStatusCode.Redirect, (await SignInAsync(browser, "forged@example.com", "forged-pw")).StatusCode);
            Assert.Null((await UserAsync("forged@example.com")).ExternalSubject);
        }

        // Gegenprobe: derselbe Inhalt mit Restzeit wird gelesen und verknüpft.
        var valid = site.Browser();
        await valid.GetAsync("/test/pending-link?value=" + Uri.EscapeDataString(protector.Protect(json, DateTimeOffset.UtcNow.AddMinutes(5))));
        Assert.Contains("id=\"login-link\"", await valid.GetStringAsync(LinkPage));
        await SignInAsync(valid, "forged@example.com", "forged-pw");
        Assert.Equal("sub-forged", (await UserAsync("forged@example.com")).ExternalSubject);
    }

    private async Task<HttpResponseMessage> SignInAsync(HttpClient browser, string username, string password)
    {
        var page = await browser.GetStringAsync(LinkPage);
        return await browser.PostAsync(LinkPage, new FormUrlEncodedContent(new Dictionary<string, string>
        {
            ["__RequestVerificationToken"] = Forms.Token(page),
            ["Username"] = username,
            ["Password"] = password,
        }));
    }

    private async Task<User> AddUserAsync(string username, string password)
    {
        await using var scope = site.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var user = new User { Username = username, DisplayName = username, PasswordHash = PasswordHasher.Hash(password), Role = Roles.User };
        db.Users.Add(user);
        await db.SaveChangesAsync();
        return user;
    }

    private async Task<User> UserAsync(string username)
    {
        await using var scope = site.Services.CreateAsyncScope();
        return await scope.ServiceProvider.GetRequiredService<AppDbContext>().Users.AsNoTracking().SingleAsync(u => u.Username == username);
    }

    private async Task<bool> UsersAsync(System.Linq.Expressions.Expression<Func<User, bool>> where)
    {
        await using var scope = site.Services.CreateAsyncScope();
        return await scope.ServiceProvider.GetRequiredService<AppDbContext>().Users.AnyAsync(where);
    }

    private async Task<int> CountAsync(System.Linq.Expressions.Expression<Func<User, bool>> where)
    {
        await using var scope = site.Services.CreateAsyncScope();
        return await scope.ServiceProvider.GetRequiredService<AppDbContext>().Users.CountAsync(where);
    }

    private async Task<int> SessionsOfAsync(long userId)
    {
        await using var scope = site.Services.CreateAsyncScope();
        return await scope.ServiceProvider.GetRequiredService<AppDbContext>().UserSessions.CountAsync(s => s.UserId == userId);
    }
}

using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using MatPlay.Data;
using MatPlay.Services;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace MatPlay.Tests;

/// <summary>Der Plattform-Vertrag aus MatDesign docs/PLATFORM.md, wie MatPlay ihn beantwortet: Formen, Statuscodes, Zugriff.</summary>
public sealed class PlatformApiTests(PlayFactory site) : IClassFixture<PlayFactory>
{
    private static readonly string[] Protected = ["/api/state", "/api/state/games", "/api/configure"];

    [Fact]
    public async Task Health_answers_without_a_key()
    {
        var response = await site.CreateClient().GetAsync("/api/health");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        using var doc = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        Assert.Equal("ok", doc.RootElement.GetProperty("status").GetString());
        Assert.Equal("matplay", doc.RootElement.GetProperty("app").GetString());
        Assert.False(string.IsNullOrEmpty(doc.RootElement.GetProperty("version").GetString()));
        Assert.Contains(doc.RootElement.GetProperty("checks").EnumerateArray(),
            c => c.GetProperty("name").GetString() == "database" && c.GetProperty("status").GetString() == "ok");
    }

    [Fact]
    public async Task Calls_of_the_platform_api_without_a_cookie_leave_no_sessions_behind()
    {
        var before = await CountSessionsAsync();
        var client = site.CreateClient();
        for (var i = 0; i < 5; i++)
        {
            Assert.Equal(HttpStatusCode.OK, (await client.GetAsync("/api/health")).StatusCode);
            Assert.Equal(HttpStatusCode.OK, (await client.GetAsync("/api")).StatusCode);
            Assert.Equal(HttpStatusCode.OK, (await site.Api().GetAsync("/api/state")).StatusCode);
        }

        Assert.Equal(before, await CountSessionsAsync());
    }

    [Fact]
    public async Task The_definition_answers_without_a_key_and_with_an_etag()
    {
        var client = site.CreateClient();
        var response = await client.GetAsync("/api");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal("application/json", response.Content.Headers.ContentType?.MediaType);
        var etag = response.Headers.ETag!.Tag;

        using var request = new HttpRequestMessage(HttpMethod.Get, "/api");
        request.Headers.TryAddWithoutValidation("If-None-Match", etag);
        Assert.Equal(HttpStatusCode.NotModified, (await client.SendAsync(request)).StatusCode);

        using var doc = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        var root = doc.RootElement;
        Assert.Equal(1, root.GetProperty("contract").GetInt32());
        var app = root.GetProperty("app");
        Assert.Equal("matplay", app.GetProperty("id").GetString());
        Assert.Equal("MatPlay", app.GetProperty("name").GetString());
        Assert.Equal("#00819b", app.GetProperty("accent").GetString());
        Assert.Equal("/", app.GetProperty("home").GetString());
        Assert.True(root.GetProperty("auth").GetProperty("apiKey").GetBoolean());
        Assert.False(root.GetProperty("auth").TryGetProperty("identity", out _));

        Assert.Equal(["overview", "settings"], root.GetProperty("pages").EnumerateArray().Select(p => p.GetProperty("id").GetString()));
        Assert.Equal(
            [("admin", "Administrator"), ("user", "Benutzer")],
            root.GetProperty("roles").EnumerateArray().Select(r => (r.GetProperty("key").GetString(), r.GetProperty("label").GetString())));

        var openUsers = root.GetProperty("actions").EnumerateArray().Single(a => a.GetProperty("id").GetString() == "openUsers");
        Assert.Equal("link", openUsers.GetProperty("kind").GetString());
        Assert.Equal("/admin/users", openUsers.GetProperty("url").GetString());

        // Das Icon der App gibt es wirklich.
        Assert.True(File.Exists(Path.Combine(Repo.WebRoot(), app.GetProperty("icon").GetString()!.TrimStart('/'))));
        Assert.Equal(HttpStatusCode.OK, (await client.GetAsync(app.GetProperty("icon").GetString())).StatusCode);
    }

    [Fact]
    public async Task The_definition_follows_accept_language()
    {
        var client = site.CreateClient();
        using var german = new HttpRequestMessage(HttpMethod.Get, "/api");
        german.Headers.TryAddWithoutValidation("Accept-Language", "de-DE");
        using var english = new HttpRequestMessage(HttpMethod.Get, "/api");
        english.Headers.TryAddWithoutValidation("Accept-Language", "en-US,en;q=0.9");

        using var de = JsonDocument.Parse(await (await client.SendAsync(german)).Content.ReadAsStringAsync());
        using var en = JsonDocument.Parse(await (await client.SendAsync(english)).Content.ReadAsStringAsync());
        Assert.Equal("Übersicht", de.RootElement.GetProperty("pages")[0].GetProperty("label").GetString());
        Assert.Equal("Overview", en.RootElement.GetProperty("pages")[0].GetProperty("label").GetString());
        Assert.Equal("Letzter Spieleabend", de.RootElement.GetProperty("state").EnumerateArray().Single(s => s.GetProperty("key").GetString() == "lastGameNight").GetProperty("label").GetString());
        Assert.Equal("Last game night", en.RootElement.GetProperty("state").EnumerateArray().Single(s => s.GetProperty("key").GetString() == "lastGameNight").GetProperty("label").GetString());
    }

    [Fact]
    public async Task State_configure_and_actions_need_the_key()
    {
        var anonymous = site.CreateClient();
        foreach (var path in Protected)
        {
            var response = await anonymous.GetAsync(path);
            Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
            Assert.Equal("application/json", response.Content.Headers.ContentType?.MediaType);
        }

        Assert.Equal(HttpStatusCode.Unauthorized, (await anonymous.PutAsJsonAsync("/api/configure", new { values = new { allowRegistration = false } })).StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, (await anonymous.PostAsync("/api/actions/cleanupSessions", null)).StatusCode);

        var wrong = site.CreateClient();
        wrong.DefaultRequestHeaders.Add("X-Api-Key", "wrong");
        Assert.Equal(HttpStatusCode.Unauthorized, (await wrong.GetAsync("/api/state")).StatusCode);

        // Ein Bearer-Token ist kein Weg hinein, solange MatIdentity nicht eingerichtet ist.
        var bearer = site.CreateClient();
        bearer.DefaultRequestHeaders.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", "abc.def.ghi");
        Assert.Equal(HttpStatusCode.Unauthorized, (await bearer.GetAsync("/api/state")).StatusCode);

        // Die Spiel-API über den Share-Link bleibt davon unberührt (unbekanntes Spiel: 404 wie bisher).
        Assert.Equal(HttpStatusCode.NotFound, (await anonymous.GetAsync("/api/play/unknown/state")).StatusCode);
    }

    [Fact]
    public async Task State_answers_every_tile_with_real_numbers_and_one_value_by_key()
    {
        var api = site.Api();
        using (var empty = JsonDocument.Parse(await api.GetStringAsync("/api/state")))
        {
            var values = empty.RootElement.GetProperty("values");
            Assert.True(empty.RootElement.TryGetProperty("time", out _));
            using var definition = JsonDocument.Parse(await api.GetStringAsync("/api"));
            foreach (var key in definition.RootElement.GetProperty("state").EnumerateArray().Select(s => s.GetProperty("key").GetString()!))
            {
                Assert.True(values.TryGetProperty(key, out var value), $"No value for {key}");
                Assert.True(value.TryGetProperty("value", out _), $"{key} has no value field");
            }
        }

        // Zwei Skyjo-Abende und ein beendetes Kniffel, über den GameService wie in der App.
        using (var scope = site.Services.CreateScope())
        {
            var games = scope.ServiceProvider.GetRequiredService<GameService>();
            await games.CreateAsync("Skyjo 1", "counter", "{}", ["Anna", "Ben"], savePlayers: false, presetKey: "skyjo");
            await games.CreateAsync("Skyjo 2", "counter", "{}", ["Anna", "Ben", "Cem"], savePlayers: false, presetKey: "skyjo");
            var kniffel = await games.CreateAsync("Kniffel", "kniffel", "{}", ["Anna"], savePlayers: false, presetKey: "kniffel");
            await games.SetStatusAsync(kniffel, GameStatus.Finished);
        }

        using (var state = JsonDocument.Parse(await api.GetStringAsync("/api/state")))
        {
            var values = state.RootElement.GetProperty("values");
            Assert.True(values.GetProperty("games").GetProperty("value").GetInt32() >= 3);
            Assert.True(values.GetProperty("runningGames").GetProperty("value").GetInt32() >= 2);
            Assert.True(values.GetProperty("gamesLast30Days").GetProperty("value").GetInt32() >= 3);
            Assert.True(values.GetProperty("accounts").GetProperty("value").GetInt32() >= 1);
            Assert.Equal("Skyjo", values.GetProperty("topGame").GetProperty("value").GetString());
            var last = values.GetProperty("lastGameNight").GetProperty("value");
            Assert.Equal(JsonValueKind.String, last.ValueKind);
            Assert.True(DateTimeOffset.Parse(last.GetString()!) > DateTimeOffset.UtcNow.AddMinutes(-5));
        }

        var one = await api.GetAsync("/api/state/games");
        Assert.Equal(HttpStatusCode.OK, one.StatusCode);
        using (var doc = JsonDocument.Parse(await one.Content.ReadAsStringAsync()))
        {
            Assert.Equal("games", doc.RootElement.GetProperty("key").GetString());
            Assert.True(doc.RootElement.GetProperty("value").GetInt32() >= 3);
        }

        var unknown = await api.GetAsync("/api/state/nothing");
        Assert.Equal(HttpStatusCode.NotFound, unknown.StatusCode);
        using (var doc = JsonDocument.Parse(await unknown.Content.ReadAsStringAsync()))
        {
            Assert.True(doc.RootElement.GetProperty("errors").TryGetProperty("nothing", out _));
        }
    }

    [Fact]
    public async Task Configure_checks_every_value_and_saves_nothing_when_one_is_wrong()
    {
        var api = site.Api();

        var bad = await api.PutAsJsonAsync("/api/configure", new { values = new { allowRegistration = false, playerCodeLength = 0, nothing = 1 } });
        Assert.Equal(HttpStatusCode.BadRequest, bad.StatusCode);
        using (var errors = JsonDocument.Parse(await bad.Content.ReadAsStringAsync()))
        {
            var byKey = errors.RootElement.GetProperty("errors");
            Assert.True(byKey.TryGetProperty("nothing", out _));
            Assert.True(byKey.TryGetProperty("playerCodeLength", out _));
        }

        using (var unchanged = JsonDocument.Parse(await api.GetStringAsync("/api/configure")))
        {
            Assert.True(unchanged.RootElement.GetProperty("values").GetProperty("allowRegistration").GetBoolean());
            Assert.Equal(2, unchanged.RootElement.GetProperty("values").GetProperty("playerCodeLength").GetInt32());
        }

        foreach (var wrong in new object[]
                 {
                     new { playerCodeLength = 5 },
                     new { playerCodeLength = 1.5 },
                     new { playerCodeLength = "3" },
                     new { allowRegistration = "yes" },
                     new { appName = "x" },
                 })
        {
            var response = await api.PutAsJsonAsync("/api/configure", new { values = wrong });
            Assert.True(response.StatusCode == HttpStatusCode.BadRequest, $"{JsonSerializer.Serialize(wrong)}: {(int)response.StatusCode}");
        }

        var good = await api.PutAsJsonAsync("/api/configure", new { values = new { allowRegistration = false, playerCodeLength = 3 } });
        Assert.Equal(HttpStatusCode.OK, good.StatusCode);
        using (var saved = JsonDocument.Parse(await good.Content.ReadAsStringAsync()))
        {
            var values = saved.RootElement.GetProperty("values");
            Assert.False(values.GetProperty("allowRegistration").GetBoolean());
            Assert.Equal(3, values.GetProperty("playerCodeLength").GetInt32());
        }

        // Dieselben Werte sieht die eigene Einstellungsseite (AppConfigService); die Registrierung ist jetzt zu.
        var config = site.Services.GetRequiredService<AppConfigService>().Config;
        Assert.False(config.AllowRegistration);
        Assert.Equal(3, config.PlayerCodeLength);
        Assert.Equal(HttpStatusCode.NotFound, (await site.Browser().GetAsync("/account/register")).StatusCode);

        // Zurück auf die Standardwerte, damit die anderen Tests die übliche Installation sehen.
        var reset = await api.PutAsJsonAsync("/api/configure", new { values = new { allowRegistration = true, playerCodeLength = 2 } });
        Assert.Equal(HttpStatusCode.OK, reset.StatusCode);
    }

    [Fact]
    public async Task Configure_answers_every_field_and_never_a_secret()
    {
        var api = site.Api();
        using var definition = JsonDocument.Parse(await api.GetStringAsync("/api"));
        using var configuration = JsonDocument.Parse(await api.GetStringAsync("/api/configure"));
        var values = configuration.RootElement.GetProperty("values");

        foreach (var field in definition.RootElement.GetProperty("configure").EnumerateArray())
        {
            var key = field.GetProperty("key").GetString()!;
            Assert.True(values.TryGetProperty(key, out var value), $"No value for {key}");
            if (field.GetProperty("type").GetString() == "secret")
            {
                Assert.Equal(JsonValueKind.Object, value.ValueKind);
                Assert.Equal(["isSet"], value.EnumerateObject().Select(p => p.Name));
            }
        }

        // Weder der Schlüssel noch ein Passwort-Hash verlassen die App.
        foreach (var text in new[] { configuration.RootElement.GetRawText(), definition.RootElement.GetRawText(), await api.GetStringAsync("/api/state"), await api.GetStringAsync("/api/health") })
        {
            Assert.DoesNotContain(PlayFactory.ApiKey, text);
            Assert.DoesNotContain("100000.", text);
        }
    }

    [Fact]
    public async Task Actions_run_and_an_unknown_one_is_404()
    {
        // Eine abgelaufene Session, die das Aufräumen löscht.
        using (var scope = site.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            db.UserSessions.Add(new UserSession { ExpiresDate = DateTime.UtcNow.AddDays(-1) });
            await db.SaveChangesAsync();
        }

        var api = site.Api();
        var cleanup = await api.PostAsync("/api/actions/cleanupSessions", null);
        Assert.Equal(HttpStatusCode.OK, cleanup.StatusCode);
        using (var doc = JsonDocument.Parse(await cleanup.Content.ReadAsStringAsync()))
        {
            Assert.True(doc.RootElement.GetProperty("ok").GetBoolean());
            Assert.StartsWith("1 ", doc.RootElement.GetProperty("message").GetString());
        }

        using (var scope = site.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            Assert.False(await db.UserSessions.AnyAsync(s => s.ExpiresDate <= DateTime.UtcNow));
        }

        Assert.Equal(HttpStatusCode.NotFound, (await api.PostAsync("/api/actions/nothing", null)).StatusCode);
        // Eine Link-Aktion ist kein POST.
        Assert.Equal(HttpStatusCode.NotFound, (await api.PostAsync("/api/actions/openUsers", null)).StatusCode);
    }

    private async Task<int> CountSessionsAsync()
    {
        using var scope = site.Services.CreateScope();
        return await scope.ServiceProvider.GetRequiredService<AppDbContext>().UserSessions.CountAsync();
    }
}

/// <summary>Ohne <c>MAT_API_KEY</c> ist der Schlüssel aus: die geschützten Endpunkte antworten jedem Schlüssel mit 401.</summary>
public sealed class PlatformWithoutKeyTests(NoKeyFactory site) : IClassFixture<NoKeyFactory>
{
    [Fact]
    public async Task Every_protected_endpoint_is_401_and_the_definition_says_so()
    {
        var client = site.CreateClient();
        client.DefaultRequestHeaders.Add("X-Api-Key", PlayFactory.ApiKey);
        Assert.Equal(HttpStatusCode.Unauthorized, (await client.GetAsync("/api/state")).StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, (await client.GetAsync("/api/configure")).StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, (await client.PostAsync("/api/actions/cleanupSessions", null)).StatusCode);

        using var doc = JsonDocument.Parse(await client.GetStringAsync("/api"));
        Assert.False(doc.RootElement.GetProperty("auth").GetProperty("apiKey").GetBoolean());
        Assert.Equal(HttpStatusCode.OK, (await client.GetAsync("/api/health")).StatusCode);
    }
}

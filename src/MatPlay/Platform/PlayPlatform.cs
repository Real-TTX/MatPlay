using System.Text.Json;
using MatPlatform;
using MatPlay.Services;

namespace MatPlay.Platform;

/// <summary>
/// MatPlay als App des Plattform-Vertrags (MatDesign docs/PLATFORM.md), auf dem gemeinsamen Paket MatPlatform (Ordner <c>MatPlatform/</c>, eine Kopie):
/// wie Mattiverse MatPlay zeigt (<see cref="Describe"/>: Kacheln, Einstellungen, Knöpfe), woher die Werte kommen und was die Knöpfe tun.
/// Die Einstellungen sind die beiden von /admin/settings. Kein DbContext hier (Web-Schicht): alles über die Services.
/// Texte deutsch, englisch mit <c>Accept-Language: en</c>.
/// </summary>
public sealed class PlayPlatform(
    AppConfigService appConfig,
    InstanceStatusService status,
    ILogger<PlayPlatform> logger) : IMatPlatformState, IMatPlatformConfiguration, IMatPlatformActions, IMatPlatformHealth
{
    /// <summary>Die Id von MatPlay in der Plattform (MatDesign AppRegistry.cs) und die Standard-Client-Id in MatIdentity.</summary>
    public const string AppId = "matplay";

    public const string Accent = "#00819b";

    public const int MinPlayerCodeLength = 1;
    public const int MaxPlayerCodeLength = 4;

    /// <summary>Die MatApiDefinition von MatPlay (<c>GET /api</c>); <c>auth</c> und <c>endpoints</c> ergänzt MatPlatform.</summary>
    public static void Describe(MatPlatformBuilder o)
    {
        var T = o.T;

        o.App(
            AppId,
            "MatPlay",
            T("Punkte zählen am Spieleabend", "Keeping score on game night"),
            AppVersion.Current,
            "/icons/favicon.svg",
            Accent);

        o.Page("overview", T("Übersicht", "Overview"), "dashboard",
            MatSection.Tiles("games", "runningGames", "gamesLast30Days", "players", "accounts", "topGame", "lastGameNight"),
            MatSection.Actions("openUsers", "openSettings", "cleanupSessions"));
        o.Page("settings", T("Einstellungen", "Settings"), "settings",
            MatSection.Form(T("Benutzer", "Users"), "allowRegistration"),
            MatSection.Form(T("Spielerprofile", "Player profiles"), "playerCodeLength"));

        o.Tile("games", T("Spiele", "Games"), icon: "gamepad");
        o.Tile("runningGames", T("Laufende Spiele", "Games in progress"), icon: "play");
        o.Tile("gamesLast30Days", T("Spiele der letzten 30 Tage", "Games in the last 30 days"), icon: "calendar");
        o.Tile("players", T("Spielerprofile", "Player profiles"), icon: "users");
        o.Tile("accounts", T("Benutzerkonten", "Accounts"), icon: "user");
        o.Tile("topGame", T("Meistgespieltes Spiel", "Most played game"), "text", icon: "star");
        o.Tile("lastGameNight", T("Letzter Spieleabend", "Last game night"), "time", icon: "clock");

        o.Field(new MatField("allowRegistration", T("Selbst-Registrierung erlauben", "Allow sign-up"), "bool")
        {
            Hint = T("Aus: Konten legt nur die Administration an. Spielen als Gast geht immer.",
                "Off: only administrators create accounts. Playing as a guest always works."),
        });
        o.Field(new MatField("playerCodeLength", T("Länge der automatischen Kürzel", "Length of the automatic player codes"), "number")
        {
            Required = true,
            Min = MinPlayerCodeLength,
            Max = MaxPlayerCodeLength,
            Hint = T("Gilt für neu angelegte Spieler; bestehende Kürzel bleiben.", "For new players; existing codes stay."),
        });

        o.LinkAction("openUsers", T("Benutzer verwalten", "Manage users"), "users", "/admin/users");
        o.LinkAction("openSettings", T("Alle Einstellungen", "All settings"), "settings", "/admin/settings");
        o.PostAction("cleanupSessions", T("Abgelaufene Sitzungen löschen", "Remove expired sessions"), "broom");

        o.Role("admin", "Administrator");
        o.Role("user", T("Benutzer", "User"));
    }

    public async Task<IReadOnlyList<MatHealthCheck>> CheckHealthAsync(MatPlatformCall call, CancellationToken ct)
    {
        if (!await status.CanReadDatabaseAsync(ct))
        {
            return [new MatHealthCheck("database", "down", call.T("Die Datenbank kann nicht gelesen werden.", "The database cannot be read."))];
        }

        var checks = new List<MatHealthCheck> { new("database", "ok") };
        if (await status.CountActiveAdminsAsync(ct) == 0)
        {
            checks.Add(new MatHealthCheck("administrators", "degraded", call.T("Kein aktiver Administrator.", "No active administrator.")));
        }

        return checks;
    }

    public async Task<IReadOnlyDictionary<string, MatStateValue>> GetStateAsync(MatPlatformCall call, CancellationToken ct)
    {
        var snapshot = await status.GetSnapshotAsync(ct);
        return new Dictionary<string, MatStateValue>
        {
            ["games"] = new(snapshot.Games),
            ["runningGames"] = new(snapshot.RunningGames),
            ["gamesLast30Days"] = new(snapshot.GamesLast30Days),
            ["players"] = new(snapshot.SavedPlayers),
            ["accounts"] = new(snapshot.Accounts),
            ["topGame"] = new(snapshot.TopGame),
            ["lastGameNight"] = new(snapshot.LastPlayedUtc is { } last
                ? new DateTimeOffset(DateTime.SpecifyKind(last, DateTimeKind.Utc))
                : null),
        };
    }

    public Task<IReadOnlyDictionary<string, object?>> GetConfigurationAsync(MatPlatformCall call, CancellationToken ct)
    {
        var config = appConfig.Config;
        IReadOnlyDictionary<string, object?> values = new Dictionary<string, object?>
        {
            ["allowRegistration"] = config.AllowRegistration,
            ["playerCodeLength"] = config.PlayerCodeLength,
        };
        return Task.FromResult(values);
    }

    public Task<IReadOnlyDictionary<string, string>> ConfigureAsync(IReadOnlyDictionary<string, JsonElement> values, MatPlatformCall call, CancellationToken ct)
    {
        var previous = appConfig.Config;
        var next = new AppConfig
        {
            AllowRegistration = previous.AllowRegistration,
            AppName = previous.AppName,
            PlayerCodeLength = previous.PlayerCodeLength,
        };
        var errors = new Dictionary<string, string>();

        // Typ, Pflicht und Grenzen prüft MatPlatform vorher anhand der Definition; hier nur noch, was sie nicht sagt (ganze Zahl).
        foreach (var (key, value) in values)
        {
            switch (key)
            {
                case "allowRegistration" when value.ValueKind is JsonValueKind.True or JsonValueKind.False:
                    next.AllowRegistration = value.GetBoolean();
                    break;
                case "playerCodeLength" when value.ValueKind == JsonValueKind.Number && value.TryGetInt32(out var length)
                                             && length is >= MinPlayerCodeLength and <= MaxPlayerCodeLength:
                    next.PlayerCodeLength = length;
                    break;
                case "allowRegistration":
                    errors[key] = call.T("Bitte true oder false angeben.", "Send true or false.");
                    break;
                case "playerCodeLength":
                    errors[key] = call.T($"Bitte eine ganze Zahl von {MinPlayerCodeLength} bis {MaxPlayerCodeLength} angeben.",
                        $"Send a whole number from {MinPlayerCodeLength} to {MaxPlayerCodeLength}.");
                    break;
                default:
                    errors[key] = call.T("Diese Einstellung gibt es nicht.", "There is no such setting.");
                    break;
            }
        }

        if (errors.Count == 0)
        {
            appConfig.Save(next);
            // MatPlay hat kein Aktivitätsprotokoll: die Änderung mit dem Aufrufer ins Log.
            logger.LogInformation(
                "Einstellungen über die Plattform-API geändert von {Caller}: Registrierung {RegistrationBefore} -> {RegistrationAfter}, Kürzel-Länge {LengthBefore} -> {LengthAfter}",
                call.Caller, previous.AllowRegistration, next.AllowRegistration, previous.PlayerCodeLength, next.PlayerCodeLength);
        }

        return Task.FromResult<IReadOnlyDictionary<string, string>>(errors);
    }

    public async Task<MatActionResult?> RunActionAsync(string id, MatPlatformCall call, CancellationToken ct)
    {
        switch (id)
        {
            case "cleanupSessions":
                var removed = await status.CleanupExpiredSessionsAsync(ct);
                logger.LogInformation("{Removed} abgelaufene Sitzungen über die Plattform-API gelöscht ({Caller})", removed, call.Caller);
                return new MatActionResult(true, removed == 1
                    ? call.T("1 abgelaufene Sitzung gelöscht.", "1 expired session removed.")
                    : call.T($"{removed} abgelaufene Sitzungen gelöscht.", $"{removed} expired sessions removed."));
            default:
                return null;
        }
    }
}

using System.Text.Json;
using MatPlatform;
using MatPlay.Data;
using MatPlay.Platform;
using MatPlay.Services;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.EntityFrameworkCore;

var builder = WebApplication.CreateBuilder(args);

// Datenverzeichnis (Volume im Container): DB, Configs
// "appdata" statt "data", um lokal keine Namenskollision mit dem Quellordner Data/ zu haben
var dataDir = Environment.GetEnvironmentVariable("MATPLAY_DATA")
              ?? Path.Combine(builder.Environment.ContentRootPath, "appdata");
var dbDir = Path.Combine(dataDir, "db");
Directory.CreateDirectory(dbDir);

var appVersion = AppVersion.Current;

builder.Services.AddRazorPages(options =>
{
    options.Conventions.AuthorizeFolder("/Admin", "AdminOnly");
});

// Schlüssel im Volume persistieren, damit Antiforgery-Tokens Container-Restarts überleben
builder.Services.AddDataProtection()
    .PersistKeysToFileSystem(new DirectoryInfo(Path.Combine(dataDir, "keys")))
    .SetApplicationName("MatPlay");

builder.Services.AddDbContext<AppDbContext>(o =>
    o.UseSqlite($"Data Source={Path.Combine(dbDir, "matplay.db")}"));

builder.Services.AddHttpContextAccessor();
builder.Services.AddScoped<CurrentContext>();
builder.Services.AddScoped<AuthService>();
builder.Services.AddScoped<GameService>();
builder.Services.AddScoped<SavedPlayerService>();
builder.Services.AddScoped<InstanceStatusService>();
builder.Services.AddScoped<ExternalSignInService>();
var appConfigService = new AppConfigService(dataDir);
builder.Services.AddSingleton(appConfigService);
builder.Services.AddSingleton(new AppInfo(appVersion));

// Plattform-Vertrag (MatDesign docs/PLATFORM.md): MAT_API_KEY und optional MATIDENTITY_*.
// Das Paket MatPlatform (Kopie in MatPlatform/) stellt die Endpunkte bereit; PlayPlatform beschreibt MatPlay und liefert die Werte.
var platform = builder.AddMatPlatform<PlayPlatform>(PlayPlatform.Describe);

builder.Services
    .AddAuthentication(SessionAuthDefaults.Scheme)
    .AddScheme<AuthenticationSchemeOptions, SessionAuthHandler>(SessionAuthDefaults.Scheme, null)
    // Anmeldung über MatIdentity, nur mit MATIDENTITY_AUTHORITY (ohne die Variable ändert sich nichts).
    // PlayIdentityUsers meldet in der MatPlay-Session an; das Schema der Session ist dafür das "Cookie-Schema".
    .AddMatIdentitySignIn<PlayIdentityUsers>(platform, o =>
    {
        o.CookieScheme = SessionAuthDefaults.Scheme;
        o.LoginPath = "/account/login";
        // Abgelehnt (z.B. gelöschtes Konto): zurück zur lokalen Anmeldung mit Hinweis
        o.AccessDeniedPath = "/account/login?local=1&ssoError=1";
    });

builder.Services.AddAuthorization(options =>
{
    options.AddPolicy("AdminOnly", p => p.RequireRole(Roles.Admin));
    options.AddPolicy("UserOnly", p => p.RequireRole(Roles.Admin, Roles.User));
});

var app = builder.Build();

// Schema anlegen + Admin-Seed
using (var scope = app.Services.CreateScope())
{
    var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
    DbInitializer.Initialize(db, appConfigService.Config.PlayerCodeLength, app.Logger);
}

app.UseStaticFiles();
app.UseRouting();
app.UseRateLimiter();
app.UseAuthentication();
app.UseAuthorization();

// Erzwungene Passwort-Änderung (z.B. Erst-Login des Seed-Admins); nicht nach einer Anmeldung über MatIdentity
app.Use(async (ctx, next) =>
{
    var current = ctx.RequestServices.GetRequiredService<CurrentContext>();
    var path = ctx.Request.Path;
    if (current.User?.MustChangePassword == true
        && !current.SignedInByIdentity
        && !path.StartsWithSegments("/account/change-password")
        && !path.StartsWithSegments("/account/logout")
        && !path.StartsWithSegments("/api"))
    {
        ctx.Response.Redirect("/account/change-password");
        return;
    }
    await next();
});

app.MapRazorPages();

// ---- Plattform-API: /api (Definition), /api/health, /api/state, /api/configure, /api/actions/{id} ----
app.MapMatPlatform();

// ---- Play-API (Zugriff über Share-Token = Link-Freigabe, auch anonym) ----
var play = app.MapGroup("/api/play/{token}");

play.MapGet("/state", async (string token, GameService games) =>
{
    var game = await games.GetByTokenAsync(token);
    if (game == null) return Results.NotFound();
    var players = await games.GetPlayersAsync(game.Id);
    var scores = await games.GetScoresAsync(game.Id);
    return Results.Json(new
    {
        version = game.Version,
        name = game.Name,
        moduleKey = game.ModuleKey,
        status = game.Status,
        config = JsonDocument.Parse(game.ConfigJson).RootElement,
        players = players.Select(p => new
        {
            id = p.Id,
            name = p.Name,
            state = JsonDocument.Parse(string.IsNullOrEmpty(p.StateJson) ? "{}" : p.StateJson).RootElement,
        }),
        // Kind=Utc erzwingen, damit der Client die Zeit korrekt in Lokalzeit umrechnet
        scores = scores.Select(e => new
        {
            id = e.Id, playerId = e.PlayerId, value = e.Value, round = e.Round,
            date = DateTime.SpecifyKind(e.CreateDate, DateTimeKind.Utc),
        }),
    });
});

play.MapPost("/score", async (string token, ScoreRequest req, GameService games) =>
{
    var game = await games.GetByTokenAsync(token);
    if (game == null) return Results.NotFound();
    if (game.Status != GameStatus.Running) return Results.BadRequest();
    await games.AddScoreAsync(game, req.PlayerId, req.Value, req.Round);
    return Results.Json(new { version = game.Version });
});

play.MapPost("/undo", async (string token, UndoRequest req, GameService games) =>
{
    var game = await games.GetByTokenAsync(token);
    if (game == null) return Results.NotFound();
    var ok = await games.UndoLastScoreAsync(game, req.PlayerId);
    return Results.Json(new { ok, version = game.Version });
});

play.MapPost("/player-state", async (string token, PlayerStateRequest req, GameService games) =>
{
    var game = await games.GetByTokenAsync(token);
    if (game == null) return Results.NotFound();
    if (game.Status != GameStatus.Running) return Results.BadRequest();
    await games.SetPlayerStateAsync(game, req.PlayerId, req.State.GetRawText());
    return Results.Json(new { version = game.Version });
});

play.MapPost("/config", async (string token, ConfigRequest req, GameService games) =>
{
    var game = await games.GetByTokenAsync(token);
    if (game == null) return Results.NotFound();
    await games.SetConfigAsync(game, req.Config.GetRawText());
    return Results.Json(new { version = game.Version });
});

play.MapPost("/status", async (string token, StatusRequest req, GameService games) =>
{
    var game = await games.GetByTokenAsync(token);
    if (game == null) return Results.NotFound();
    await games.SetStatusAsync(game, req.Status);
    return Results.Json(new { version = game.Version });
});

app.Run();

public record ScoreRequest(long PlayerId, int Value, int Round);
public record UndoRequest(long? PlayerId);
public record PlayerStateRequest(long PlayerId, JsonElement State);
public record ConfigRequest(JsonElement Config);
public record StatusRequest(int Status);

public record AppInfo(string Version);

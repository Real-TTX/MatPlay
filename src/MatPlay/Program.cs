using System.Text.Json;
using MatPlay.Data;
using MatPlay.Services;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.EntityFrameworkCore;

var builder = WebApplication.CreateBuilder(args);

// Datenverzeichnis (Volume im Container): DB, Configs
var dataDir = Environment.GetEnvironmentVariable("MATPLAY_DATA")
              ?? Path.Combine(builder.Environment.ContentRootPath, "data");
var dbDir = Path.Combine(dataDir, "db");
Directory.CreateDirectory(dbDir);

var appVersion = Environment.GetEnvironmentVariable("APP_VERSION")
                 ?? $"local-{DateTime.UtcNow:yyyyMMdd}";

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
builder.Services.AddSingleton(new AppConfigService(dataDir));
builder.Services.AddSingleton(new AppInfo(appVersion));

builder.Services
    .AddAuthentication(SessionAuthDefaults.Scheme)
    .AddScheme<AuthenticationSchemeOptions, SessionAuthHandler>(SessionAuthDefaults.Scheme, null);

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
    db.Database.EnsureCreated();

    // Mini-Schema-Upgrade für bestehende Datenbanken (EnsureCreated legt neue Tabellen nicht nach)
    db.Database.ExecuteSqlRaw("""
        CREATE TABLE IF NOT EXISTS "UserFavorite" (
            "Id" INTEGER NOT NULL CONSTRAINT "PK_UserFavorite" PRIMARY KEY AUTOINCREMENT,
            "CreateDate" TEXT NOT NULL,
            "CreateUserId" INTEGER NULL,
            "UpdateDate" TEXT NOT NULL,
            "UpdateUserId" INTEGER NULL,
            "UpdateState" INTEGER NOT NULL,
            "UserId" INTEGER NOT NULL,
            "PresetKey" TEXT NOT NULL
        );
        CREATE UNIQUE INDEX IF NOT EXISTS "IX_UserFavorite_UserId_PresetKey"
            ON "UserFavorite" ("UserId", "PresetKey");
        """);
    if (!db.Users.Any())
    {
        db.Users.Add(new User
        {
            Username = "admin",
            DisplayName = "Administrator",
            Role = Roles.Admin,
            PasswordHash = PasswordHasher.Hash("admin"),
        });
        db.SaveChanges();
        app.Logger.LogWarning("Admin-Benutzer angelegt: admin / admin – Passwort bitte ändern!");
    }
}

app.UseStaticFiles();
app.UseRouting();
app.UseAuthentication();
app.UseAuthorization();
app.MapRazorPages();

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
        scores = scores.Select(e => new { id = e.Id, playerId = e.PlayerId, value = e.Value, round = e.Round }),
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

play.MapPost("/player", async (string token, AddPlayerRequest req, GameService games) =>
{
    var game = await games.GetByTokenAsync(token);
    if (game == null) return Results.NotFound();
    if (string.IsNullOrWhiteSpace(req.Name)) return Results.BadRequest();
    var player = await games.AddPlayerAsync(game, req.Name);
    return Results.Json(new { id = player.Id, version = game.Version });
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
public record AddPlayerRequest(string Name);
public record PlayerStateRequest(long PlayerId, JsonElement State);
public record ConfigRequest(JsonElement Config);
public record StatusRequest(int Status);

public record AppInfo(string Version);

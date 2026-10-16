using MatPlay.Data;
using Microsoft.EntityFrameworkCore;

namespace MatPlay.Services;

/// <summary>Kennzahlen der ganzen Instanz (über alle Benutzer und Sessions).</summary>
/// <param name="Games">Spiele (ohne gelöschte).</param>
/// <param name="RunningGames">Davon noch nicht beendet.</param>
/// <param name="GamesLast30Days">In den letzten 30 Tagen angelegte Spiele.</param>
/// <param name="SavedPlayers">Gespeicherte Spielerprofile.</param>
/// <param name="Accounts">Benutzerkonten (ohne Gäste und gelöschte).</param>
/// <param name="TopGame">Name der am häufigsten gespielten Spielart, null ohne Spiele.</param>
/// <param name="LastPlayedUtc">Letzte Änderung an einem Spiel (Punkte, Status, ...), null ohne Spiele.</param>
public sealed record InstanceSnapshot(
    int Games,
    int RunningGames,
    int GamesLast30Days,
    int SavedPlayers,
    int Accounts,
    string? TopGame,
    DateTime? LastPlayedUtc);

/// <summary>Zustand und Pflege der Instanz für die Plattform-API (Kacheln, Health, Aktionen).</summary>
public class InstanceStatusService(AppDbContext db)
{
    public async Task<InstanceSnapshot> GetSnapshotAsync(CancellationToken ct = default)
    {
        var games = db.Games.AsNoTracking().Where(g => g.UpdateState != UpdateStates.Deleted);
        var since = DateTime.UtcNow.AddDays(-30);

        var top = await games
            .GroupBy(g => g.PresetKey ?? g.ModuleKey)
            .Select(g => new { Key = g.Key, Count = g.Count() })
            .OrderByDescending(g => g.Count).ThenBy(g => g.Key)
            .FirstOrDefaultAsync(ct);

        return new InstanceSnapshot(
            await games.CountAsync(ct),
            await games.CountAsync(g => g.Status == GameStatus.Running, ct),
            await games.CountAsync(g => g.CreateDate >= since, ct),
            await db.SavedPlayers.AsNoTracking().CountAsync(p => p.UpdateState != UpdateStates.Deleted, ct),
            await db.Users.AsNoTracking().CountAsync(u => u.UpdateState != UpdateStates.Deleted, ct),
            top is null ? null : GameTypeName(top.Key),
            await games.MaxAsync(g => (DateTime?)g.UpdateDate, ct));
    }

    /// <summary>True, wenn die Datenbank gelesen werden kann.</summary>
    public async Task<bool> CanReadDatabaseAsync(CancellationToken ct = default)
    {
        try
        {
            await db.Users.AsNoTracking().AnyAsync(ct);
            return true;
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            return false;
        }
    }

    public Task<int> CountActiveAdminsAsync(CancellationToken ct = default) =>
        db.Users.AsNoTracking().CountAsync(u => u.UpdateState != UpdateStates.Deleted && u.Role == Roles.Admin, ct);

    /// <summary>Löscht abgelaufene Sessions (sie sind ohnehin nicht mehr gültig); Spiele und Profile bleiben.</summary>
    public Task<int> CleanupExpiredSessionsAsync(CancellationToken ct = default)
    {
        var now = DateTime.UtcNow;
        return db.UserSessions.Where(s => s.ExpiresDate <= now).ExecuteDeleteAsync(ct);
    }

    /// <summary>Anzeigename einer Spielart: Preset, sonst Modul, sonst der Schlüssel.</summary>
    private static string GameTypeName(string key) =>
        ModuleRegistry.GetPreset(key)?.Name ?? ModuleRegistry.GetModule(key)?.Name ?? key;
}

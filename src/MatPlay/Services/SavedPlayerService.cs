using MatPlay.Data;
using Microsoft.EntityFrameworkCore;

namespace MatPlay.Services;

/// <summary>Gespeicherte Spielerprofile des aktuellen Benutzers bzw. der aktuellen Session.</summary>
public class SavedPlayerService(AppDbContext db, CurrentContext current)
{
    public IQueryable<SavedPlayer> QueryMine() =>
        db.SavedPlayers.Where(p => p.UpdateState != UpdateStates.Deleted &&
            ((p.OwnerUserId != null && p.OwnerUserId == current.UserId) ||
             (p.OwnerSessionId != null && p.OwnerSessionId == current.SessionId)));

    public bool IsOwner(SavedPlayer player) =>
        (player.OwnerUserId != null && player.OwnerUserId == current.UserId)
        || (player.OwnerSessionId != null && player.OwnerSessionId == current.SessionId)
        || current.IsAdmin;

    public Task<SavedPlayer?> FindMineByNameAsync(string name)
    {
        var normalized = name.Trim().ToLower();
        return QueryMine().FirstOrDefaultAsync(p => p.Name.ToLower() == normalized);
    }

    public async Task<SavedPlayer> CreateAsync(string name)
    {
        var player = new SavedPlayer
        {
            Name = name.Trim(),
            OwnerUserId = current.UserId,
            OwnerSessionId = current.UserId == null ? current.SessionId : null,
            CreateUserId = current.UserId,
            UpdateUserId = current.UserId,
        };
        db.SavedPlayers.Add(player);
        await db.SaveChangesAsync();
        return player;
    }

    /// <summary>Verknüpft einen Spielteilnehmer mit einem Profil: vorhandenes wird wiederverwendet,
    /// neues nur angelegt, wenn <paramref name="createIfMissing"/> gesetzt ist.</summary>
    public async Task LinkAsync(GamePlayer gamePlayer, bool createIfMissing)
    {
        var saved = await FindMineByNameAsync(gamePlayer.Name);
        if (saved == null && createIfMissing)
            saved = await CreateAsync(gamePlayer.Name);
        if (saved == null) return;

        gamePlayer.SavedPlayerId = saved.Id;
        saved.LastUsedDate = DateTime.UtcNow;
        await db.SaveChangesAsync();
    }

    public async Task SoftDeleteAsync(SavedPlayer player)
    {
        player.UpdateState = UpdateStates.Deleted;
        player.UpdateDate = DateTime.UtcNow;
        player.UpdateUserId = current.UserId;
        await db.SaveChangesAsync();
    }

    /// <summary>Spiele, an denen das Profil teilgenommen hat (neueste zuerst).</summary>
    public Task<List<Game>> GetGamesAsync(long savedPlayerId) =>
        (from gp in db.GamePlayers
         where gp.SavedPlayerId == savedPlayerId && gp.UpdateState != UpdateStates.Deleted
         join g in db.Games on gp.GameId equals g.Id
         where g.UpdateState != UpdateStates.Deleted
         orderby g.UpdateDate descending
         select g).ToListAsync();
}

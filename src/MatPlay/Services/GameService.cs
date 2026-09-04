using MatPlay.Data;
using Microsoft.EntityFrameworkCore;

namespace MatPlay.Services;

public class GameService(AppDbContext db, CurrentContext current, SavedPlayerService savedPlayers)
{
    public async Task<Game> CreateAsync(string name, string moduleKey, string configJson,
        IEnumerable<string> playerNames, bool savePlayers = false, string? presetKey = null)
    {
        var game = new Game
        {
            Name = name,
            ModuleKey = moduleKey,
            PresetKey = presetKey,
            ConfigJson = configJson,
            OwnerUserId = current.UserId,
            OwnerSessionId = current.UserId == null ? current.SessionId : null,
            CreateUserId = current.UserId,
            UpdateUserId = current.UserId,
        };
        db.Games.Add(game);
        await db.SaveChangesAsync();

        var order = 0;
        var created = new List<GamePlayer>();
        foreach (var playerName in playerNames.Where(n => !string.IsNullOrWhiteSpace(n)))
        {
            var player = new GamePlayer
            {
                GameId = game.Id,
                Name = playerName.Trim(),
                SortOrder = order++,
                CreateUserId = current.UserId,
                UpdateUserId = current.UserId,
            };
            db.GamePlayers.Add(player);
            created.Add(player);
        }
        await db.SaveChangesAsync();

        // Mit Spielerprofilen verknüpfen; neue Profile nur bei aktiviertem Auto-Speichern
        foreach (var player in created)
            await savedPlayers.LinkAsync(player, savePlayers);
        return game;
    }

    public Task<Game?> GetByTokenAsync(string shareToken) =>
        db.Games.FirstOrDefaultAsync(g => g.ShareToken == shareToken && g.UpdateState != UpdateStates.Deleted);

    public Task<Game?> GetByIdAsync(long id) =>
        db.Games.FirstOrDefaultAsync(g => g.Id == id && g.UpdateState != UpdateStates.Deleted);

    public bool IsOwner(Game game) =>
        (game.OwnerUserId != null && game.OwnerUserId == current.UserId)
        || (game.OwnerSessionId != null && game.OwnerSessionId == current.SessionId)
        || current.IsAdmin;

    public IQueryable<Game> QueryMyGames() =>
        db.Games.Where(g => g.UpdateState != UpdateStates.Deleted &&
            ((g.OwnerUserId != null && g.OwnerUserId == current.UserId) ||
             (g.OwnerSessionId != null && g.OwnerSessionId == current.SessionId)));

    public Task<List<GamePlayer>> GetPlayersAsync(long gameId) =>
        db.GamePlayers
            .Where(p => p.GameId == gameId && p.UpdateState != UpdateStates.Deleted)
            .OrderBy(p => p.SortOrder).ThenBy(p => p.Id)
            .ToListAsync();

    public Task<List<ScoreEntry>> GetScoresAsync(long gameId) =>
        db.ScoreEntries
            .Where(e => e.GameId == gameId && e.UpdateState != UpdateStates.Deleted)
            .OrderBy(e => e.Id)
            .ToListAsync();

    public async Task<ScoreEntry> AddScoreAsync(Game game, long playerId, int value, int round)
    {
        var entry = new ScoreEntry
        {
            GameId = game.Id,
            PlayerId = playerId,
            Value = value,
            Round = round,
            CreateUserId = current.UserId,
            UpdateUserId = current.UserId,
        };
        db.ScoreEntries.Add(entry);
        await BumpVersionAsync(game);
        return entry;
    }

    public async Task<bool> UndoLastScoreAsync(Game game, long? playerId = null)
    {
        var query = db.ScoreEntries.Where(e => e.GameId == game.Id && e.UpdateState != UpdateStates.Deleted);
        if (playerId != null) query = query.Where(e => e.PlayerId == playerId);
        var last = await query.OrderByDescending(e => e.Id).FirstOrDefaultAsync();
        if (last == null) return false;
        last.UpdateState = UpdateStates.Deleted;
        last.UpdateDate = DateTime.UtcNow;
        last.UpdateUserId = current.UserId;
        await BumpVersionAsync(game);
        return true;
    }

    public async Task SetPlayerStateAsync(Game game, long playerId, string stateJson)
    {
        var player = await db.GamePlayers.FirstOrDefaultAsync(p =>
            p.Id == playerId && p.GameId == game.Id && p.UpdateState != UpdateStates.Deleted);
        if (player == null) return;
        player.StateJson = stateJson;
        player.UpdateDate = DateTime.UtcNow;
        player.UpdateUserId = current.UserId;
        player.UpdateState = UpdateStates.Updated;
        await BumpVersionAsync(game);
    }

    public async Task<GamePlayer> AddPlayerAsync(Game game, string name)
    {
        var maxOrder = await db.GamePlayers
            .Where(p => p.GameId == game.Id)
            .Select(p => (int?)p.SortOrder).MaxAsync() ?? -1;
        var player = new GamePlayer
        {
            GameId = game.Id,
            Name = name.Trim(),
            SortOrder = maxOrder + 1,
            CreateUserId = current.UserId,
            UpdateUserId = current.UserId,
        };
        db.GamePlayers.Add(player);
        await BumpVersionAsync(game);
        // Nachträglich hinzugefügte Spieler mit vorhandenem Profil des Besitzers verknüpfen
        if (IsOwner(game))
            await savedPlayers.LinkAsync(player, createIfMissing: false);
        return player;
    }

    public async Task SetStatusAsync(Game game, int status)
    {
        game.Status = status;
        await BumpVersionAsync(game);
    }

    public async Task SetConfigAsync(Game game, string configJson)
    {
        game.ConfigJson = configJson;
        await BumpVersionAsync(game);
    }

    public async Task SoftDeleteAsync(Game game)
    {
        game.UpdateState = UpdateStates.Deleted;
        game.UpdateDate = DateTime.UtcNow;
        game.UpdateUserId = current.UserId;
        await db.SaveChangesAsync();
    }

    private async Task BumpVersionAsync(Game game)
    {
        game.Version++;
        game.UpdateDate = DateTime.UtcNow;
        game.UpdateUserId = current.UserId;
        game.UpdateState = UpdateStates.Updated;
        await db.SaveChangesAsync();
    }
}

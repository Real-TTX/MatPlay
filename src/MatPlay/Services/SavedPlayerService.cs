using MatPlay.Data;
using Microsoft.EntityFrameworkCore;

namespace MatPlay.Services;

/// <summary>Gespeicherte Spielerprofile des aktuellen Benutzers bzw. der aktuellen Session.</summary>
public class SavedPlayerService(AppDbContext db, CurrentContext current, AppConfigService config)
{
    /// <summary>Neon-Palette für die automatische Farbvergabe.</summary>
    public static readonly string[] Palette =
    [
        "#00e5ff", "#ff2ec4", "#a3ff12", "#ff9f1c", "#7c4dff", "#ff3d5a",
        "#22d3a5", "#f0b400", "#2f7ee8", "#ff6ee0", "#8aff5a", "#00b8d9",
    ];

    /// <summary>Erzeugt ein eindeutiges Kürzel: erste Buchstaben des Namens; bei Kollision wandert
    /// der letzte Buchstabe durch den Namen (Matthias = "Ma", Manuel = "Mn"), danach Alphabet/Ziffern.</summary>
    public static string GenerateCode(string name, IReadOnlyCollection<string> taken, int length)
    {
        length = Math.Clamp(length, 1, 4);
        var letters = new string(name.Where(char.IsLetterOrDigit).ToArray());
        if (letters.Length == 0) letters = "X";
        var prefixLen = length - 1;
        var prefix = letters.Length >= prefixLen ? letters[..prefixLen] : letters.PadRight(prefixLen, 'x');

        string Format(string code) => char.ToUpperInvariant(code[0]) + code[1..].ToLowerInvariant();
        bool IsFree(string code) => !taken.Contains(code, StringComparer.OrdinalIgnoreCase);

        var rest = letters.Length > prefixLen ? letters[prefixLen..] : "";
        foreach (var c in rest + "abcdefghijklmnopqrstuvwxyz0123456789")
        {
            var code = Format(prefix + c);
            if (IsFree(code)) return code;
        }
        for (var i = 2; i < 1000; i++)
        {
            var code = Format(prefix + i);
            if (IsFree(code)) return code;
        }
        return Format(prefix + "?");
    }

    /// <summary>Nächste freie Farbe aus der Palette, danach zyklisch.</summary>
    public static string NextColor(IReadOnlyCollection<string> used)
    {
        var usedSet = used.ToHashSet(StringComparer.OrdinalIgnoreCase);
        return Palette.FirstOrDefault(c => !usedSet.Contains(c)) ?? Palette[used.Count % Palette.Length];
    }

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
        var mine = await QueryMine().ToListAsync();
        var player = new SavedPlayer
        {
            Name = name.Trim(),
            Code = GenerateCode(name, mine.Select(p => p.Code).ToList(), config.Config.PlayerCodeLength),
            Color = NextColor(mine.Select(p => p.Color).ToList()),
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

namespace MatPlay.Data;

/// <summary>Audit-Basis nach DB-Guideline: Id als BIGINT, Create/Update-Felder, UpdateState für Soft-Delete.</summary>
public abstract class BaseEntity
{
    public long Id { get; set; }
    public DateTime CreateDate { get; set; } = DateTime.UtcNow;
    public long? CreateUserId { get; set; }
    public DateTime UpdateDate { get; set; } = DateTime.UtcNow;
    public long? UpdateUserId { get; set; }
    /// <summary>0 = Deleted, 1 = Created, 2 = Updated</summary>
    public int UpdateState { get; set; } = 1;
}

public static class UpdateStates
{
    public const int Deleted = 0;
    public const int Created = 1;
    public const int Updated = 2;
}

public static class Roles
{
    public const string Admin = "Admin";
    public const string User = "User";
    public const string Anonymous = "Anonymous";
}

public class User : BaseEntity
{
    public string Username { get; set; } = "";
    public string DisplayName { get; set; } = "";
    public string PasswordHash { get; set; } = "";
    public string Role { get; set; } = Roles.User;
    /// <summary>Erzwingt die Passwort-Änderung beim nächsten Login (z.B. Seed-Admin).</summary>
    public bool MustChangePassword { get; set; }
}

/// <summary>Serverseitige Session – überlebt Container-Restarts, Token ist das Sicherheitsmerkmal.</summary>
public class UserSession : BaseEntity
{
    public string Token { get; set; } = Guid.NewGuid().ToString("N");
    public long? UserId { get; set; }
    public DateTime LastSeenDate { get; set; } = DateTime.UtcNow;
    public DateTime ExpiresDate { get; set; } = DateTime.UtcNow.AddDays(180);
}

/// <summary>Gespeichertes Spielerprofil eines Benutzers bzw. einer (anonymen) Session.</summary>
public class SavedPlayer : BaseEntity
{
    public long? OwnerUserId { get; set; }
    public long? OwnerSessionId { get; set; }
    public string Name { get; set; } = "";
    /// <summary>Automatisch vergebenes, pro Besitzer eindeutiges Kürzel (z.B. "Ma", "Mn"); manuell änderbar.</summary>
    public string Code { get; set; } = "";
    /// <summary>Automatisch zugewiesene Spielerfarbe (Hex); manuell änderbar.</summary>
    public string Color { get; set; } = "";
    public DateTime LastUsedDate { get; set; } = DateTime.UtcNow;
}

/// <summary>Favorisiertes Spiel (Preset) eines angemeldeten Benutzers.</summary>
public class UserFavorite : BaseEntity
{
    public long UserId { get; set; }
    public string PresetKey { get; set; } = "";
}

public static class GameStatus
{
    public const int Running = 0;
    public const int Finished = 1;
}

public class Game : BaseEntity
{
    public string Name { get; set; } = "";
    /// <summary>Schlüssel des Spielmoduls, z.B. "counter" oder "qwixx".</summary>
    public string ModuleKey { get; set; } = "counter";
    /// <summary>Preset, aus dem das Spiel erstellt wurde (für "zuletzt gespielte Spielarten").</summary>
    public string? PresetKey { get; set; }
    /// <summary>Share-Token für den Freigabe-Link (Anonym-Zugriff).</summary>
    public string ShareToken { get; set; } = Guid.NewGuid().ToString("N");
    /// <summary>Modul-spezifische Konfiguration als JSON.</summary>
    public string ConfigJson { get; set; } = "{}";
    public int Status { get; set; } = GameStatus.Running;
    /// <summary>Wird bei jeder Spieländerung erhöht – Basis für Live-Polling.</summary>
    public long Version { get; set; } = 1;
    public long? OwnerUserId { get; set; }
    /// <summary>Besitzer-Session für anonym erstellte Spiele.</summary>
    public long? OwnerSessionId { get; set; }
}

public class GamePlayer : BaseEntity
{
    public long GameId { get; set; }
    public string Name { get; set; } = "";
    public int SortOrder { get; set; }
    /// <summary>Modul-spezifischer Spielerzustand als JSON (z.B. Qwixx-Kreuze).</summary>
    public string StateJson { get; set; } = "{}";
    /// <summary>Verknüpfung zum gespeicherten Spielerprofil (für Historie und Vorauswahl).</summary>
    public long? SavedPlayerId { get; set; }
}

public class ScoreEntry : BaseEntity
{
    public long GameId { get; set; }
    public long PlayerId { get; set; }
    public int Round { get; set; }
    public int Value { get; set; }
}

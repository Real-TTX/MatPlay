using MatPlay.Services;
using Microsoft.EntityFrameworkCore;

namespace MatPlay.Data;

/// <summary>
/// Schema anlegen, für bestehende Datenbanken nachziehen (EnsureCreated legt neue Tabellen/Spalten nicht nach) und den Admin seeden.
/// Läuft bei jedem Start und mehrfach ohne Schaden.
/// </summary>
public static class DbInitializer
{
    public static void Initialize(AppDbContext db, int playerCodeLength, ILogger logger)
    {
        db.Database.EnsureCreated();
        Upgrade(db, playerCodeLength);
        SeedAdmin(db, logger);
    }

    /// <summary>Mini-Schema-Upgrade: neue Tabellen, Spalten und Indizes für ältere Datenbanken, danach Backfills.</summary>
    public static void Upgrade(AppDbContext db, int playerCodeLength)
    {
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
        db.Database.ExecuteSqlRaw("""
            CREATE TABLE IF NOT EXISTS "SavedPlayer" (
                "Id" INTEGER NOT NULL CONSTRAINT "PK_SavedPlayer" PRIMARY KEY AUTOINCREMENT,
                "CreateDate" TEXT NOT NULL,
                "CreateUserId" INTEGER NULL,
                "UpdateDate" TEXT NOT NULL,
                "UpdateUserId" INTEGER NULL,
                "UpdateState" INTEGER NOT NULL,
                "OwnerUserId" INTEGER NULL,
                "OwnerSessionId" INTEGER NULL,
                "Name" TEXT NOT NULL,
                "LastUsedDate" TEXT NOT NULL
            );
            CREATE INDEX IF NOT EXISTS "IX_SavedPlayer_OwnerUserId" ON "SavedPlayer" ("OwnerUserId");
            CREATE INDEX IF NOT EXISTS "IX_SavedPlayer_OwnerSessionId" ON "SavedPlayer" ("OwnerSessionId");
            """);
        // Spalten-Upgrades: ALTER schlägt fehl, wenn die Spalte schon existiert
        foreach (var alter in new[]
        {
            """ALTER TABLE "User" ADD COLUMN "MustChangePassword" INTEGER NOT NULL DEFAULT 0;""",
            """ALTER TABLE "GamePlayer" ADD COLUMN "SavedPlayerId" INTEGER NULL;""",
            """ALTER TABLE "SavedPlayer" ADD COLUMN "Code" TEXT NOT NULL DEFAULT '';""",
            """ALTER TABLE "SavedPlayer" ADD COLUMN "Color" TEXT NOT NULL DEFAULT '';""",
            """ALTER TABLE "Game" ADD COLUMN "PresetKey" TEXT NULL;""",
            // Anmeldung über MatIdentity: sub des Benutzers, ID-Token der Session (für die Abmeldung dort)
            """ALTER TABLE "User" ADD COLUMN "ExternalSubject" TEXT NULL;""",
            """ALTER TABLE "UserSession" ADD COLUMN "IdentityToken" TEXT NULL;""",
        })
        {
            try
            {
                db.Database.ExecuteSqlRaw(alter);
            }
            catch (Microsoft.Data.Sqlite.SqliteException)
            {
                // Spalte existiert bereits
            }
        }
        db.Database.ExecuteSqlRaw("""
            CREATE UNIQUE INDEX IF NOT EXISTS "IX_User_ExternalSubject" ON "User" ("ExternalSubject");
            """);

        // Backfill: Kürzel und Farben für bestehende Spielerprofile vergeben
        if (db.SavedPlayers.Any(p => p.UpdateState != UpdateStates.Deleted && (p.Code == "" || p.Color == "")))
        {
            var all = db.SavedPlayers.Where(p => p.UpdateState != UpdateStates.Deleted).ToList();
            foreach (var group in all.GroupBy(p => (p.OwnerUserId, p.OwnerSessionId)))
            {
                var codes = group.Where(p => p.Code != "").Select(p => p.Code).ToList();
                var colors = group.Where(p => p.Color != "").Select(p => p.Color).ToList();
                foreach (var player in group)
                {
                    if (player.Code == "")
                    {
                        player.Code = SavedPlayerService.GenerateCode(player.Name, codes, playerCodeLength);
                        codes.Add(player.Code);
                    }
                    if (player.Color == "")
                    {
                        player.Color = SavedPlayerService.NextColor(colors);
                        colors.Add(player.Color);
                    }
                }
            }
            db.SaveChanges();
        }
    }

    /// <summary>Legt den Admin admin/admin an, wenn es noch keinen Benutzer gibt (Passwort-Änderung beim ersten Login erzwungen).</summary>
    public static void SeedAdmin(AppDbContext db, ILogger logger)
    {
        if (db.Users.Any()) return;

        db.Users.Add(new User
        {
            Username = "admin",
            DisplayName = "Administrator",
            Role = Roles.Admin,
            PasswordHash = PasswordHasher.Hash("admin"),
            MustChangePassword = true,
        });
        db.SaveChanges();
        logger.LogWarning("Admin-Benutzer angelegt: admin / admin – Passwort bitte ändern!");
    }
}

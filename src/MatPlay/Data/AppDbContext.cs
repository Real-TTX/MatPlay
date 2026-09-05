using Microsoft.EntityFrameworkCore;

namespace MatPlay.Data;

public class AppDbContext(DbContextOptions<AppDbContext> options) : DbContext(options)
{
    public DbSet<User> Users => Set<User>();
    public DbSet<UserSession> UserSessions => Set<UserSession>();
    public DbSet<Game> Games => Set<Game>();
    public DbSet<GamePlayer> GamePlayers => Set<GamePlayer>();
    public DbSet<ScoreEntry> ScoreEntries => Set<ScoreEntry>();
    public DbSet<UserFavorite> UserFavorites => Set<UserFavorite>();
    public DbSet<SavedPlayer> SavedPlayers => Set<SavedPlayer>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        // Tabellen PascalCase (Singular = Klassenname)
        modelBuilder.Entity<User>().ToTable("User");
        modelBuilder.Entity<UserSession>().ToTable("UserSession");
        modelBuilder.Entity<Game>().ToTable("Game");
        modelBuilder.Entity<GamePlayer>().ToTable("GamePlayer");
        modelBuilder.Entity<ScoreEntry>().ToTable("ScoreEntry");
        modelBuilder.Entity<UserFavorite>().ToTable("UserFavorite");
        modelBuilder.Entity<SavedPlayer>().ToTable("SavedPlayer");

        modelBuilder.Entity<User>().HasIndex(u => u.Username).IsUnique();
        modelBuilder.Entity<UserSession>().HasIndex(s => s.Token).IsUnique();
        modelBuilder.Entity<Game>().HasIndex(g => g.ShareToken).IsUnique();
        modelBuilder.Entity<GamePlayer>().HasIndex(p => p.GameId);
        modelBuilder.Entity<ScoreEntry>().HasIndex(e => e.GameId);
        modelBuilder.Entity<UserFavorite>().HasIndex(f => new { f.UserId, f.PresetKey }).IsUnique();
        modelBuilder.Entity<SavedPlayer>().HasIndex(p => p.OwnerUserId);
        modelBuilder.Entity<SavedPlayer>().HasIndex(p => p.OwnerSessionId);
        modelBuilder.Entity<GamePlayer>().HasIndex(p => p.SavedPlayerId);
    }
}

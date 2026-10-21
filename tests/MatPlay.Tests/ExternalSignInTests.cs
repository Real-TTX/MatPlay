using MatPlay.Data;
using MatPlay.Services;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;

namespace MatPlay.Tests;

/// <summary>Wie eine von MatIdentity angemeldete Person zum lokalen Benutzer wird (<see cref="ExternalSignInService"/>), auf einer SQLite-Datenbank im Speicher.</summary>
public sealed class ExternalSignInTests : IAsyncLifetime
{
    private readonly SqliteConnection _connection = new("Data Source=:memory:");
    private AppDbContext _db = null!;
    private ExternalSignInService _users = null!;

    public async Task InitializeAsync()
    {
        await _connection.OpenAsync();
        _db = new AppDbContext(new DbContextOptionsBuilder<AppDbContext>().UseSqlite(_connection).Options);
        DbInitializer.Initialize(_db, 2, NullLogger.Instance);
        _users = new ExternalSignInService(_db, NullLogger<ExternalSignInService>.Instance);
    }

    public async Task DisposeAsync()
    {
        await _db.DisposeAsync();
        await _connection.DisposeAsync();
    }

    [Fact]
    public async Task A_new_person_becomes_a_user_with_the_role_of_the_claim_and_is_found_again_by_sub()
    {
        var result = await _users.FindOrCreateAsync(new ExternalIdentity("sub-1", "Neu@Example.com", "Nora Neu", "nora", "admin"));

        Assert.True(result.Succeeded, result.Error);
        var user = result.User!;
        Assert.Equal("sub-1", user.ExternalSubject);
        Assert.Equal("Nora Neu", user.DisplayName);
        Assert.Equal("neu@example.com", user.Username);
        Assert.Equal(Roles.Admin, user.Role);
        Assert.Equal("", user.PasswordHash);

        var again = await _users.FindOrCreateAsync(new ExternalIdentity("sub-1", "other@example.com", "Other", null, "admin"));
        Assert.Equal(user.Id, again.User!.Id);
    }

    [Fact]
    public async Task Without_a_name_the_preferred_username_is_used_and_without_role_claim_the_user_is_a_user()
    {
        var result = await _users.FindOrCreateAsync(new ExternalIdentity("sub-2", null, null, "bert", null));
        Assert.Equal("bert", result.User!.DisplayName);
        Assert.Equal("bert", result.User.Username);
        Assert.Equal(Roles.User, result.User.Role);
    }

    [Fact]
    public async Task A_taken_username_is_not_reused()
    {
        var result = await _users.FindOrCreateAsync(new ExternalIdentity("sub-3", null, "Jemand", "admin", null));
        Assert.Equal("admin-2", result.User!.Username);
        Assert.Equal(Roles.User, result.User.Role);
        Assert.NotEqual((await _db.Users.SingleAsync(u => u.Username == "admin")).Id, result.User.Id);
    }

    [Fact]
    public async Task An_existing_user_whose_username_is_the_email_is_not_linked_by_the_address_alone()
    {
        var anna = await AddAsync("Anna@Example.com", "geheim1");
        var count = await _db.Users.CountAsync();

        var result = await _users.FindOrCreateAsync(new ExternalIdentity("sub-anna", "anna@example.com", "Anna Example", null, "admin"));

        Assert.False(result.Succeeded);
        Assert.True(result.LinkRequired);
        Assert.Equal(anna.Id, result.LinkUserId);
        var stored = await _db.Users.AsNoTracking().SingleAsync(x => x.Id == anna.Id);
        Assert.Null(stored.ExternalSubject);
        Assert.Equal(Roles.User, stored.Role);
        Assert.Equal(count, await _db.Users.CountAsync());
    }

    [Fact]
    public async Task After_the_local_sign_in_the_account_is_linked_and_follows_the_role_claim()
    {
        var anna = await AddAsync("Anna@Example.com", "geheim1");

        var result = await _users.LinkAfterLocalSignInAsync(anna.Id, new ExternalIdentity("sub-anna", "anna@example.com", "Anna Example", null, "admin"));

        Assert.True(result.Succeeded, result.Error);
        var stored = await _db.Users.AsNoTracking().SingleAsync(x => x.Id == anna.Id);
        Assert.Equal("sub-anna", stored.ExternalSubject);
        Assert.Equal(Roles.Admin, stored.Role);
        Assert.Equal("Anna", stored.DisplayName);
        Assert.Equal(anna.Id, (await _users.FindOrCreateAsync(new ExternalIdentity("sub-anna", "anna@example.com", null, null, null))).User!.Id);
    }

    [Fact]
    public async Task The_link_is_refused_when_the_account_no_longer_matches()
    {
        var linked = await AddAsync("linked@example.com", "geheim1");
        linked.ExternalSubject = "sub-old";
        var renamed = await AddAsync("renamed", "geheim1");
        var deleted = await AddAsync("deleted@example.com", "geheim1");
        deleted.UpdateState = UpdateStates.Deleted;
        var free = await AddAsync("free@example.com", "geheim1");
        var taken = await AddAsync("taken", "geheim1");
        taken.ExternalSubject = "sub-taken";
        await _db.SaveChangesAsync();

        Assert.False((await _users.LinkAfterLocalSignInAsync(linked.Id, new ExternalIdentity("sub-new", "linked@example.com", null, null, null))).Succeeded);
        Assert.False((await _users.LinkAfterLocalSignInAsync(renamed.Id, new ExternalIdentity("sub-new", "renamed@example.com", null, null, null))).Succeeded);
        Assert.False((await _users.LinkAfterLocalSignInAsync(deleted.Id, new ExternalIdentity("sub-new", "deleted@example.com", null, null, null))).Succeeded);
        Assert.False((await _users.LinkAfterLocalSignInAsync(free.Id, new ExternalIdentity("sub-taken", "free@example.com", null, null, null))).Succeeded);
        Assert.Equal("sub-old", (await _db.Users.AsNoTracking().SingleAsync(x => x.Id == linked.Id)).ExternalSubject);
        Assert.Null((await _db.Users.AsNoTracking().SingleAsync(x => x.Id == free.Id)).ExternalSubject);
    }

    [Fact]
    public async Task A_matching_account_without_a_password_cannot_prove_it_and_a_new_user_is_made_beside_it()
    {
        var bare = await AddAsync("bare@example.com", null);

        var result = await _users.FindOrCreateAsync(new ExternalIdentity("sub-bare", "bare@example.com", "Bare", "bare", null));

        Assert.True(result.Succeeded, result.Error);
        Assert.NotEqual(bare.Id, result.User!.Id);
        Assert.Equal("bare", result.User.Username);
        Assert.Null((await _db.Users.AsNoTracking().SingleAsync(x => x.Id == bare.Id)).ExternalSubject);
    }

    [Fact]
    public async Task A_user_already_linked_to_another_person_is_not_taken_over()
    {
        await _users.FindOrCreateAsync(new ExternalIdentity("sub-a", "same@example.com", "A", null, null));

        var result = await _users.FindOrCreateAsync(new ExternalIdentity("sub-b", "same@example.com", "B", null, null));

        Assert.True(result.Succeeded);
        Assert.Equal("sub-b", result.User!.ExternalSubject);
        Assert.NotEqual("same@example.com", result.User.Username);
    }

    [Fact]
    public async Task The_role_claim_wins_at_every_sign_in_and_without_it_the_local_role_stays()
    {
        Assert.Equal(Roles.User, (await _users.FindOrCreateAsync(new ExternalIdentity("sub-r", "r@example.com", "R", null, "user"))).User!.Role);
        Assert.Equal(Roles.Admin, (await _users.FindOrCreateAsync(new ExternalIdentity("sub-r", null, "R", null, "admin"))).User!.Role);
        Assert.Equal(Roles.Admin, (await _users.FindOrCreateAsync(new ExternalIdentity("sub-r", null, "R", null, null))).User!.Role);
        Assert.Equal(Roles.Admin, (await _users.FindOrCreateAsync(new ExternalIdentity("sub-r", null, "R", null, "something-else"))).User!.Role);
        Assert.Equal(Roles.User, (await _users.FindOrCreateAsync(new ExternalIdentity("sub-r", null, "R", null, "user"))).User!.Role);
    }

    [Fact]
    public async Task The_last_administrator_is_not_demoted_by_a_claim()
    {
        var admin = await _db.Users.SingleAsync(x => x.Role == Roles.Admin);
        admin.Username = "admin@example.com";
        await _db.SaveChangesAsync();

        Assert.Equal(admin.Id, (await _users.FindOrCreateAsync(new ExternalIdentity("sub-admin", "admin@example.com", "Admin", null, "user"))).LinkUserId);
        var result = await _users.LinkAfterLocalSignInAsync(admin.Id, new ExternalIdentity("sub-admin", "admin@example.com", "Admin", null, "user"));

        Assert.Equal(admin.Id, result.User!.Id);
        Assert.Equal(Roles.Admin, result.User.Role);
        Assert.Equal(Roles.Admin, (await _users.FindOrCreateAsync(new ExternalIdentity("sub-admin", null, "Admin", null, "user"))).User!.Role);
    }

    [Fact]
    public async Task A_deleted_user_stays_out()
    {
        var created = await _users.FindOrCreateAsync(new ExternalIdentity("sub-d", "d@example.com", "D", null, null));
        created.User!.UpdateState = UpdateStates.Deleted;
        await _db.SaveChangesAsync();

        Assert.False((await _users.FindOrCreateAsync(new ExternalIdentity("sub-d", "d@example.com", "D", null, "admin"))).Succeeded);
        // Auch nicht über die Adresse des gelöschten Kontos mit einem anderen sub.
        Assert.False((await _users.FindOrCreateAsync(new ExternalIdentity("sub-d2", "d@example.com", "D", null, null))).Succeeded);
    }

    private async Task<User> AddAsync(string username, string? password)
    {
        var user = new User { Username = username, DisplayName = username.Split('@')[0], PasswordHash = password is null ? "" : PasswordHasher.Hash(password), Role = Roles.User };
        _db.Users.Add(user);
        await _db.SaveChangesAsync();
        return user;
    }

    [Fact]
    public async Task No_subject_is_refused()
        => Assert.False((await _users.FindOrCreateAsync(new ExternalIdentity(" ", "x@example.com", "X", null, null))).Succeeded);

    [Fact]
    public async Task A_user_from_matidentity_cannot_sign_in_locally_without_a_password()
    {
        var created = await _users.FindOrCreateAsync(new ExternalIdentity("sub-p", "p@example.com", "P", null, null));
        Assert.False(PasswordHasher.Verify("", created.User!.PasswordHash));
    }
}

/// <summary>Das Schema-Upgrade ergänzt die Spalten für MatIdentity in einer Datenbank einer älteren Version und läuft zweimal ohne Schaden.</summary>
public sealed class SchemaUpgradeTests
{
    [Fact]
    public async Task An_old_database_gets_the_external_subject_the_identity_token_and_the_identity_session()
    {
        await using var connection = new SqliteConnection("Data Source=:memory:");
        await connection.OpenAsync();
        await using var db = new AppDbContext(new DbContextOptionsBuilder<AppDbContext>().UseSqlite(connection).Options);
        await db.Database.EnsureCreatedAsync();

        // Wie die Version vor MatIdentity sie angelegt hat: ohne Spalten und Index.
        await db.Database.ExecuteSqlRawAsync("DROP INDEX \"IX_User_ExternalSubject\";");
        await db.Database.ExecuteSqlRawAsync("ALTER TABLE \"User\" DROP COLUMN \"ExternalSubject\";");
        await db.Database.ExecuteSqlRawAsync("ALTER TABLE \"UserSession\" DROP COLUMN \"IdentityToken\";");
        await db.Database.ExecuteSqlRawAsync("DROP INDEX IF EXISTS \"IX_UserSession_IdentitySessionId\";");
        await db.Database.ExecuteSqlRawAsync("ALTER TABLE \"UserSession\" DROP COLUMN \"IdentitySessionId\";");

        DbInitializer.Upgrade(db, 2);
        DbInitializer.Upgrade(db, 2);

        db.Users.Add(new User { Username = "x", DisplayName = "X", ExternalSubject = "sub" });
        db.UserSessions.Add(new UserSession { IdentityToken = "token", IdentitySessionId = "sid" });
        await db.SaveChangesAsync();
        Assert.Equal("sub", (await db.Users.AsNoTracking().SingleAsync()).ExternalSubject);
        Assert.Equal("token", (await db.UserSessions.AsNoTracking().SingleAsync()).IdentityToken);
        Assert.Equal("sid", (await db.UserSessions.AsNoTracking().SingleAsync()).IdentitySessionId);

        // Der Index hält sub eindeutig.
        db.Users.Add(new User { Username = "y", DisplayName = "Y", ExternalSubject = "sub" });
        await Assert.ThrowsAsync<DbUpdateException>(() => db.SaveChangesAsync());
    }
}

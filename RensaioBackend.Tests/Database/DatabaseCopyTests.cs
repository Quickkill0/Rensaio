using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using RensaioBackend.Data;
using Xunit;

namespace RensaioBackend.Tests.Database;

[Collection(PostgresCollection.Name)]
public sealed class DatabaseCopyTests : IClassFixture<SqliteHarness>
{
    private readonly SqliteHarness _sqlite;
    private readonly PostgresHarness _postgres;

    public DatabaseCopyTests(SqliteHarness sqlite, PostgresHarness postgres)
    {
        _sqlite = sqlite;
        _postgres = postgres;
    }

    [Fact]
    public void Tables_are_ordered_parents_first()
    {
        using var db = SqliteHarness.Open(Path.Combine(Path.GetTempPath(), "rensaio-order-probe.db"));
        var order = DatabaseCopy.TablesInDependencyOrder(db.Model).Select(t => t.GetTableName()).ToList();

        Assert.True(order.IndexOf("Series") < order.IndexOf("SeriesProviders"));
        Assert.True(order.IndexOf("Series") < order.IndexOf("SeriesMappings"));
        Assert.True(order.IndexOf("Users") < order.IndexOf("UserScrobblerConfigs"));
        Assert.True(order.IndexOf("Users") < order.IndexOf("UserExternalLogins"));
        Assert.Equal(db.Model.GetEntityTypes().Count(), order.Count);
    }

    [Fact]
    public async Task Sqlite_to_postgres_preserves_every_value()
    {
        await using var source = await _sqlite.CreateEmptyAsync();
        await Seed.PopulateAsync(source);
        source.ChangeTracker.Clear();

        await using var target = await _postgres.CreateEmptyAsync();
        Assert.True(await DatabaseCopy.IsEmptyAsync(target));

        var results = await DatabaseCopy.CopyAsync(source, target);
        Assert.All(results, r => Assert.True(r.Matches, $"{r.Table}: {r.SourceRows} vs {r.TargetRows}"));
        Assert.Equal(1, results.Single(r => r.Table == "Series").TargetRows);
        Assert.False(await DatabaseCopy.IsEmptyAsync(target));

        var scrobbler = await target.UserScrobblerConfigs.SingleAsync(c => c.Id == Seed.ScrobblerId);
        Assert.False(scrobbler.IsEnabled);
        Assert.Equal(Seed.CreatedAt, scrobbler.TokenExpiresAt);
        Assert.Equal(DateTimeKind.Utc, scrobbler.TokenExpiresAt!.Value.Kind);

        var mapping = await target.SeriesMappings.SingleAsync(m => m.Id == Seed.MappingId);
        Assert.Equal(["NARUTO -ナルト-", "Naruto"], mapping.AlternativeTitles);
        Assert.Equal(Seed.SeriesId, mapping.SeriesId);

        var series = await target.Series.SingleAsync(s => s.Id == Seed.SeriesId);
        Assert.Equal(["Action", "Adventure"], series.Genre);
        Assert.Equal(12.5m, series.StartFromChapter);

        var user = await target.Users.SingleAsync(u => u.Id == Seed.UserId);
        Assert.Equal(new byte[] { 1, 2, 3, 250 }, user.AvatarBlob);

        // JSON moved as text, never re-serialized.
        Assert.Equal(Seed.LegacyChaptersJson, await Seed.ReadChaptersJsonAsync(target));
        var provider = await target.SeriesProviders.SingleAsync(p => p.Id == Seed.ProviderId);
        Assert.Equal(0.5m, provider.ContinueAfterChapter);
        Assert.True(provider.IsTitle);
        Assert.False(provider.IsNSFW);
    }

    [Fact]
    public async Task Postgres_to_sqlite_round_trips()
    {
        await using var source = await _postgres.CreateEmptyAsync();
        await Seed.PopulateAsync(source);
        source.ChangeTracker.Clear();

        await using var target = await _sqlite.CreateEmptyAsync();
        var results = await DatabaseCopy.CopyAsync(source, target);
        Assert.All(results, r => Assert.True(r.Matches, $"{r.Table}: {r.SourceRows} vs {r.TargetRows}"));

        var scrobbler = await target.UserScrobblerConfigs.SingleAsync(c => c.Id == Seed.ScrobblerId);
        Assert.False(scrobbler.IsEnabled);
        var series = await target.Series.SingleAsync(s => s.Id == Seed.SeriesId);
        Assert.Equal("Naruto Shippuden", series.Title);
        Assert.Equal(Seed.CreatedAt, series.LastChapterDate);
        Assert.Equal(Seed.LegacyChaptersJson, await Seed.ReadChaptersJsonAsync(target));
    }

    [Fact]
    public async Task Command_refuses_nonempty_target_without_applying_migrations()
    {
        await using var source = await _sqlite.CreateEmptyAsync();
        await using var target = await _postgres.CreateDatabaseAsync();
        await target.Database.ExecuteSqlRawAsync("CREATE TABLE unrelated (id integer); INSERT INTO unrelated VALUES (1);");
        var pg = new Npgsql.NpgsqlConnectionStringBuilder(target.Database.GetConnectionString());
        var configuration = new Microsoft.Extensions.Configuration.ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?>
        {
            ["ConnectionStrings:DefaultConnection"] = "Data Source=" + source.Database.GetDbConnection().DataSource,
            ["Database:Host"] = pg.Host,
            ["Database:Port"] = pg.Port.ToString(),
            ["Database:Name"] = pg.Database,
            ["Database:Username"] = pg.Username,
            ["Database:Password"] = pg.Password,
            ["Database:SslMode"] = pg.SslMode.ToString(),
        }).Build();
        Assert.Equal(2, await MigrateDbCommand.RunAsync(["--to", "postgres"], configuration));
        Assert.Single(await target.Database.GetPendingMigrationsAsync());
    }

    [Fact]
    public async Task Command_copies_both_directions_and_refuses_existing_target()
    {
        await using var source = await _sqlite.CreateEmptyAsync();
        await Seed.PopulateAsync(source);
        string sourcePath = source.Database.GetDbConnection().DataSource;
        await using var target = await _postgres.CreateDatabaseAsync();
        string targetConnection = target.Database.GetConnectionString()!;
        var settings = new Dictionary<string, string?>
        {
            ["ConnectionStrings:DefaultConnection"] = "Data Source=" + sourcePath,
            ["Database:Host"] = "127.0.0.1"
        };
        var pg = new Npgsql.NpgsqlConnectionStringBuilder(targetConnection);
        settings["Database:Host"] = pg.Host;
        settings["Database:Port"] = pg.Port.ToString();
        settings["Database:Name"] = pg.Database;
        settings["Database:Username"] = pg.Username;
        settings["Database:Password"] = pg.Password;
        settings["Database:SslMode"] = pg.SslMode.ToString();
        var configuration = new Microsoft.Extensions.Configuration.ConfigurationBuilder().AddInMemoryCollection(settings).Build();
        string marker = sourcePath + MigrateDbCommand.MarkerSuffix;
        string returnedPath = sourcePath + ".returned.db";
        try
        {
            // No tracked entities used: exercise the actual CLI resolver, target migrations and copier.
            Assert.Equal(0, await MigrateDbCommand.RunAsync(["--to", "postgres"], configuration));
            Assert.True(File.Exists(marker));
            Assert.Equal(1, await target.Series.CountAsync());
            Assert.Equal(2, await MigrateDbCommand.RunAsync(["--to", "postgres"], configuration));
            Assert.Equal(2, await MigrateDbCommand.RunAsync(["--to", "sqlite"], configuration));
            settings["ConnectionStrings:DefaultConnection"] = "Data Source=" + returnedPath;
            configuration = new Microsoft.Extensions.Configuration.ConfigurationBuilder().AddInMemoryCollection(settings).Build();
            Assert.Equal(0, await MigrateDbCommand.RunAsync(["--to", "sqlite"], configuration));
            await using var returned = SqliteHarness.Open(returnedPath);
            await returned.Database.MigrateAsync();
            Assert.Equal(Seed.LegacyChaptersJson, await Seed.ReadChaptersJsonAsync(returned));
            Assert.False((await returned.UserScrobblerConfigs.SingleAsync()).IsEnabled);
        }
        finally
        {
            Microsoft.Data.Sqlite.SqliteConnection.ClearAllPools();
            foreach (var path in new[] { marker, returnedPath, returnedPath + "-wal", returnedPath + "-shm" })
                File.Delete(path);
        }
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task Failure_after_inserts_rolls_back_all_rows(bool toPostgres)
    {
        await using var source = toPostgres ? await _sqlite.CreateEmptyAsync() : await _postgres.CreateEmptyAsync();
        await Seed.PopulateAsync(source);
        await using var target = toPostgres ? await _postgres.CreateEmptyAsync() : await _sqlite.CreateEmptyAsync();

        await Assert.ThrowsAsync<InvalidOperationException>(() => DatabaseCopy.CopyAsync(source, target,
            progress: line =>
            {
                if (line.EndsWith(": 1 rows"))
                    throw new InvalidOperationException("Injected copy failure after an insert");
            }));
        Assert.True(await DatabaseCopy.IsEmptyAsync(target));
        Assert.Equal(1, await source.Users.CountAsync());
        Assert.Equal(1, await source.Series.CountAsync());
        // An empty target can be retried without manual cleanup.
        Assert.All(await DatabaseCopy.CopyAsync(source, target), r => Assert.True(r.Matches));
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task Cancellation_after_inserts_rolls_back_all_rows(bool toPostgres)
    {
        await using var source = toPostgres ? await _sqlite.CreateEmptyAsync() : await _postgres.CreateEmptyAsync();
        await Seed.PopulateAsync(source);
        await using var target = toPostgres ? await _postgres.CreateEmptyAsync() : await _sqlite.CreateEmptyAsync();
        using var cancellation = new CancellationTokenSource();
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => DatabaseCopy.CopyAsync(source, target,
            progress: line =>
            {
                if (line.EndsWith(": 1 rows"))
                    cancellation.Cancel();
            }, token: cancellation.Token));
        Assert.True(await DatabaseCopy.IsEmptyAsync(target));
        Assert.Equal(1, await source.Series.CountAsync());
    }

    [Fact]
    public async Task Unsupported_postgres_value_rolls_back_prior_tables()
    {
        await using var source = await _sqlite.CreateEmptyAsync();
        await Seed.PopulateAsync(source);
        await source.Database.ExecuteSqlRawAsync("UPDATE Users SET Username = char(0) || 'bad';");
        await using var target = await _postgres.CreateEmptyAsync();
        await Assert.ThrowsAsync<Npgsql.PostgresException>(() => DatabaseCopy.CopyAsync(source, target));
        Assert.True(await DatabaseCopy.IsEmptyAsync(target));
        Assert.Equal(1, await source.Users.CountAsync());
    }

    [Fact]
    public async Task Count_mismatch_rolls_back_instead_of_committing_a_partial_copy()
    {
        await using var source = await _sqlite.CreateEmptyAsync();
        await Seed.PopulateAsync(source);
        await using var target = await _postgres.CreateEmptyAsync();
        await target.Database.ExecuteSqlRawAsync("""
            CREATE FUNCTION skip_row() RETURNS trigger LANGUAGE plpgsql AS 'BEGIN RETURN NULL; END';
            CREATE TRIGGER skip_user BEFORE INSERT ON "Users" FOR EACH ROW EXECUTE FUNCTION skip_row();
            """);
        // Use a row without dependents so the trigger causes a count mismatch, not an FK error.
        await source.Database.ExecuteSqlRawAsync("""DELETE FROM "UserScrobblerConfigs"; DELETE FROM "UserExternalLogins";""");
        await Assert.ThrowsAsync<InvalidOperationException>(() => DatabaseCopy.CopyAsync(source, target));
        Assert.True(await DatabaseCopy.IsEmptyAsync(target));
    }

    [Fact]
    public async Task Copy_refuses_a_non_empty_target()
    {
        await using var source = await _sqlite.CreateEmptyAsync();
        await Seed.PopulateAsync(source);
        await using var target = await _postgres.CreateEmptyAsync();
        await Seed.PopulateAsync(target);
        await Assert.ThrowsAsync<InvalidOperationException>(() => DatabaseCopy.CopyAsync(source, target));
        Assert.Equal(1, await target.Users.CountAsync());
        Assert.Equal(1, await target.Series.CountAsync());
    }
}

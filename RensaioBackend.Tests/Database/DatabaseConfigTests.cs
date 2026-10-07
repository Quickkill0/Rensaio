using Microsoft.Extensions.Configuration;
using Npgsql;
using RensaioBackend.Data;
using Xunit;

namespace RensaioBackend.Tests.Database;

public sealed class DatabaseConfigTests
{
    private static IConfiguration Config(params (string Key, string? Value)[] pairs)
        => new ConfigurationBuilder()
            .AddInMemoryCollection(pairs.Select(p => new KeyValuePair<string, string?>(p.Key, p.Value)))
            .Build();

    [Fact]
    public void Defaults_to_sqlite_in_the_data_directory()
    {
        var config = DatabaseConfig.Resolve(Config());
        Assert.Equal(DatabaseProvider.Sqlite, config.Provider);
        Assert.EndsWith("rensaio.db", config.SqlitePath);
        Assert.True(Path.IsPathRooted(config.SqlitePath));
        Assert.Null(config.Postgres);
    }

    [Fact]
    public void Sqlite_connection_string_is_honoured()
    {
        var config = DatabaseConfig.Resolve(Config(("ConnectionStrings:DefaultConnection", "Data Source=/data/lib.db")));
        Assert.Equal(DatabaseProvider.Sqlite, config.Provider);
        Assert.Equal(Path.GetFullPath("/data/lib.db"), config.SqlitePath);
        Assert.StartsWith("Data Source=", config.SqliteConnectionString);
    }

    [Fact]
    public void Host_alone_selects_postgres()
    {
        var config = DatabaseConfig.Resolve(Config(
            ("Database:Host", "db.example"), ("Database:Username", "rensaio"), ("Database:Password", "s;cr=et")));
        Assert.Equal(DatabaseProvider.Postgres, config.Provider);
        Assert.Equal("db.example", config.Postgres!.Host);
        Assert.Equal(5432, config.Postgres.Port);
        Assert.Equal("rensaio", config.Postgres.Database);

        var built = new NpgsqlConnectionStringBuilder(PostgresAppDbContext.BuildConnectionString(config.Postgres));
        Assert.Equal("s;cr=et", built.Password);
        Assert.Equal("Rensaio", built.ApplicationName);
    }

    [Fact]
    public void Libpq_uri_is_translated_including_ssl_parameters()
    {
        var config = DatabaseConfig.Resolve(Config(("ConnectionStrings:DefaultConnection",
            "postgresql://ren%40saio:p%40ss@postgres-rw.database.svc:5432/rensaio?sslmode=verify-full&sslrootcert=/var/run/secrets/ca.crt")));
        Assert.Equal(DatabaseProvider.Postgres, config.Provider);

        var built = new NpgsqlConnectionStringBuilder(PostgresAppDbContext.BuildConnectionString(config.Postgres!));
        Assert.Equal("postgres-rw.database.svc", built.Host);
        Assert.Equal("ren@saio", built.Username);
        Assert.Equal("p@ss", built.Password);
        Assert.Equal("rensaio", built.Database);
        Assert.Equal(SslMode.VerifyFull, built.SslMode);
        Assert.Equal("/var/run/secrets/ca.crt", built.RootCertificate);
    }

    [Theory]
    [InlineData("disable", SslMode.Disable)]
    [InlineData("allow", SslMode.Allow)]
    [InlineData("prefer", SslMode.Prefer)]
    [InlineData("require", SslMode.Require)]
    [InlineData("verify-ca", SslMode.VerifyCA)]
    [InlineData("verify-full", SslMode.VerifyFull)]
    public void Tls_modes_are_not_downgraded(string mode, SslMode expected)
    {
        var config = DatabaseConfig.Resolve(Config(("Database:Host", "localhost"), ("Database:SslMode", mode)));
        var built = new NpgsqlConnectionStringBuilder(PostgresAppDbContext.BuildConnectionString(config.Postgres!));
        Assert.Equal(expected, built.SslMode);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void Libpq_tls_environment_is_applied_without_overriding_explicit_settings(bool raw)
    {
        var settings = raw ? new PostgresSettings { RawConnectionString = "Host=localhost" } : new PostgresSettings { Host = "localhost" };
        var built = new NpgsqlConnectionStringBuilder(PostgresAppDbContext.BuildConnectionString(settings, "verify-full", "/tmp/ca.crt"));
        Assert.Equal(SslMode.VerifyFull, built.SslMode);
        Assert.Equal("/tmp/ca.crt", built.RootCertificate);
        Assert.Throws<InvalidOperationException>(() => PostgresAppDbContext.BuildConnectionString(settings, "invalid", null));

        settings = raw
            ? new PostgresSettings { RawConnectionString = "Host=localhost;SSL Mode=Require;Root Certificate=/explicit.crt" }
            : new PostgresSettings { Host = "localhost", SslMode = "require", RootCertificate = "/explicit.crt" };
        built = new NpgsqlConnectionStringBuilder(PostgresAppDbContext.BuildConnectionString(settings, "verify-full", "/tmp/ca.crt"));
        Assert.Equal(SslMode.Require, built.SslMode);
        Assert.Equal("/explicit.crt", built.RootCertificate);
    }

    [Fact]
    public void Escaped_credentials_stay_out_of_the_log_description()
    {
        var config = DatabaseConfig.Resolve(Config(("ConnectionStrings:DefaultConnection",
            "postgresql://u:p%3B%3Dx%3A%40@localhost/db?sslmode=verify-full")));
        string connection = PostgresAppDbContext.BuildConnectionString(config.Postgres!);
        Assert.Equal("p;=x:@", new NpgsqlConnectionStringBuilder(connection).Password);
        Assert.DoesNotContain("p;=x:@", PostgresAppDbContext.Describe(connection));
        Assert.DoesNotContain("Password", PostgresAppDbContext.Describe(connection));
    }

    [Fact]
    public async Task Command_rejects_invalid_arguments_without_touching_a_database()
    {
        Assert.Equal(1, await MigrateDbCommand.RunAsync(["--to", "postgres", "--unknown"], Config()));
        Assert.Equal(1, await MigrateDbCommand.RunAsync(["--to", "sqlite", "--to", "postgres"], Config()));
        Assert.Equal(1, await MigrateDbCommand.RunAsync(["--to", "mysql"], Config()));
    }

    [Fact]
    public void Npgsql_keyword_string_passes_through()
    {
        var config = DatabaseConfig.Resolve(Config(("ConnectionStrings:DefaultConnection", "Host=h;Database=d;Username=u;Password=p")));
        Assert.Equal(DatabaseProvider.Postgres, config.Provider);
        var built = new NpgsqlConnectionStringBuilder(PostgresAppDbContext.BuildConnectionString(config.Postgres!));
        Assert.Equal("h", built.Host);
        Assert.Equal("d", built.Database);
    }

    [Fact]
    public void Explicit_provider_wins_and_is_validated()
    {
        Assert.Equal(DatabaseProvider.Sqlite, DatabaseConfig.Resolve(Config(("Database:Provider", "sqlite"))).Provider);
        Assert.Throws<InvalidOperationException>(() => DatabaseConfig.Resolve(Config(("Database:Provider", "mysql"))));
        Assert.Throws<InvalidOperationException>(() => DatabaseConfig.Resolve(Config(("Database:Provider", "postgres"))));
        Assert.Throws<InvalidOperationException>(() => DatabaseConfig.Resolve(Config(
            ("Database:Provider", "sqlite"), ("ConnectionStrings:DefaultConnection", "Host=h;Database=d"))));
    }

    [Fact]
    public void Unknown_uri_parameter_is_rejected()
    {
        var ex = Assert.Throws<InvalidOperationException>(() => DatabaseConfig.Resolve(Config(
            ("ConnectionStrings:DefaultConnection", "postgresql://u:p@h/d?application_name=x"))));
        Assert.Contains("application_name", ex.Message);
    }

    [Fact]
    public void Forced_provider_resolves_both_sides_of_one_configuration()
    {
        var config = Config(("Database:Provider", "postgres"), ("Database:Host", "h"),
            ("ConnectionStrings:DefaultConnection", "Data Source=/data/lib.db"));
        Assert.Equal(Path.GetFullPath("/data/lib.db"), DatabaseConfig.Resolve(config, DatabaseProvider.Sqlite).SqlitePath);
        Assert.Equal("h", DatabaseConfig.Resolve(config, DatabaseProvider.Postgres).Postgres!.Host);
    }
}

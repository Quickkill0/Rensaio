using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using RensaioBackend.Migration;

namespace RensaioBackend.Data
{
    /// <summary>
    /// <c>RensaioBackend migrate-db --to postgres|sqlite</c>: copies the library from the
    /// current database to the other provider, using the same configuration the server
    /// reads (<c>Database:*</c>, <c>ConnectionStrings:DefaultConnection</c>). The source is
    /// never opened for writes by the copier. Exit codes: 0 done, 1 bad arguments, configuration or copy failure,
    /// 2 target not empty. Failed row copies are rolled back; prepared schema may remain.
    /// </summary>
    public static class MigrateDbCommand
    {
        public const string MarkerSuffix = ".migrated-to-postgres";

        public static async Task<int> RunAsync(string[] args, IConfiguration configuration, CancellationToken token = default)
        {
            try
            {
                return await CopyAsync(args, configuration, token).ConfigureAwait(false);
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                // Driver exceptions can contain credentials or row payloads. Never print them.
                Console.Error.WriteLine("Database copy failed; uncommitted target rows were rolled back. Check configuration, TLS, permissions and source schema. Stop Rensaio before retrying.");
                return 1;
            }
        }

        private static async Task<int> CopyAsync(string[] args, IConfiguration configuration, CancellationToken token)
        {
            DatabaseProvider? to = null;
            if (args.Length == 2 && args[0] == "--to")
                to = args[1].ToLowerInvariant() switch
                {
                    "postgres" or "postgresql" => DatabaseProvider.Postgres,
                    "sqlite" => DatabaseProvider.Sqlite,
                    _ => null
                };
            if (to is null)
            {
                Console.Error.WriteLine("usage: RensaioBackend migrate-db --to postgres|sqlite");
                Console.Error.WriteLine("Configure the PostgreSQL side with Database__Host/Port/Name/Username/Password or ConnectionStrings__DefaultConnection.");
                return 1;
            }

            DatabaseConfig sqlite, postgres;
            try
            {
                sqlite = DatabaseConfig.Resolve(configuration, DatabaseProvider.Sqlite);
                postgres = DatabaseConfig.Resolve(configuration, DatabaseProvider.Postgres);
            }
            catch (InvalidOperationException)
            {
                Console.Error.WriteLine("Invalid database configuration. Set a valid provider and connection settings.");
                return 1;
            }

            string postgresConnectionString = PostgresAppDbContext.BuildConnectionString(postgres.Postgres!);
            var sqliteOptions = new DbContextOptionsBuilder<SqliteAppDbContext>();
            var sqliteConnection = new Microsoft.Data.Sqlite.SqliteConnectionStringBuilder(sqlite.SqliteConnectionString!)
            {
                Mode = to == DatabaseProvider.Postgres ? Microsoft.Data.Sqlite.SqliteOpenMode.ReadOnly : Microsoft.Data.Sqlite.SqliteOpenMode.ReadWriteCreate,
                Pooling = false
            };
            SqliteAppDbContext.Configure(sqliteOptions, sqliteConnection.ConnectionString);
            var postgresOptions = new DbContextOptionsBuilder<PostgresAppDbContext>();
            PostgresAppDbContext.Configure(postgresOptions, postgresConnectionString);

            await using var sqliteDb = new SqliteAppDbContext(sqliteOptions.Options);
            await using var postgresDb = new PostgresAppDbContext(postgresOptions.Options);

            AppDbContext source, target;
            string sourceName = "SQLite " + sqlite.SqlitePath;
            string targetName = PostgresAppDbContext.Describe(postgresConnectionString);
            if (to == DatabaseProvider.Postgres)
            {
                if (!File.Exists(sqlite.SqlitePath!))
                {
                    Console.Error.WriteLine($"Source database not found: {sqlite.SqlitePath}");
                    return 1;
                }
                (source, target) = (sqliteDb, postgresDb);
            }
            else
            {
                if (File.Exists(sqlite.SqlitePath!) && new FileInfo(sqlite.SqlitePath!).Length > 0)
                {
                    Console.Error.WriteLine($"Target file already exists: {sqlite.SqlitePath}. Move it away first; the copy only writes into an empty database.");
                    return 2;
                }
                (source, target) = (postgresDb, sqliteDb);
                (sourceName, targetName) = (targetName, sourceName);
            }

            Console.WriteLine($"Source: {sourceName}");
            Console.WriteLine($"Target: {targetName}");

            if (!await source.Database.CanConnectAsync(token).ConfigureAwait(false))
            {
                Console.Error.WriteLine("Cannot connect to the source database.");
                return 1;
            }

            // Prepare the target schema. PostgreSQL has a complete migration set; SQLite's
            // schema comes from the model, the same way a fresh install builds it.
            if (target == postgresDb)
            {
                if (!await target.Database.CanConnectAsync(token).ConfigureAwait(false))
                {
                    Console.Error.WriteLine("Cannot connect to the target database. Check Database__Host/Port/Name/Username/Password.");
                    return 1;
                }
                // Reject existing data before schema migrations can change the target.
                if (await HasPostgresDataAsync(postgresDb, token).ConfigureAwait(false))
                {
                    Console.Error.WriteLine("The target database already contains data. The copy only writes into an empty database.");
                    return 2;
                }
                await target.Database.MigrateAsync(token).ConfigureAwait(false);
            }
            else
            {
                await target.Database.EnsureCreatedAsync(token).ConfigureAwait(false);
                await MigrationService.MarkAllMigrationsAsAppliedAsync(target, token).ConfigureAwait(false);
            }

            if (!await DatabaseCopy.IsEmptyAsync(target, token).ConfigureAwait(false))
            {
                Console.Error.WriteLine("The target database already contains data. The copy only writes into an empty database.");
                return 2;
            }

            Console.WriteLine("Copying...");
            var results = await DatabaseCopy.CopyAsync(source, target, line => Console.WriteLine("  " + line), token).ConfigureAwait(false);

            Console.WriteLine();
            Console.WriteLine($"{"Table",-26} {"Source",10} {"Target",10}");
            foreach (var r in results)
                Console.WriteLine($"{r.Table,-26} {r.SourceRows,10} {r.TargetRows,10}");

            if (to == DatabaseProvider.Postgres)
            {
                string marker = sqlite.SqlitePath + MarkerSuffix;
                await File.WriteAllTextAsync(marker, DateTime.UtcNow.ToString("O") + Environment.NewLine, token).ConfigureAwait(false);
                Console.WriteLine();
                Console.WriteLine("Done. The SQLite file was left untouched.");
                Console.WriteLine("Now start Rensaio with Database__Provider=postgres (and the same Database__* settings). To go back, start it without them.");
            }
            else
            {
                Console.WriteLine();
                Console.WriteLine($"Done. Start Rensaio with Database__Provider=sqlite (or no Database__* settings) to use {sqlite.SqlitePath}.");
            }
            return 0;
        }

        private static async Task<bool> HasPostgresDataAsync(PostgresAppDbContext db, CancellationToken token)
        {
            var connection = db.Database.GetDbConnection();
            if (connection.State != System.Data.ConnectionState.Open)
                await connection.OpenAsync(token).ConfigureAwait(false);
            var tables = new List<(string Schema, string Table)>();
            await using (var command = connection.CreateCommand())
            {
                command.CommandText = "SELECT schemaname, tablename FROM pg_catalog.pg_tables WHERE schemaname = current_schema() AND tablename <> '__EFMigrationsHistory'";
                await using var reader = await command.ExecuteReaderAsync(token).ConfigureAwait(false);
                while (await reader.ReadAsync(token).ConfigureAwait(false))
                    tables.Add((reader.GetString(0), reader.GetString(1)));
            }
            foreach (var (schema, table) in tables)
            {
                await using var command = connection.CreateCommand();
                static string Quote(string name) => "\"" + name.Replace("\"", "\"\"") + "\"";
                command.CommandText = $"SELECT EXISTS (SELECT 1 FROM {Quote(schema)}.{Quote(table)})";
                if ((bool)(await command.ExecuteScalarAsync(token).ConfigureAwait(false))!)
                    return true;
            }
            return false;
        }
    }
}

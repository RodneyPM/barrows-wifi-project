using System.Text.RegularExpressions;
using Microsoft.Data.SqlClient;

namespace BarrowsWifi.Api.Data;

/// <summary>
/// Applies the numbered .sql scripts in db/migrations on startup, once each,
/// tracked in dbo.__SchemaVersions. Scripts are split into batches on lines containing only "GO".
/// </summary>
public static class DbMigrator
{
    private static readonly Regex GoSeparator = new(@"^\s*GO\s*$", RegexOptions.Multiline | RegexOptions.IgnoreCase);

    public static async Task RunAsync(string connectionString, string scriptsFolder, ILogger logger, CancellationToken ct = default)
    {
        var target = new SqlConnectionStringBuilder(connectionString);
        var database = target.InitialCatalog;

        await WaitForServerAsync(target, logger, ct);
        await EnsureDatabaseAsync(target, database, ct);

        await using var conn = new SqlConnection(connectionString);
        await conn.OpenAsync(ct);

        await ExecAsync(conn, @"IF OBJECT_ID('dbo.__SchemaVersions') IS NULL
            CREATE TABLE dbo.__SchemaVersions (ScriptName NVARCHAR(260) PRIMARY KEY, AppliedAt DATETIMEOFFSET NOT NULL);", ct);

        var applied = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        await using (var cmd = new SqlCommand("SELECT ScriptName FROM dbo.__SchemaVersions", conn))
        await using (var reader = await cmd.ExecuteReaderAsync(ct))
        {
            while (await reader.ReadAsync(ct)) applied.Add(reader.GetString(0));
        }

        var scripts = Directory.GetFiles(scriptsFolder, "*.sql").OrderBy(p => Path.GetFileName(p), StringComparer.Ordinal);
        foreach (var path in scripts)
        {
            var name = Path.GetFileName(path);
            if (applied.Contains(name)) continue;

            logger.LogInformation("Applying migration {Script}", name);
            var sql = await File.ReadAllTextAsync(path, ct);
            await using var tx = (SqlTransaction)await conn.BeginTransactionAsync(ct);
            foreach (var batch in GoSeparator.Split(sql).Where(b => !string.IsNullOrWhiteSpace(b)))
            {
                await ExecAsync(conn, batch, ct, timeoutSeconds: 300, tx);
            }

            await using var record = new SqlCommand(
                "INSERT INTO dbo.__SchemaVersions (ScriptName, AppliedAt) VALUES (@n, SYSDATETIMEOFFSET())", conn, tx);
            record.Parameters.AddWithValue("@n", name);
            await record.ExecuteNonQueryAsync(ct);
            await tx.CommitAsync(ct);
        }
    }

    private static async Task WaitForServerAsync(SqlConnectionStringBuilder target, ILogger logger, CancellationToken ct)
    {
        var master = new SqlConnectionStringBuilder(target.ConnectionString) { InitialCatalog = "master" };
        for (var attempt = 1; ; attempt++)
        {
            try
            {
                await using var conn = new SqlConnection(master.ConnectionString);
                await conn.OpenAsync(ct);
                return;
            }
            catch (SqlException) when (attempt < 30)
            {
                logger.LogInformation("Waiting for SQL Server ({Attempt}/30)...", attempt);
                await Task.Delay(TimeSpan.FromSeconds(2), ct);
            }
        }
    }

    private static async Task EnsureDatabaseAsync(SqlConnectionStringBuilder target, string database, CancellationToken ct)
    {
        var master = new SqlConnectionStringBuilder(target.ConnectionString) { InitialCatalog = "master" };
        await using var conn = new SqlConnection(master.ConnectionString);
        await conn.OpenAsync(ct);
        await ExecAsync(conn, $"IF DB_ID(N'{database.Replace("'", "''")}') IS NULL CREATE DATABASE [{database.Replace("]", "]]")}];", ct);
    }

    private static async Task ExecAsync(SqlConnection conn, string sql, CancellationToken ct, int timeoutSeconds = 30,
        SqlTransaction? tx = null)
    {
        await using var cmd = new SqlCommand(sql, conn, tx) { CommandTimeout = timeoutSeconds };
        await cmd.ExecuteNonQueryAsync(ct);
    }
}

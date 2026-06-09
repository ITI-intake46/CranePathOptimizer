using Microsoft.EntityFrameworkCore;
using System.Data;
using System.Data.Common;

namespace CraneOptimizer.Infrastructure.Contexts
{
    public static class SqliteMigrationCompatibilityGuard
    {
        public const string CatalogOperatingConditionLabelMigrationId = "20260609011000_AddLoadChartOperatingConditionLabel";
        public const string CreatedProjectOperatingConditionLabelMigrationId = "20260609011100_AddLoadChartSnapshotOperatingConditionLabel";

        public static Task EnsureCatalogOperatingConditionSchemaAsync(
            DbContext context,
            CancellationToken cancellationToken = default)
        {
            return EnsureColumnAndMigrationHistoryAsync(
                context,
                tableName: "TargetPoints",
                columnName: "OperatingConditionLabel",
                columnSql: "TEXT",
                migrationId: CatalogOperatingConditionLabelMigrationId,
                cancellationToken);
        }

        public static Task EnsureCreatedProjectOperatingConditionSchemaAsync(
            DbContext context,
            CancellationToken cancellationToken = default)
        {
            return EnsureColumnAndMigrationHistoryAsync(
                context,
                tableName: "ProjectCraneLoadChartSnapshots",
                columnName: "OperatingConditionLabel",
                columnSql: "TEXT",
                migrationId: CreatedProjectOperatingConditionLabelMigrationId,
                cancellationToken);
        }

        private static async Task EnsureColumnAndMigrationHistoryAsync(
            DbContext context,
            string tableName,
            string columnName,
            string columnSql,
            string migrationId,
            CancellationToken cancellationToken)
        {
            ArgumentNullException.ThrowIfNull(context);

            var connection = context.Database.GetDbConnection();
            bool shouldClose = connection.State != ConnectionState.Open;

            if (shouldClose)
                await connection.OpenAsync(cancellationToken).ConfigureAwait(false);

            try
            {
                if (!await TableExistsAsync(connection, tableName, cancellationToken).ConfigureAwait(false))
                    return;

                bool columnExists = await ColumnExistsAsync(connection, tableName, columnName, cancellationToken).ConfigureAwait(false);
                bool historyExists = await TableExistsAsync(connection, "__EFMigrationsHistory", cancellationToken).ConfigureAwait(false);

                if (!columnExists)
                {
                    await ExecuteNonQueryAsync(
                        connection,
                        $"ALTER TABLE \"{EscapeIdentifier(tableName)}\" ADD COLUMN \"{EscapeIdentifier(columnName)}\" {columnSql};",
                        cancellationToken).ConfigureAwait(false);
                }

                if (historyExists
                    && !await MigrationHistoryContainsAsync(connection, migrationId, cancellationToken).ConfigureAwait(false))
                {
                    string productVersion = await ResolveProductVersionAsync(connection, cancellationToken).ConfigureAwait(false);
                    await InsertMigrationHistoryAsync(connection, migrationId, productVersion, cancellationToken).ConfigureAwait(false);
                }
            }
            finally
            {
                if (shouldClose)
                    connection.Close();
            }
        }

        private static async Task<bool> TableExistsAsync(
            DbConnection connection,
            string tableName,
            CancellationToken cancellationToken)
        {
            using var command = connection.CreateCommand();
            command.CommandText = "SELECT 1 FROM sqlite_master WHERE type = 'table' AND name = $name LIMIT 1;";
            AddParameter(command, "$name", tableName);
            object? value = await command.ExecuteScalarAsync(cancellationToken).ConfigureAwait(false);
            return value != null && value != DBNull.Value;
        }

        private static async Task<bool> ColumnExistsAsync(
            DbConnection connection,
            string tableName,
            string columnName,
            CancellationToken cancellationToken)
        {
            using var command = connection.CreateCommand();
            command.CommandText = $"PRAGMA table_info('{EscapeStringLiteral(tableName)}');";

            using var reader = await command.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false);
            while (await reader.ReadAsync(cancellationToken).ConfigureAwait(false))
            {
                string? name = reader["name"]?.ToString();
                if (string.Equals(name, columnName, StringComparison.OrdinalIgnoreCase))
                    return true;
            }

            return false;
        }

        private static async Task<bool> MigrationHistoryContainsAsync(
            DbConnection connection,
            string migrationId,
            CancellationToken cancellationToken)
        {
            using var command = connection.CreateCommand();
            command.CommandText = "SELECT 1 FROM \"__EFMigrationsHistory\" WHERE \"MigrationId\" = $migrationId LIMIT 1;";
            AddParameter(command, "$migrationId", migrationId);
            object? value = await command.ExecuteScalarAsync(cancellationToken).ConfigureAwait(false);
            return value != null && value != DBNull.Value;
        }

        private static async Task<string> ResolveProductVersionAsync(
            DbConnection connection,
            CancellationToken cancellationToken)
        {
            using var command = connection.CreateCommand();
            command.CommandText = "SELECT \"ProductVersion\" FROM \"__EFMigrationsHistory\" ORDER BY \"MigrationId\" DESC LIMIT 1;";
            object? value = await command.ExecuteScalarAsync(cancellationToken).ConfigureAwait(false);
            string? productVersion = value?.ToString();

            return string.IsNullOrWhiteSpace(productVersion)
                ? "9.0.14"
                : productVersion;
        }

        private static async Task InsertMigrationHistoryAsync(
            DbConnection connection,
            string migrationId,
            string productVersion,
            CancellationToken cancellationToken)
        {
            using var command = connection.CreateCommand();
            command.CommandText = "INSERT INTO \"__EFMigrationsHistory\" (\"MigrationId\", \"ProductVersion\") VALUES ($migrationId, $productVersion);";
            AddParameter(command, "$migrationId", migrationId);
            AddParameter(command, "$productVersion", productVersion);
            await command.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);
        }

        private static async Task ExecuteNonQueryAsync(
            DbConnection connection,
            string sql,
            CancellationToken cancellationToken)
        {
            using var command = connection.CreateCommand();
            command.CommandText = sql;
            await command.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);
        }

        private static void AddParameter(DbCommand command, string name, object value)
        {
            var parameter = command.CreateParameter();
            parameter.ParameterName = name;
            parameter.Value = value;
            command.Parameters.Add(parameter);
        }

        private static string EscapeIdentifier(string identifier)
        {
            return identifier.Replace("\"", "\"\"");
        }

        private static string EscapeStringLiteral(string value)
        {
            return value.Replace("'", "''");
        }
    }
}
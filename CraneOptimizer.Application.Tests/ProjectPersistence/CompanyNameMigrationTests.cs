using CraneOptimizer.Infrastructure.Contexts;
using CraneOptimizer.Infrastructure.Contexts.CreatedProject;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;

namespace CraneOptimizer.Application.Tests.ProjectPersistence;

public sealed class CompanyNameMigrationTests
{
    [Fact]
    public void CraneDbContext_DiscoversCompanyNameMigration()
    {
        var options = new DbContextOptionsBuilder<CraneDbContext>()
            .UseSqlite("Data Source=:memory:")
            .Options;

        using var context = new CraneDbContext(options);

        Assert.Contains(
            SqliteMigrationCompatibilityGuard.CatalogCompanyNameMigrationId,
            context.Database.GetMigrations());
    }

    [Fact]
    public void CreatedProjectDbContext_DiscoversCompanyNameMigration()
    {
        var options = new DbContextOptionsBuilder<CreatedProjectDbContext>()
            .UseSqlite("Data Source=:memory:")
            .Options;

        using var context = new CreatedProjectDbContext(options);

        Assert.Contains(
            SqliteMigrationCompatibilityGuard.CreatedProjectCompanyNameMigrationId,
            context.Database.GetMigrations());
    }

    [Fact]
    public void CraneDbContext_FreshDatabaseContainsCompanyNameColumn()
    {
        using var connection = new SqliteConnection("Data Source=:memory:");
        connection.Open();

        var options = new DbContextOptionsBuilder<CraneDbContext>()
            .UseSqlite(connection)
            .Options;

        using var context = new CraneDbContext(options);
        context.Database.Migrate();

        Assert.True(ColumnExists(connection, "Projects", "CompanyName"));
    }

    [Fact]
    public void CreatedProjectDbContext_FreshDatabaseContainsCompanyNameColumn()
    {
        using var connection = new SqliteConnection("Data Source=:memory:");
        connection.Open();

        var options = new DbContextOptionsBuilder<CreatedProjectDbContext>()
            .UseSqlite(connection)
            .Options;

        using var context = new CreatedProjectDbContext(options);
        context.Database.Migrate();

        Assert.True(ColumnExists(connection, "ProjectInfo", "CompanyName"));
    }

    private static bool ColumnExists(
        SqliteConnection connection,
        string tableName,
        string columnName)
    {
        using var command = connection.CreateCommand();
        command.CommandText = "SELECT 1 FROM pragma_table_info($tableName) WHERE name = $columnName LIMIT 1;";
        command.Parameters.AddWithValue("$tableName", tableName);
        command.Parameters.AddWithValue("$columnName", columnName);

        return command.ExecuteScalar() is not null;
    }
}

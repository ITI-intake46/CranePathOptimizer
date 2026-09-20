using CraneOptimizer.Infrastructure.Persistence;
using Microsoft.Data.Sqlite;
using System.Security.Cryptography;
using System.Text.Json;

namespace CraneOptimizer.Application.Tests.Persistence;

public sealed class CatalogDatabaseBootstrapperTests
{
    [Fact]
    public void FirstRun_CopiesCanonicalCatalogByteForByte()
    {
        var root = CreateTempRoot();
        try
        {
            var app = Path.Combine(root, "app");
            var data = Path.Combine(app, "Data");
            Directory.CreateDirectory(data);
            var canonical = Path.Combine(data, CatalogDatabaseBootstrapper.CanonicalCatalogFileName);
            File.Copy(Path.Combine(AppContext.BaseDirectory, "Fixtures", "CanonicalCatalog.crane.db"), canonical);
            File.Copy(
                Path.Combine(AppContext.BaseDirectory, "Fixtures", "catalog-manifest.json"),
                Path.Combine(data, CatalogDatabaseBootstrapper.CanonicalCatalogManifestFileName));
            var writable = Path.Combine(root, "user", CatalogDatabaseBootstrapper.CatalogFileName);

            var bootstrapper = new CatalogDatabaseBootstrapper(app, writable);
            bootstrapper.EnsureWritableCatalogExists();

            Assert.True(File.Exists(writable));
            Assert.Equal(Sha256(canonical), Sha256(writable));
        }
        finally
        {
            Directory.Delete(root, recursive: true);
        }
    }

    [Fact]
    public void Backup_IsVerifiedAndPreservesOriginalBytes()
    {
        var root = CreateTempRoot();
        try
        {
            var app = Path.Combine(root, "app");
            var data = Path.Combine(app, "Data");
            Directory.CreateDirectory(data);
            var canonical = Path.Combine(data, CatalogDatabaseBootstrapper.CanonicalCatalogFileName);
            File.Copy(Path.Combine(AppContext.BaseDirectory, "Fixtures", "CanonicalCatalog.crane.db"), canonical);
            var writable = Path.Combine(root, "user", CatalogDatabaseBootstrapper.CatalogFileName);
            var bootstrapper = new CatalogDatabaseBootstrapper(app, writable);
            bootstrapper.EnsureWritableCatalogExists();

            var backup = bootstrapper.CreateVerifiedPreMigrationBackup();

            Assert.True(File.Exists(backup));
            Assert.Equal(Sha256(writable), Sha256(backup));
        }
        finally
        {
            Directory.Delete(root, recursive: true);
        }
    }

    [Fact]
    public void ExistingZeroByteCatalog_FailsClosedInsteadOfMigrating()
    {
        var root = CreateTempRoot();
        try
        {
            var app = Path.Combine(root, "app");
            var data = Path.Combine(app, "Data");
            Directory.CreateDirectory(data);
            File.Copy(Path.Combine(AppContext.BaseDirectory, "Fixtures", "CanonicalCatalog.crane.db"), Path.Combine(data, CatalogDatabaseBootstrapper.CanonicalCatalogFileName));
            var writable = Path.Combine(root, "user", CatalogDatabaseBootstrapper.CatalogFileName);
            Directory.CreateDirectory(Path.GetDirectoryName(writable)!);
            File.WriteAllBytes(writable, Array.Empty<byte>());

            var bootstrapper = new CatalogDatabaseBootstrapper(app, writable);

            Assert.Throws<InvalidDataException>(() => bootstrapper.EnsureWritableCatalogExists());
            Assert.Throws<InvalidDataException>(() => bootstrapper.CreateVerifiedPreMigrationBackup());
        }
        finally
        {
            Directory.Delete(root, recursive: true);
        }
    }

    [Fact]
    public void CanonicalWithoutManagedIdentity_FailsClosed()
    {
        var root = CreateTempRoot();
        try
        {
            var app = Path.Combine(root, "app");
            var data = Path.Combine(app, "Data");
            Directory.CreateDirectory(data);
            File.WriteAllText(Path.Combine(data, CatalogDatabaseBootstrapper.CanonicalCatalogFileName), "not-a-managed-sqlite-catalog");
            var writable = Path.Combine(root, "user", CatalogDatabaseBootstrapper.CatalogFileName);

            var bootstrapper = new CatalogDatabaseBootstrapper(app, writable);
            Assert.Throws<InvalidDataException>(() => bootstrapper.EnsureWritableCatalogExists());
            Assert.False(File.Exists(writable));
        }
        finally { Directory.Delete(root, recursive: true); }
    }

    [Fact]
    public void CanonicalAcceptanceCatalog_HasExactly808VersionedPointsAndManifestHashMatches()
    {
        var catalog = Path.Combine(AppContext.BaseDirectory, "Fixtures", "CanonicalCatalog.crane.db");
        var manifestPath = Path.Combine(AppContext.BaseDirectory, "Fixtures", "catalog-manifest.json");
        Assert.True(File.Exists(catalog));
        Assert.True(File.Exists(manifestPath));

        using var connection = new SqliteConnection($"Data Source={catalog};Mode=ReadOnly");
        connection.Open();
        using var command = connection.CreateCommand();
        command.CommandText = "SELECT COUNT(*) FROM TargetPoints";
        Assert.Equal(808L, (long)command.ExecuteScalar()!);

        command.CommandText = "SELECT COUNT(*) FROM Cranes";
        Assert.True((long)command.ExecuteScalar()! >= 1);

        using var doc = JsonDocument.Parse(File.ReadAllText(manifestPath));
        Assert.Equal(808, doc.RootElement.GetProperty("targetPointCount").GetInt32());
        Assert.Equal("SOFTWARE ACCEPTANCE FIXTURE", doc.RootElement.GetProperty("fixtureType").GetString());
        Assert.Equal(
            doc.RootElement.GetProperty("sha256").GetString(),
            Sha256(catalog),
            ignoreCase: true);
    }

    [Fact]
    public void ExistingLegacyCatalog_IsBackedUpAndReconciledToCanonicalIdentity()
    {
        var root = CreateTempRoot();
        try
        {
            var app = Path.Combine(root, "app"); var data = Path.Combine(app, "Data"); Directory.CreateDirectory(data);
            var canonical = Path.Combine(data, CatalogDatabaseBootstrapper.CanonicalCatalogFileName);
            File.Copy(Path.Combine(AppContext.BaseDirectory, "Fixtures", "CanonicalCatalog.crane.db"), canonical);
            var writable = Path.Combine(root, "user", CatalogDatabaseBootstrapper.CatalogFileName);
            Directory.CreateDirectory(Path.GetDirectoryName(writable)!);
            using (var c = new SqliteConnection($"Data Source={writable}")) { c.Open(); using var cmd=c.CreateCommand(); cmd.CommandText="CREATE TABLE Legacy(Id INTEGER PRIMARY KEY); INSERT INTO Legacy VALUES(1);"; cmd.ExecuteNonQuery(); }

            var bootstrapper = new CatalogDatabaseBootstrapper(app, writable);
            bootstrapper.EnsureWritableCatalogExists();

            Assert.Equal(Sha256(canonical), Sha256(writable));
            var backup = Assert.Single(Directory.GetFiles(bootstrapper.BackupDirectory, "crane.pre-catalog-reconcile.*.db"));
            Assert.NotEqual(Sha256(writable), Sha256(backup));
        }
        finally { Directory.Delete(root, true); }
    }

    [Fact]
    public void ExistingMatchingCanonicalCatalog_IsRetainedWithoutReconciliationBackup()
    {
        var root = CreateTempRoot();
        try
        {
            var app=Path.Combine(root,"app"); var data=Path.Combine(app,"Data"); Directory.CreateDirectory(data);
            var canonical=Path.Combine(data,CatalogDatabaseBootstrapper.CanonicalCatalogFileName);
            File.Copy(Path.Combine(AppContext.BaseDirectory,"Fixtures","CanonicalCatalog.crane.db"),canonical);
            var writable=Path.Combine(root,"user",CatalogDatabaseBootstrapper.CatalogFileName); Directory.CreateDirectory(Path.GetDirectoryName(writable)!); File.Copy(canonical,writable);
            var before=File.GetLastWriteTimeUtc(writable);
            new CatalogDatabaseBootstrapper(app,writable).EnsureWritableCatalogExists();
            Assert.Equal(Sha256(canonical),Sha256(writable));
            Assert.False(Directory.Exists(Path.Combine(Path.GetDirectoryName(writable)!,"Backups")));
        } finally { Directory.Delete(root,true); }
    }

    [Fact]
    public void CompanionManifestHashMismatch_FailsClosedBeforeProvisioning()
    {
        var root = CreateTempRoot();
        try
        {
            var app = Path.Combine(root, "app");
            var data = Path.Combine(app, "Data");
            Directory.CreateDirectory(data);
            File.Copy(
                Path.Combine(AppContext.BaseDirectory, "Fixtures", "CanonicalCatalog.crane.db"),
                Path.Combine(data, CatalogDatabaseBootstrapper.CanonicalCatalogFileName));

            using var doc = JsonDocument.Parse(File.ReadAllText(Path.Combine(AppContext.BaseDirectory, "Fixtures", "catalog-manifest.json")));
            var manifest = new Dictionary<string, object?>
            {
                ["catalogId"] = doc.RootElement.GetProperty("catalogId").GetString(),
                ["catalogVersion"] = doc.RootElement.GetProperty("catalogVersion").GetString(),
                ["fixtureType"] = doc.RootElement.GetProperty("fixtureType").GetString(),
                ["sha256"] = new string('0', 64)
            };
            File.WriteAllText(
                Path.Combine(data, CatalogDatabaseBootstrapper.CanonicalCatalogManifestFileName),
                JsonSerializer.Serialize(manifest));

            var writable = Path.Combine(root, "user", CatalogDatabaseBootstrapper.CatalogFileName);
            var bootstrapper = new CatalogDatabaseBootstrapper(app, writable);

            var ex = Assert.Throws<InvalidDataException>(() => bootstrapper.EnsureWritableCatalogExists());
            Assert.Contains("SHA-256", ex.Message);
            Assert.False(File.Exists(writable));
        }
        finally
        {
            Directory.Delete(root, recursive: true);
        }
    }

    private static string CreateTempRoot()
    {
        var root = Path.Combine(Path.GetTempPath(), "CraneOptCatalogTests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(root);
        return root;
    }

    private static string Sha256(string path)
    {
        using var sha = SHA256.Create();
        using var stream = File.OpenRead(path);
        return Convert.ToHexString(sha.ComputeHash(stream)).ToLowerInvariant();
    }
}

using System.IO;
using System.Security.Cryptography;
using System.Text.Json;
using Microsoft.Data.Sqlite;

namespace CraneOptimizer.Infrastructure.Persistence
{
    /// <summary>
    /// Owns the writable catalog lifecycle. The shipped catalog remains an immutable
    /// application asset; a byte-for-byte copy is created under LocalAppData on first run.
    /// Any operation that can mutate an existing catalog must be preceded by a verified
    /// backup. Backup failures are fatal by design.
    /// </summary>
    public sealed class CatalogDatabaseBootstrapper
    {
        public const string CatalogFileName = "crane.db";
        public const string CanonicalCatalogFileName = "CanonicalCatalog.crane.db";
        public const string CanonicalCatalogManifestFileName = "CanonicalCatalog.manifest.json";

        public string WritableCatalogPath { get; }
        public string CanonicalCatalogPath { get; }
        public string CanonicalCatalogManifestPath { get; }
        public string BackupDirectory { get; }

        public CatalogDatabaseBootstrapper(string? applicationBaseDirectory = null, string? writableCatalogPath = null)
        {
            applicationBaseDirectory ??= AppContext.BaseDirectory;
            var localRoot = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
            if (string.IsNullOrWhiteSpace(localRoot))
                throw new InvalidOperationException("LocalApplicationData is not available; CraneOpt cannot create a writable catalog safely.");

            WritableCatalogPath = writableCatalogPath
                ?? Environment.GetEnvironmentVariable("CRANEOPT_CATALOG_PATH")
                ?? Path.Combine(localRoot, "CraneOpt", "Data", CatalogFileName);
            CanonicalCatalogPath = Path.Combine(applicationBaseDirectory, "Data", CanonicalCatalogFileName);
            CanonicalCatalogManifestPath = Path.Combine(applicationBaseDirectory, "Data", CanonicalCatalogManifestFileName);
            BackupDirectory = Path.Combine(Path.GetDirectoryName(WritableCatalogPath)!, "Backups");
        }

        public void EnsureWritableCatalogExists()
        {
            Directory.CreateDirectory(Path.GetDirectoryName(WritableCatalogPath)!);

            ValidateNonEmptyFile(CanonicalCatalogPath, "canonical catalog");
            var canonicalIdentity = ReadAndValidateCanonicalIdentity();

            if (File.Exists(WritableCatalogPath))
            {
                ValidateNonEmptyFile(WritableCatalogPath, "writable catalog");
                if (HasSameManagedCatalogIdentity(canonicalIdentity, WritableCatalogPath))
                    return;

                // A legacy/stale catalog is preserved before the managed acceptance catalog
                // replaces it. This prevents an upgraded installation from retaining the
                // old 94/136-point seed while the release claims an 808-point catalog.
                CreateVerifiedCatalogReconciliationBackup();
                CopyAndVerify(CanonicalCatalogPath, WritableCatalogPath);
                return;
            }

            CopyAndVerify(CanonicalCatalogPath, WritableCatalogPath);
        }

        private (string Id, string Version) ReadAndValidateCanonicalIdentity()
        {
            // Official REV05 packages ship a companion manifest next to the immutable SQLite
            // catalog. Validate the exact bytes first so startup never depends solely on a
            // schema query against a copied/runtime SQLite file.
            if (File.Exists(CanonicalCatalogManifestPath))
            {
                try
                {
                    using var document = JsonDocument.Parse(File.ReadAllText(CanonicalCatalogManifestPath));
                    var root = document.RootElement;
                    var id = root.GetProperty("catalogId").GetString();
                    var version = root.GetProperty("catalogVersion").GetString();
                    var expectedHash = root.GetProperty("sha256").GetString();
                    var fixtureType = root.TryGetProperty("fixtureType", out var fixture) ? fixture.GetString() : null;

                    if (string.IsNullOrWhiteSpace(id) || string.IsNullOrWhiteSpace(version) || string.IsNullOrWhiteSpace(expectedHash))
                        throw new InvalidDataException($"Canonical catalog manifest is missing catalogId/catalogVersion/sha256: {CanonicalCatalogManifestPath}");
                    if (!string.Equals(fixtureType, "SOFTWARE ACCEPTANCE FIXTURE", StringComparison.Ordinal))
                        throw new InvalidDataException($"Canonical catalog manifest has unexpected fixtureType '{fixtureType}'.");

                    var actualHash = Convert.ToHexString(ComputeSha256(CanonicalCatalogPath)).ToLowerInvariant();
                    if (!string.Equals(expectedHash, actualHash, StringComparison.OrdinalIgnoreCase))
                        throw new InvalidDataException($"Canonical catalog SHA-256 does not match its release manifest. Expected {expectedHash}, actual {actualHash}: {CanonicalCatalogPath}");

                    // The embedded CatalogManifest is a second consistency signal. If it can
                    // be read, it must agree. If an older SQLite provider cannot query that
                    // auxiliary table, the exact signed/generated byte hash above still
                    // establishes the canonical identity without weakening provenance.
                    var embedded = ReadCatalogIdentity(CanonicalCatalogPath);
                    if (embedded is not null &&
                        (!string.Equals(id, embedded.Value.Id, StringComparison.Ordinal) ||
                         !string.Equals(version, embedded.Value.Version, StringComparison.Ordinal)))
                    {
                        throw new InvalidDataException("Canonical catalog embedded identity does not match its companion manifest.");
                    }

                    return (id!, version!);
                }
                catch (JsonException ex)
                {
                    throw new InvalidDataException($"Canonical catalog manifest is invalid JSON: {CanonicalCatalogManifestPath}", ex);
                }
            }

            // Backward-compatible path used by focused unit tests and older developer copies.
            // Official release packages are required by the acceptance verifier to carry the
            // companion manifest, so production provenance remains hash-bound.
            return ReadCatalogIdentity(CanonicalCatalogPath)
                ?? throw new InvalidDataException($"Canonical catalog is not a managed versioned catalog and no valid companion manifest was supplied: {CanonicalCatalogPath}");
        }


        public string CreateVerifiedCatalogReconciliationBackup()
        {
            ValidateNonEmptyFile(WritableCatalogPath, "writable catalog");
            Directory.CreateDirectory(BackupDirectory);
            var stamp = DateTime.UtcNow.ToString("yyyyMMddTHHmmssfffffffZ");
            var backupPath = Path.Combine(BackupDirectory, $"crane.pre-catalog-reconcile.{stamp}.db");
            CopyAndVerify(WritableCatalogPath, backupPath);
            return backupPath;
        }

        private static bool HasSameManagedCatalogIdentity((string Id, string Version) canonical, string writablePath)
        {
            var writable = ReadCatalogIdentity(writablePath);
            return writable is not null
                && string.Equals(canonical.Id, writable.Value.Id, StringComparison.Ordinal)
                && string.Equals(canonical.Version, writable.Value.Version, StringComparison.Ordinal);
        }

        private static (string Id, string Version)? ReadCatalogIdentity(string path)
        {
            try
            {
                var cs = new SqliteConnectionStringBuilder { DataSource = path, Mode = SqliteOpenMode.ReadOnly }.ToString();
                using var connection = new SqliteConnection(cs);
                connection.Open();
                using var command = connection.CreateCommand();
                command.CommandText = "SELECT Key, Value FROM CatalogManifest WHERE Key IN ('CatalogId','CatalogVersion')";
                using var reader = command.ExecuteReader();
                string? id = null, version = null;
                while (reader.Read())
                {
                    var key = reader.GetString(0); var value = reader.GetString(1);
                    if (key == "CatalogId") id = value; else if (key == "CatalogVersion") version = value;
                }
                return string.IsNullOrWhiteSpace(id) || string.IsNullOrWhiteSpace(version) ? null : (id, version);
            }
            catch (SqliteException)
            {
                // Existing historical catalogs may predate CatalogManifest. Treat them as
                // stale rather than attempting to infer identity from point counts.
                return null;
            }
        }

        public string CreateVerifiedPreMigrationBackup()
        {
            ValidateNonEmptyFile(WritableCatalogPath, "writable catalog");
            Directory.CreateDirectory(BackupDirectory);

            var stamp = DateTime.UtcNow.ToString("yyyyMMddTHHmmssfffffffZ");
            var backupPath = Path.Combine(BackupDirectory, $"crane.pre-migration.{stamp}.db");
            CopyAndVerify(WritableCatalogPath, backupPath);
            return backupPath;
        }

        private static void CopyAndVerify(string source, string destination)
        {
            var temp = destination + ".tmp";
            try
            {
                File.Copy(source, temp, overwrite: true);
                using (var stream = new FileStream(temp, FileMode.Open, FileAccess.Read, FileShare.Read, 4096, FileOptions.WriteThrough))
                {
                    if (stream.Length == 0)
                        throw new IOException($"Copy of '{source}' produced a zero-byte catalog.");
                }

                var sourceHash = ComputeSha256(source);
                var copiedHash = ComputeSha256(temp);
                if (!sourceHash.SequenceEqual(copiedHash))
                    throw new IOException($"Catalog copy verification failed for '{destination}'.");

                File.Move(temp, destination, overwrite: true);
            }
            catch
            {
                try { if (File.Exists(temp)) File.Delete(temp); } catch { }
                throw;
            }
        }

        private static byte[] ComputeSha256(string path)
        {
            using var sha = SHA256.Create();
            using var stream = File.OpenRead(path);
            return sha.ComputeHash(stream);
        }

        private static void ValidateNonEmptyFile(string path, string description)
        {
            if (!File.Exists(path))
                throw new FileNotFoundException($"Required {description} was not found: {path}", path);

            var length = new FileInfo(path).Length;
            if (length <= 0)
                throw new InvalidDataException($"Required {description} is zero bytes and will not be migrated: {path}");
        }
    }
}

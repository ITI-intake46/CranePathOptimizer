using System;
using System.IO;

namespace CraneOptimizer.Application.Services
{
    /// <summary>
    /// Resolves all project-owned output paths relative to the active project root.
    /// Throws <see cref="InvalidOperationException"/> if no project is currently loaded.
    /// Creates each directory on demand when the path is first resolved.
    /// </summary>
    public sealed class ProjectOutputPathService : IProjectOutputPathService
    {
        private readonly IActiveProjectPathProvider _pathProvider;

        public ProjectOutputPathService(IActiveProjectPathProvider pathProvider)
        {
            _pathProvider = pathProvider ?? throw new ArgumentNullException(nameof(pathProvider));
        }

        // ── Internal helpers ───────────────────────────────────────────────

        /// <summary>
        /// Returns the active project root, throwing if none is loaded.
        /// No fallbacks to environment folders.
        /// </summary>
        private string RequireProjectRoot()
        {
            var root = _pathProvider.GetActiveProjectPath();
            if (string.IsNullOrWhiteSpace(root))
                throw new InvalidOperationException(
                    "No active project is loaded. Please create, open, or save a project first.");
            return root;
        }

        /// <summary>Builds a path under the project root and creates the directory.</summary>
        private string Resolve(params string[] segments)
        {
            var root = RequireProjectRoot();
            var parts = new string[segments.Length + 1];
            parts[0] = root;
            segments.CopyTo(parts, 1);
            var path = Path.Combine(parts);
            Directory.CreateDirectory(path);
            return path;
        }

        // ── Root ──────────────────────────────────────────────────────────

        public string GetProjectRoot()
        {
            var root = RequireProjectRoot();
            return root;
        }

        // ── Exports (final user deliverables) ─────────────────────────────

        public string GetExportsDirectory()         => Resolve("Exports");
        public string GetUserPdfExportsDirectory()  => Resolve("Exports", "PDF");
        public string GetUserExcelExportsDirectory()=> Resolve("Exports", "Excel");
        public string GetUserCsvExportsDirectory()  => Resolve("Exports", "CSV");

        // ── Reports (internal diagnostics / analysis) ──────────────────────

        public string GetReportsDirectory()         => Resolve("Reports");
        public string GetGaReportsDirectory()       => Resolve("Reports", "GA");

        public string GetGaRunReportsDirectory(string runId)
        {
            if (string.IsNullOrWhiteSpace(runId))
                throw new ArgumentException("runId must not be null or whitespace.", nameof(runId));
            return Resolve("Reports", "GA", SanitizeSegment(runId));
        }

        public string GetGaHistoryDirectory()          => Resolve("Reports", "GA", "History");
        public string GetIfcAuditReportsDirectory()    => Resolve("Reports", "IFC");
        public string GetDataSourceAuditDirectory()    => Resolve("Reports", "DataSourceAudit");

        // ── Logs (runtime / profiling) ────────────────────────────────────

        public string GetLogsDirectory()    => Resolve("Logs");
        public string GetGaLogsDirectory()  => Resolve("Logs", "GA");

        // ── Models (imported source files) ────────────────────────────────

        public string GetModelsDirectory()    => Resolve("Models");
        public string GetIfcModelsDirectory() => Resolve("Models", "IFC");
        public string GetDxfModelsDirectory() => Resolve("Models", "DXF");

        // ── Private utility ───────────────────────────────────────────────

        /// <summary>
        /// Sanitizes a path segment so it is safe to use as a directory name.
        /// Replaces path-separator characters and trims whitespace.
        /// </summary>
        private static string SanitizeSegment(string value)
        {
            var invalid = Path.GetInvalidFileNameChars();
            var chars = value.ToCharArray();
            for (int i = 0; i < chars.Length; i++)
            {
                if (Array.IndexOf(invalid, chars[i]) >= 0)
                    chars[i] = '_';
            }
            return new string(chars).Trim();
        }
    }
}

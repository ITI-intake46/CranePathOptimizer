namespace CraneOptimizer.Application.Services
{
    /// <summary>
    /// Resolves canonical output paths for all project-owned files
    /// according to the official project folder contract:
    ///
    ///   &lt;ProjectRoot&gt;/
    ///     Exports/    — Final user deliverables (PDF, Excel, CSV)
    ///     Logs/       — Runtime / profiling logs
    ///     Models/     — Imported source BIM files (IFC, DXF)
    ///     Reports/    — Internal diagnostics and analysis outputs
    ///     project.db  — Project SQLite database
    ///
    /// All methods throw <see cref="InvalidOperationException"/> when no project
    /// is active. Callers must ensure a project is loaded before calling any method.
    /// No silent fallbacks to environment folders are permitted.
    /// </summary>
    public interface IProjectOutputPathService
    {
        // ── Root ──────────────────────────────────────────────────────────
        string GetProjectRoot();

        // ── Exports (final user deliverables) ────────────────────────────
        string GetExportsDirectory();
        string GetUserPdfExportsDirectory();
        string GetUserExcelExportsDirectory();
        string GetUserCsvExportsDirectory();

        // ── Reports (internal diagnostics / analysis) ─────────────────────
        string GetReportsDirectory();
        string GetGaReportsDirectory();
        string GetGaRunReportsDirectory(string runId);
        string GetGaHistoryDirectory();
        string GetIfcAuditReportsDirectory();
        string GetDataSourceAuditDirectory();

        // ── Logs (runtime / profiling) ────────────────────────────────────
        string GetLogsDirectory();
        string GetGaLogsDirectory();

        // ── Models (imported source files) ────────────────────────────────
        string GetModelsDirectory();
        string GetIfcModelsDirectory();
        string GetDxfModelsDirectory();
    }
}

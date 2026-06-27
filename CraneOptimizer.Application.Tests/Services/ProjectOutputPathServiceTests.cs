using CraneOptimizer.Application.Services;
using System;
using System.IO;
using Xunit;

namespace CraneOptimizer.Application.Tests.Services
{
    /// <summary>
    /// Unit tests for ProjectOutputPathService.
    /// Verifies canonical path resolution, directory creation, naming conventions,
    /// and no-active-project enforcement.
    /// </summary>
    public class ProjectOutputPathServiceTests : IDisposable
    {
        private readonly string _tempRoot;

        public ProjectOutputPathServiceTests()
        {
            // Use a unique temp directory per test run
            _tempRoot = Path.Combine(Path.GetTempPath(), "CraneOptimizerTests", Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(_tempRoot);
        }

        public void Dispose()
        {
            if (Directory.Exists(_tempRoot))
                Directory.Delete(_tempRoot, recursive: true);
        }

        // ── Helpers ───────────────────────────────────────────────────────

        private static ProjectOutputPathService CreateService(string? projectPath)
        {
            var provider = new FakeProjectPathProvider(projectPath);
            return new ProjectOutputPathService(provider);
        }

        // ── No-active-project enforcement ─────────────────────────────────

        [Fact]
        public void GetProjectRoot_ThrowsWhenNoActiveProject()
        {
            var svc = CreateService(null);
            Assert.Throws<InvalidOperationException>(() => svc.GetProjectRoot());
        }

        [Fact]
        public void GetExportsDirectory_ThrowsWhenNoActiveProject()
        {
            var svc = CreateService(null);
            Assert.Throws<InvalidOperationException>(() => svc.GetExportsDirectory());
        }

        [Fact]
        public void GetReportsDirectory_ThrowsWhenNoActiveProject()
        {
            var svc = CreateService(string.Empty);
            Assert.Throws<InvalidOperationException>(() => svc.GetReportsDirectory());
        }

        [Fact]
        public void GetGaLogsDirectory_ThrowsWhenNoActiveProject()
        {
            var svc = CreateService(null);
            Assert.Throws<InvalidOperationException>(() => svc.GetGaLogsDirectory());
        }

        [Fact]
        public void GetGaHistoryDirectory_ThrowsWhenNoActiveProject()
        {
            var svc = CreateService(null);
            Assert.Throws<InvalidOperationException>(() => svc.GetGaHistoryDirectory());
        }

        // ── Correct path resolution under project root ────────────────────

        [Fact]
        public void GetProjectRoot_ReturnsProjectPath()
        {
            var svc = CreateService(_tempRoot);
            Assert.Equal(_tempRoot, svc.GetProjectRoot());
        }

        [Fact]
        public void GetExportsDirectory_IsUnderProjectRoot()
        {
            var svc = CreateService(_tempRoot);
            var result = svc.GetExportsDirectory();
            Assert.StartsWith(_tempRoot, result, StringComparison.OrdinalIgnoreCase);
            Assert.EndsWith("Exports", result, StringComparison.OrdinalIgnoreCase);
        }

        [Fact]
        public void GetUserPdfExportsDirectory_IsUnderExports()
        {
            var svc = CreateService(_tempRoot);
            var result = svc.GetUserPdfExportsDirectory();
            Assert.Contains(Path.Combine("Exports", "PDF"), result, StringComparison.OrdinalIgnoreCase);
        }

        [Fact]
        public void GetUserExcelExportsDirectory_IsUnderExports()
        {
            var svc = CreateService(_tempRoot);
            var result = svc.GetUserExcelExportsDirectory();
            Assert.Contains(Path.Combine("Exports", "Excel"), result, StringComparison.OrdinalIgnoreCase);
        }

        [Fact]
        public void GetUserCsvExportsDirectory_IsUnderExports()
        {
            var svc = CreateService(_tempRoot);
            var result = svc.GetUserCsvExportsDirectory();
            Assert.Contains(Path.Combine("Exports", "CSV"), result, StringComparison.OrdinalIgnoreCase);
        }

        [Fact]
        public void GetReportsDirectory_IsUnderProjectRoot()
        {
            var svc = CreateService(_tempRoot);
            var result = svc.GetReportsDirectory();
            Assert.StartsWith(_tempRoot, result, StringComparison.OrdinalIgnoreCase);
            Assert.EndsWith("Reports", result, StringComparison.OrdinalIgnoreCase);
        }

        [Fact]
        public void GetGaReportsDirectory_IsUnderReports()
        {
            var svc = CreateService(_tempRoot);
            var result = svc.GetGaReportsDirectory();
            Assert.Contains(Path.Combine("Reports", "GA"), result, StringComparison.OrdinalIgnoreCase);
        }

        [Fact]
        public void GetGaRunReportsDirectory_ContainsRunId()
        {
            var svc = CreateService(_tempRoot);
            const string runId = "run_20240101_120000";
            var result = svc.GetGaRunReportsDirectory(runId);
            Assert.Contains(runId, result, StringComparison.OrdinalIgnoreCase);
            Assert.Contains(Path.Combine("Reports", "GA"), result, StringComparison.OrdinalIgnoreCase);
        }

        [Fact]
        public void GetGaRunReportsDirectory_ThrowsOnEmptyRunId()
        {
            var svc = CreateService(_tempRoot);
            Assert.Throws<ArgumentException>(() => svc.GetGaRunReportsDirectory(string.Empty));
        }

        [Fact]
        public void GetGaHistoryDirectory_IsUnderGaReports()
        {
            var svc = CreateService(_tempRoot);
            var result = svc.GetGaHistoryDirectory();
            Assert.Contains(Path.Combine("Reports", "GA", "History"), result, StringComparison.OrdinalIgnoreCase);
        }

        [Fact]
        public void GetIfcAuditReportsDirectory_IsUnderReports()
        {
            var svc = CreateService(_tempRoot);
            var result = svc.GetIfcAuditReportsDirectory();
            Assert.Contains(Path.Combine("Reports", "IFC"), result, StringComparison.OrdinalIgnoreCase);
        }

        [Fact]
        public void GetLogsDirectory_IsUnderProjectRoot()
        {
            var svc = CreateService(_tempRoot);
            var result = svc.GetLogsDirectory();
            Assert.StartsWith(_tempRoot, result, StringComparison.OrdinalIgnoreCase);
            Assert.EndsWith("Logs", result, StringComparison.OrdinalIgnoreCase);
        }

        [Fact]
        public void GetGaLogsDirectory_IsUnderLogs()
        {
            var svc = CreateService(_tempRoot);
            var result = svc.GetGaLogsDirectory();
            Assert.Contains(Path.Combine("Logs", "GA"), result, StringComparison.OrdinalIgnoreCase);
        }

        [Fact]
        public void GetModelsDirectory_IsUnderProjectRoot()
        {
            var svc = CreateService(_tempRoot);
            var result = svc.GetModelsDirectory();
            Assert.StartsWith(_tempRoot, result, StringComparison.OrdinalIgnoreCase);
            Assert.EndsWith("Models", result, StringComparison.OrdinalIgnoreCase);
        }

        [Fact]
        public void GetIfcModelsDirectory_IsUnderModels()
        {
            var svc = CreateService(_tempRoot);
            var result = svc.GetIfcModelsDirectory();
            Assert.Contains(Path.Combine("Models", "IFC"), result, StringComparison.OrdinalIgnoreCase);
        }

        [Fact]
        public void GetDxfModelsDirectory_IsUnderModels()
        {
            var svc = CreateService(_tempRoot);
            var result = svc.GetDxfModelsDirectory();
            Assert.Contains(Path.Combine("Models", "DXF"), result, StringComparison.OrdinalIgnoreCase);
        }

        // ── Directory creation ─────────────────────────────────────────────

        [Fact]
        public void GetExportsDirectory_CreatesDirectory()
        {
            var svc = CreateService(_tempRoot);
            var path = svc.GetExportsDirectory();
            Assert.True(Directory.Exists(path), $"Expected directory to exist: {path}");
        }

        [Fact]
        public void GetGaHistoryDirectory_CreatesDirectory()
        {
            var svc = CreateService(_tempRoot);
            var path = svc.GetGaHistoryDirectory();
            Assert.True(Directory.Exists(path), $"Expected directory to exist: {path}");
        }

        // ── No fallback to environment folders ────────────────────────────

        [Fact]
        public void NoPathContainsMydocuments_WhenProjectActive()
        {
            var svc = CreateService(_tempRoot);
            var myDocs = Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments);
            // None of the resolved paths should point to MyDocuments
            Assert.DoesNotContain(myDocs, svc.GetExportsDirectory(), StringComparison.OrdinalIgnoreCase);
            Assert.DoesNotContain(myDocs, svc.GetReportsDirectory(), StringComparison.OrdinalIgnoreCase);
            Assert.DoesNotContain(myDocs, svc.GetLogsDirectory(), StringComparison.OrdinalIgnoreCase);
        }

        [Fact]
        public void NoPathContainsLocalAppData_WhenProjectActive()
        {
            // The old hardcoded fallback was: LocalApplicationData/CraneOptimizer/...
            // Verify that the resolved paths are rooted in the project folder, not in
            // the legacy fallback path (which contained the literal subdirectory 'CraneOptimizer'
            // directly under the AppData folder).
            var svc = CreateService(_tempRoot);
            var gaReports = svc.GetGaReportsDirectory();
            var gaLogs = svc.GetGaLogsDirectory();

            // Paths must start with the project root, not with AppData
            Assert.StartsWith(_tempRoot, gaReports, StringComparison.OrdinalIgnoreCase);
            Assert.StartsWith(_tempRoot, gaLogs, StringComparison.OrdinalIgnoreCase);
        }

        // ── Fake provider ──────────────────────────────────────────────────

        private sealed class FakeProjectPathProvider : IActiveProjectPathProvider
        {
            private readonly string? _path;
            public FakeProjectPathProvider(string? path) => _path = path;
            public string? GetActiveProjectPath() => _path;
        }
    }
}

using CraneOptimizer.Domain.Diagnostics;
using System;
using System.Threading;
using System.Threading.Tasks;

namespace CraneOptimizer.Domain.Interfaces
{
    public interface IIfcDiagnosticAuditService
    {
        /// <summary>
        /// Runs a non-destructive diagnostic audit on an IFC file.
        /// </summary>
        Task<IfcAuditResult> RunAuditAsync(string ifcPath, IProgress<double>? progress = null, CancellationToken cancellationToken = default);

        /// <summary>
        /// Runs a non-destructive diagnostic audit on an IFC file with import-time material settings.
        /// </summary>
        Task<IfcAuditResult> RunAuditAsync(string ifcPath, IfcReadingOptions? options, IProgress<double>? progress = null, CancellationToken cancellationToken = default);

        /// <summary>
        /// Generates Markdown, JSON, and CSV reports inside the project reports directory.
        /// </summary>
        /// <returns>The directory path where reports were written.</returns>
        Task<string> GenerateReportsAsync(IfcAuditResult auditResult, string? projectFolderPath = null, CancellationToken cancellationToken = default);
    }
}

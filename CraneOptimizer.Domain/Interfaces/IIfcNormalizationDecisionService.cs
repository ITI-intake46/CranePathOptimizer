using CraneOptimizer.Domain.Diagnostics;
using System.Threading;
using System.Threading.Tasks;

namespace CraneOptimizer.Domain.Interfaces
{
    public interface IIfcNormalizationDecisionService
    {
        /// <summary>
        /// Analyzes the raw audit results and generates a normalized decision preview.
        /// </summary>
        Task<IfcNormalizationPreviewSummary> GenerateNormalizationPreviewAsync(IfcAuditResult auditResult, CancellationToken cancellationToken = default);

        /// <summary>
        /// Writes Markdown summary, JSON details, and CSV tables for the preview report.
        /// </summary>
        /// <returns>The directory path where reports were written.</returns>
        Task<string> GeneratePreviewReportsAsync(IfcNormalizationPreviewSummary preview, string? projectFolderPath = null, CancellationToken cancellationToken = default);
    }
}

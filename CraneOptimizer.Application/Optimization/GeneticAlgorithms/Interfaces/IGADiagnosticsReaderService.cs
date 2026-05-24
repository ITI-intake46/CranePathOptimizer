using System.Collections.Generic;
using CraneOptimizer.Application.Optimization.GeneticAlgorithms.DTOs;

namespace CraneOptimizer.Application.Optimization.GeneticAlgorithms.Interfaces
{
    public interface IGADiagnosticsReaderService
    {
        IReadOnlyList<GARunHistoryDto> ReadRunHistory(string? projectFolderPath);
        IReadOnlyList<GAElementDiagnosticDto> ReadDetailedDiagnostics(string detailedCsvPath);
        IReadOnlyList<GAFailureBreakdownDto> ReadFailureBreakdown(string breakdownCsvPath);
    }
}

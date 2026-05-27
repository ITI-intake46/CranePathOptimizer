using CraneOptimizer.Domain.Diagnostics;
using System.Collections.Generic;

namespace CraneOptimizer.UI.Core.Interfaces
{
    public interface IRenderDiagnosticsStore
    {
        void AddStats(RenderPerformanceStats stats);
        RenderPerformanceStats? GetLastRenderStats();
        IReadOnlyList<RenderPerformanceStats> GetRenderStatsHistory();
        void Clear();
    }
}

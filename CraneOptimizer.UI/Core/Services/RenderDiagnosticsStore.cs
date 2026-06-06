using CraneOptimizer.Domain.Diagnostics;
using CraneOptimizer.UI.Core.Interfaces;
using System;
using System.Collections.Generic;

namespace CraneOptimizer.UI.Core.Services
{
    public sealed class RenderDiagnosticsStore : IRenderDiagnosticsStore
    {
        private readonly object _lock = new();
        private RenderPerformanceStats? _lastStats;
        private readonly List<RenderPerformanceStats> _history = new();
        private const int MaxHistorySize = 100;

        public void AddStats(RenderPerformanceStats stats)
        {
            if (stats == null) return;
            lock (_lock)
            {
                _lastStats = stats;
                _history.Add(stats);
                if (_history.Count > MaxHistorySize)
                {
                    _history.RemoveAt(0);
                }
            }
        }

        public RenderPerformanceStats? GetLastRenderStats()
        {
            lock (_lock)
            {
                return _lastStats;
            }
        }

        public IReadOnlyList<RenderPerformanceStats> GetRenderStatsHistory()
        {
            lock (_lock)
            {
                return _history.AsReadOnly();
            }
        }

        public void Clear()
        {
            lock (_lock)
            {
                _lastStats = null;
                _history.Clear();
            }
        }
    }
}

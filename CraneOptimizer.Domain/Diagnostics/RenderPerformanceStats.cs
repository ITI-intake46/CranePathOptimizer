using System;

namespace CraneOptimizer.Domain.Diagnostics
{
    public sealed class RenderPerformanceStats
    {
        public Guid? SessionId { get; set; }
        public string? FileName { get; set; }
        public string? RenderQuality { get; set; }
        public int TotalElements { get; set; }
        public int VisibleElements { get; set; }
        public int IncludeInRenderSkippedCount { get; set; }
        public int ShapeInstanceCount { get; set; }
        public int MeshesCreatedCount { get; set; }
        public int MeshCacheHitCount { get; set; }
        public int MeshCacheMissCount { get; set; }
        public int GeometryModel3DCount { get; set; }
        public int ModelVisual3DCount { get; set; }
        public int MaterialCreatedCount { get; set; }
        public int FallbackBoxCount { get; set; }
        public int RealMeshElementCount { get; set; }
        public double RenderDurationMs { get; set; }
        public double ReRenderDurationMs { get; set; }
        public double MemoryBeforeRenderMB { get; set; }
        public double MemoryAfterRenderMB { get; set; }
        public DateTime TimestampUtc { get; set; }
    }
}

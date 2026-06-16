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
        public double MeshCacheHitRatio => (MeshCacheHitCount + MeshCacheMissCount) == 0 ? 0.0 : (double)MeshCacheHitCount / (MeshCacheHitCount + MeshCacheMissCount);
        public int HighQualityMeshCacheHitCount { get; set; }
        public int HighQualityMeshCacheMissCount { get; set; }
        public double HighQualityMeshCacheHitRatio => (HighQualityMeshCacheHitCount + HighQualityMeshCacheMissCount) == 0 ? 0.0 : (double)HighQualityMeshCacheHitCount / (HighQualityMeshCacheHitCount + HighQualityMeshCacheMissCount);
        public int HighQualityMeshesCreatedCount { get; set; }
        public int HighQualityCacheCount { get; set; }
        public int GeometryModel3DCount { get; set; }
        public int ModelVisual3DCount { get; set; }
        public int MaterialCreatedCount { get; set; }
        public int FallbackBoxCount { get; set; }
        public int RealMeshElementCount { get; set; }
        public int BatchedGroupCount { get; set; }
        public int BatchedElementCount { get; set; }
        public int HitTestBoxCount { get; set; }
        public int RenderGeometryModel3DCount { get; set; }
        public int HitTestGeometryModel3DCount { get; set; }
        public int TotalGeometryModel3DCount { get; set; }
        public double RenderDurationMs { get; set; }
        public double ReRenderDurationMs { get; set; }
        public double MemoryBeforeRenderMB { get; set; }
        public double MemoryAfterRenderMB { get; set; }
        public int SpatialHitTestCandidateCount { get; set; }
        public double SpatialHitTestDurationMs { get; set; }
        public string? PickedElementId { get; set; }
        public DateTime TimestampUtc { get; set; }
    }
}

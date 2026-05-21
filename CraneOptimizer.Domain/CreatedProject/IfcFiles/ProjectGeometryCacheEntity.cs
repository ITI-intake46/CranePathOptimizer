namespace CraneOptimizer.Domain.CreatedProject.IfcFiles
{
    /// <summary>
    /// Optional geometry cache index.
    /// This table does not store the mesh directly.
    /// It stores where cached geometry exists if we decide to add geometry caching later.
    /// </summary>
    public sealed class ProjectGeometryCacheEntity
    {
        public int Id { get; set; }

        public int ProjectInfoId { get; set; }

        public int IfcFileId { get; set; }

        public string GlobalId { get; set; } = string.Empty;

        /// <summary>
        /// Level of detail of the cached geometry.
        /// 0 = BoundingBox
        /// 1 = CoarseMesh
        /// 2 = FullMesh
        /// </summary>
        public int LOD { get; set; } = 0;

        /// <summary>
        /// Internal, Xbim, Binary, Json, etc.
        /// </summary>
        public string CacheFormat { get; set; } = "Internal";

        public string? CacheFilePath { get; set; }

        public long? MeshOffset { get; set; }

        public long? MeshLength { get; set; }

        public int? VertexCount { get; set; }

        public int? TriangleCount { get; set; }

        public DateTime CreatedAtUtc { get; set; } = DateTime.UtcNow;

        public ProjectInfoEntity? ProjectInfo { get; set; }

        public ProjectIfcFileEntity? IfcFile { get; set; }
    }
}

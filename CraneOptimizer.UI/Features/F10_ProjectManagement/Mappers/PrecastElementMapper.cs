using CraneOptimizer.Domain.Common;
using CraneOptimizer.Domain.Diagnostics;
using CraneOptimizer.Domain.CreatedProject.IfcFiles;
using CraneOptimizer.Domain.Schemas;
using System;
using System.Collections.Generic;
using System.Text.Json;

namespace CraneOptimizer.UI.Features.F10_ProjectManagement.Mappers
{
    public static class PrecastElementMapper
    {
        private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

        public static ProjectIfcElementEntity ToEntity(PrecastElement domain, int projectInfoId, int ifcFileId)
        {
            if (domain == null) throw new ArgumentNullException(nameof(domain));

            double cogX = domain.COG_M?.X ?? domain.ElementCOG?.X ?? 0;
            double cogY = domain.COG_M?.Y ?? domain.ElementCOG?.Y ?? 0;
            double cogZ = domain.COG_M?.Z ?? domain.ElementCOG?.Z ?? 0;

            double bboxX = domain.BoundingBox_M?.X ?? domain.BBoxLength ?? 0;
            double bboxY = domain.BoundingBox_M?.Y ?? domain.BBoxWidth ?? 0;
            double bboxZ = domain.BoundingBox_M?.Z ?? domain.BBoxHeight ?? 0;

            return new ProjectIfcElementEntity
            {
                ProjectInfoId = projectInfoId,
                IfcFileId = ifcFileId,
                GlobalId = domain.ElementID,
                Name = domain.ElementRevitId,
                DisplayName = domain.ElementName ?? domain.ElementMark,
                Mark = domain.ElementMark,
                ElementType = domain.ElementType,
                Material = domain.ElementMaterial,
                LengthM = domain.ElementLength,
                WidthM = domain.ElementWidth,
                HeightM = domain.ElementHeight,
                VolumeM3 = domain.VolumeM3 ?? domain.ElementVolume,
                WeightTons = domain.WeightTons ?? domain.ElementWT,
                COG_X = cogX,
                COG_Y = cogY,
                COG_Z = cogZ,
                BBoxMinX = cogX - (bboxX / 2),
                BBoxMinY = cogY - (bboxY / 2),
                BBoxMinZ = cogZ - (bboxZ / 2),
                BBoxMaxX = cogX + (bboxX / 2),
                BBoxMaxY = cogY + (bboxY / 2),
                BBoxMaxZ = cogZ + (bboxZ / 2),
                IsLiftable = domain.Role == IfcElementRole.LiftableElement,

                // Phase 2A Normalization fields
                RawIfcType = domain.RawIfcType,
                ResolvedCategory = domain.ResolvedCategory,
                Role = domain.Role.ToString(),
                ParentGlobalId = domain.ParentGlobalId,
                ChildGlobalIdsJson = SerializeList(domain.ChildGlobalIds),
                VisibleGeometryOwnerGlobalIdsJson = SerializeList(domain.VisibleGeometryOwnerGlobalIds),
                HiddenInternalGeometryOwnerGlobalIdsJson = SerializeList(domain.HiddenInternalGeometryOwnerGlobalIds),
                IncludeInRender = domain.IncludeInRender,
                IncludeInElementTree = domain.IncludeInElementTree,
                IncludeInGa = domain.IncludeInGa,
                WeightSource = domain.WeightSource,
                WeightTrustLevel = domain.WeightTrustLevel.ToString(),
                ExtractionWarningsJson = SerializeList(domain.ExtractionWarnings),
                NormalizationDecisionReason = domain.NormalizationDecisionReason,

                CachedAtUtc = DateTime.UtcNow
            };
        }

        public static PrecastElement ToDomain(ProjectIfcElementEntity entity)
        {
            if (entity == null) throw new ArgumentNullException(nameof(entity));

            double minX = entity.BBoxMinX ?? 0;
            double minY = entity.BBoxMinY ?? 0;
            double minZ = entity.BBoxMinZ ?? 0;
            double maxX = entity.BBoxMaxX ?? 0;
            double maxY = entity.BBoxMaxY ?? 0;
            double maxZ = entity.BBoxMaxZ ?? 0;

            double bboxLength = maxX - minX;
            double bboxWidth = maxY - minY;
            double bboxHeight = maxZ - minZ;

            var cog = new DomainPoint3D(entity.COG_X ?? 0, entity.COG_Y ?? 0, entity.COG_Z ?? 0);
            var bbox = new DomainPoint3D(bboxLength, bboxWidth, bboxHeight);

            _ = Enum.TryParse<IfcElementRole>(entity.Role, out var role);
            _ = Enum.TryParse<IfcWeightTrustLevel>(entity.WeightTrustLevel, out var trustLevel);

            return new PrecastElement
            {
                ElementID = entity.GlobalId,
                ElementRevitId = entity.Name,
                ElementMark = entity.Mark,
                ElementName = entity.DisplayName,
                ElementType = entity.ElementType ?? string.Empty,
                ElementLength = entity.LengthM,
                ElementWidth = entity.WidthM,
                ElementHeight = entity.HeightM,
                ElementMaterial = entity.Material ?? string.Empty,
                ElementWT = entity.WeightTons,
                ElementVolume = entity.VolumeM3,
                ElementCOG = cog,
                BBoxLength = bboxLength,
                BBoxWidth = bboxWidth,
                BBoxHeight = bboxHeight,

                // Phase 2A Normalization fields
                RawIfcType = entity.RawIfcType ?? string.Empty,
                ResolvedCategory = entity.ResolvedCategory ?? string.Empty,
                Role = role,
                ParentGlobalId = entity.ParentGlobalId,
                ChildGlobalIds = DeserializeList(entity.ChildGlobalIdsJson),
                VisibleGeometryOwnerGlobalIds = DeserializeList(entity.VisibleGeometryOwnerGlobalIdsJson),
                HiddenInternalGeometryOwnerGlobalIds = DeserializeList(entity.HiddenInternalGeometryOwnerGlobalIdsJson),
                IncludeInRender = entity.IncludeInRender,
                IncludeInElementTree = entity.IncludeInElementTree,
                IncludeInGa = entity.IncludeInGa,
                WeightTons = entity.WeightTons,
                WeightSource = entity.WeightSource ?? "Missing",
                WeightTrustLevel = trustLevel,
                VolumeM3 = entity.VolumeM3,
                COG_M = cog,
                BoundingBox_M = bbox,
                ExtractionWarnings = DeserializeList(entity.ExtractionWarningsJson),
                NormalizationDecisionReason = entity.NormalizationDecisionReason ?? string.Empty
            };
        }

        private static string? SerializeList(List<string>? list)
        {
            if (list == null || list.Count == 0) return null;
            return JsonSerializer.Serialize(list, JsonOptions);
        }

        private static List<string> DeserializeList(string? json)
        {
            if (string.IsNullOrWhiteSpace(json)) return new List<string>();
            try
            {
                return JsonSerializer.Deserialize<List<string>>(json, JsonOptions) ?? new List<string>();
            }
            catch
            {
                return new List<string>();
            }
        }
    }
}

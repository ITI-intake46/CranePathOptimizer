using CraneOptimizer.Domain.Diagnostics;
using CraneOptimizer.Domain.Interfaces;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Threading;
using System.Threading.Tasks;
using Xbim.Ifc4.Interfaces;
using Xbim.ModelGeometry.Scene;

namespace CraneOptimizer.Infrastructure.Repositories
{
    public sealed class IfcNormalizationDecisionService : IIfcNormalizationDecisionService
    {
        private readonly IIfcExporterProfileResolver _profileResolver;

        public IfcNormalizationDecisionService(IIfcExporterProfileResolver profileResolver)
        {
            _profileResolver = profileResolver;
        }

        public Task<IfcNormalizationPreviewSummary> GenerateNormalizationPreviewAsync(IfcAuditResult auditResult, CancellationToken cancellationToken = default)
        {
            return Task.Run(() => GenerateNormalizationPreviewCore(auditResult, cancellationToken), cancellationToken);
        }

        public Task<string> GeneratePreviewReportsAsync(IfcNormalizationPreviewSummary preview, string? projectFolderPath = null, CancellationToken cancellationToken = default)
        {
            return Task.Run(() => GeneratePreviewReportsCore(preview, projectFolderPath, cancellationToken), cancellationToken);
        }

        private IfcNormalizationPreviewSummary GenerateNormalizationPreviewCore(IfcAuditResult auditResult, CancellationToken cancellationToken)
        {
            var summary = new IfcNormalizationPreviewSummary
            {
                FileName = auditResult.Metadata.FileName,
                ExporterApplication = auditResult.Metadata.ExporterApplication,
                TotalRawProductCount = auditResult.Classifications.Count
            };

            // 1. Resolve Profile
            summary.DetectedProfile = _profileResolver.ResolveProfile(auditResult.Metadata.ExporterApplication);
            IIfcExporterProfile profile = summary.DetectedProfile switch
            {
                IfcExporterProfile.Tekla => new TeklaIfcExporterProfile(),
                IfcExporterProfile.Revit => new RevitIfcExporterProfile(),
                _ => new GenericIfcExporterProfile()
            };

            var previews = new Dictionary<string, IfcNormalizedElementPreview>(StringComparer.Ordinal);

            // First pass: Initialize previews for all elements
            foreach (var elem in auditResult.Classifications)
            {
                var weightDetail = auditResult.Weights.FirstOrDefault(w => w.GlobalId == elem.GlobalId);
                var volMatDetail = auditResult.VolumesMaterials.FirstOrDefault(v => v.GlobalId == elem.GlobalId);
                var geomDetail = auditResult.Geometries.FirstOrDefault(g => g.LogicalGlobalId == elem.GlobalId);

                var preview = new IfcNormalizedElementPreview
                {
                    GlobalId = elem.GlobalId,
                    Name = elem.Name,
                    RawIfcType = elem.RawIfcType,
                    CandidateCategory = elem.CandidateResolvedCategory,
                    WeightSource = weightDetail?.WeightSource ?? "Missing",
                    WeightRawValue = weightDetail?.WeightRawValue,
                    WeightStandardValueTons = weightDetail?.WeightStandardValueTons,
                    VolumeStandardValueM3 = volMatDetail?.VolumeStandardValueM3,
                    WeightTrustLevel = MapWeightTrustLevel(weightDetail)
                };

                if (geomDetail != null && geomDetail.HasGeometryDirectly)
                {
                    preview.VisibleGeometryOwnerGlobalIds.Add(elem.GlobalId);
                }

                if (weightDetail?.Warning != null && !string.IsNullOrWhiteSpace(weightDetail.Warning))
                {
                    preview.Warnings.Add(weightDetail.Warning);
                }

                // Add detailed Weight Diagnostics
                if (weightDetail != null)
                {
                    var unitStr = string.IsNullOrWhiteSpace(weightDetail.WeightUnitDetected) ? "Unspecified" : weightDetail.WeightUnitDetected;
                    if (weightDetail.WeightStatus == "ExplicitPositive" || weightDetail.WeightStatus == "SuspiciousExtreme")
                    {
                        preview.Warnings.Add($"Weight: raw={weightDetail.WeightRawValue}, unit={unitStr}, normalized={weightDetail.WeightStandardValueTons:F4} tons, source={weightDetail.QuantitySetNameOrPropertySetName}.{weightDetail.WeightPropertyName}, trust={preview.WeightTrustLevel}");
                    }
                    else if (weightDetail.WeightStatus == "EstimatedByFallback")
                    {
                        preview.Warnings.Add($"Weight: raw={weightDetail.WeightRawValue}, normalized={weightDetail.WeightStandardValueTons:F4} tons, source=Fallback, trust={preview.WeightTrustLevel}, fallbackReason=No explicit weight found; estimated from volume and concrete density");
                    }
                    else if (weightDetail.WeightStatus == "ExplicitZeroFromIFC")
                    {
                        preview.Warnings.Add($"Weight: raw=0, normalized=0 tons, source={weightDetail.QuantitySetNameOrPropertySetName}.{weightDetail.WeightPropertyName}, trust={preview.WeightTrustLevel}, fallbackReason={weightDetail.Warning}");
                    }
                    else
                    {
                        preview.Warnings.Add($"Weight: raw=null, normalized=null, source=Missing, trust={preview.WeightTrustLevel}, fallbackReason={weightDetail.Warning}");
                    }
                }

                // Add detailed Volume Diagnostics
                if (volMatDetail != null)
                {
                    if (volMatDetail.VolumeRawValue.HasValue)
                    {
                        preview.Warnings.Add($"Volume: raw={volMatDetail.VolumeRawValue.Value}, unit={auditResult.Metadata.VolumeUnit}, normalized={volMatDetail.VolumeStandardValueM3:F4} m³, source={volMatDetail.VolumeSource}");
                    }
                    else
                    {
                        preview.Warnings.Add($"Volume: raw=null, normalized=null, source=Missing");
                    }
                }

                previews[elem.GlobalId] = preview;
            }

            // Map Parent-Child lists and Geometry relationships from assembly audit
            foreach (var assembly in auditResult.Assemblies.Details)
            {
                if (previews.TryGetValue(assembly.AssemblyGlobalId, out var parentPreview))
                {
                    parentPreview.IncludeInWeightRollupPreview = false; // Assemblies do not roll up directly; child elements roll up
                    parentPreview.HasChildren = true;
                }

                foreach (var child in assembly.ChildDetails)
                {
                    if (previews.TryGetValue(child.ChildGlobalId, out var childPreview))
                    {
                        childPreview.ParentGlobalId = assembly.AssemblyGlobalId;
                        childPreview.ParentGlobalIdSource = child.RelationshipSource;
                    }
                    if (parentPreview != null && !parentPreview.ChildGlobalIds.Contains(child.ChildGlobalId))
                    {
                        parentPreview.ChildGlobalIds.Add(child.ChildGlobalId);
                    }
                }
            }

            // Map assembly classification summary fields from relations to previews
            foreach (var rel in auditResult.AssemblyRelations)
            {
                if (previews.TryGetValue(rel.ParentGlobalId, out var parentPreview))
                {
                    parentPreview.MainPartGlobalId = rel.MainPartGlobalId;
                    parentPreview.MainPartName = rel.MainPartName;
                    parentPreview.MainPartCategory = rel.MainPartCategory;
                    parentPreview.MainPartDetectionMethod = rel.MainPartDetectionMethod.ToString();
                    parentPreview.ChildCount = rel.ChildCount;
                    parentPreview.VisibleSecondaryPartCount = rel.VisibleSecondaryPartCount;
                    parentPreview.HiddenWeightContributorCount = rel.HiddenWeightContributorCount;
                    parentPreview.AccessoryWeightContributorCount = rel.AccessoryWeightContributorCount;
                    parentPreview.IgnoredReferenceCount = rel.IgnoredReferenceCount;
                    parentPreview.HasSecondaryParts = rel.HasSecondaryParts;
                    parentPreview.HasHiddenContributors = rel.HasHiddenContributors;

                    if (rel.Warnings != null)
                    {
                        foreach (var warning in rel.Warnings)
                        {
                            if (!parentPreview.Warnings.Contains(warning))
                            {
                                parentPreview.Warnings.Add(warning);
                            }
                        }
                    }
                }
            }

            // Apply specific rules
            foreach (var preview in previews.Values)
            {
                cancellationToken.ThrowIfCancellationRequested();

                var rawType = preview.RawIfcType;
                var category = preview.CandidateCategory;
                var name = preview.Name;

                // Rule 0: Treat an IFC grid and its owned axes as reference geometry.
                // Grid axes have no independent GlobalId and remain owned by the grid product.
                if (rawType.Equals("IfcGrid", StringComparison.OrdinalIgnoreCase))
                {
                    bool hideGrids = auditResult.Options?.HideGrids ?? false;

                    preview.Role = IfcElementRole.InternalAuxiliary;
                    preview.IncludeInRenderPreview = !hideGrids;
                    preview.IncludeInElementTreePreview = !hideGrids;
                    preview.IncludeInGaPreview = false;
                    preview.IncludeInWeightRollupPreview = false;

                    if (hideGrids)
                    {
                        preview.DecisionReason = "Classified as reference geometry grid. Hidden by user option (HideGrids). Excluded from UI and lift items.";
                    }
                    else
                    {
                        preview.DecisionReason = "Classified as reference geometry grid. Included in rendering/UI but strictly excluded from lift items.";
                    }

                    if (preview.HasGeometryOwner())
                    {
                        preview.VisibleGeometryOwnerGlobalIds = hideGrids ? new List<string>() : new List<string> { preview.GlobalId };
                    }
                    continue;
                }

                // Rule 1: Exclude reinforcement/internal auxiliaries
                if (rawType.ContainsAny("ReinforcingBar", "ReinforcingMesh", "ReinforcingElement", "Tendon", "TendonAnchor") ||
                    name.ContainsAny("rebar", "reinforcing", "tie-wire", "stirrup"))
                {
                    preview.Role = IfcElementRole.InternalAuxiliary;
                    preview.IncludeInRenderPreview = false;
                    preview.IncludeInElementTreePreview = false;
                    preview.IncludeInGaPreview = false;
                    preview.IncludeInWeightRollupPreview = false;
                    preview.DecisionReason = "Classified as reinforcing/auxiliary steel element. Excluded from active lift items.";

                    if (preview.HasGeometryOwner())
                    {
                        preview.HiddenInternalGeometryOwnerGlobalIds.Add(preview.GlobalId);
                        preview.VisibleGeometryOwnerGlobalIds.Clear();
                    }

                    // Reinforcement rebar rollup candidates
                    if (preview.ParentGlobalId != null && previews.TryGetValue(preview.ParentGlobalId, out var host))
                    {
                        if (preview.WeightStandardValueTons.HasValue && preview.WeightStandardValueTons.Value > 0)
                        {
                            if (host.WeightTrustLevel == IfcWeightTrustLevel.Missing ||
                                host.WeightTrustLevel == IfcWeightTrustLevel.EstimatedFallback_NotAllowedForGA)
                            {
                                host.ReinforcementWeightContributionTons = (host.ReinforcementWeightContributionTons ?? 0) + preview.WeightStandardValueTons.Value;
                                if (host.WeightTrustLevel != IfcWeightTrustLevel.RequiresUserApproval)
                                {
                                    host.WeightTrustLevel = IfcWeightTrustLevel.RequiresUserApproval;
                                    host.Warnings.Add("Total precast assembly weight is missing or fallback, but positive reinforcement steel weight is available. Roll-up requires user approval.");
                                }
                            }
                            else
                            {
                                preview.Warnings.Add("Host already has trusted explicit total weight. Auxiliary reinforcement weight was ignored to avoid double-counting.");
                            }
                        }
                        else
                        {
                            host.HasMissingReinforcementWeight = true;
                        }
                    }
                    continue;
                }

                // Rule 2: Assembly Rules
                if (rawType.Equals("IfcElementAssembly", StringComparison.OrdinalIgnoreCase))
                {
                    var assemblyDetail = auditResult.Assemblies.Details.FirstOrDefault(a => a.AssemblyGlobalId == preview.GlobalId);
                    if (assemblyDetail == null)
                    {
                        preview.Role = IfcElementRole.GroupingOnly;
                        preview.AssemblyDecision = IfcAssemblyDecision.UnknownNeedsReview;
                        preview.IncludeInGaPreview = false;
                        preview.DecisionReason = "Empty IfcElementAssembly container. Grouping node only.";
                        continue;
                    }

                    // Filter out rebar/internal auxiliary children to count physical structural members
                    var nonInternalChildrenIds = preview.ChildGlobalIds
                        .Where(cid => previews.TryGetValue(cid, out var cp) && cp.Role != IfcElementRole.InternalAuxiliary)
                        .ToList();

                    // Case A: Exactly one physical structural child - Single Child Wrapper
                    if (nonInternalChildrenIds.Count == 1)
                    {
                        var childId = nonInternalChildrenIds[0];
                        preview.Role = IfcElementRole.GroupingOnly;
                        preview.AssemblyDecision = IfcAssemblyDecision.SingleChildWrapper;
                        preview.IncludeInGaPreview = false;
                        preview.VisibleGeometryOwnerGlobalIds = new List<string> { childId };
                        preview.DecisionReason = "Single-child assembly wrapper (Tekla-style). Unrolled child treated as liftable element; parent is grouping node.";

                        if (previews.TryGetValue(childId, out var childPreview))
                        {
                            childPreview.Role = IfcElementRole.LiftableElement;
                            childPreview.ParentGlobalId = preview.GlobalId;
                            childPreview.ParentGlobalIdSource = "IfcDecomposition";
                            childPreview.IncludeInGaPreview = HasGaApprovedExplicitWeight(childPreview);
                            childPreview.VisibleGeometryOwnerGlobalIds = new List<string> { childId };
                            childPreview.DecisionReason = childPreview.IncludeInGaPreview
                                ? "Unrolled structural child from single-child assembly wrapper."
                                : $"Unrolled structural child from single-child assembly wrapper, but blocked from GA because {DescribeWeightBlock(childPreview)}";
                        }
                        continue;
                    }

                    // Case B: Multiple physical children
                    if (nonInternalChildrenIds.Count > 1)
                    {
                        // Revit Beam System Rule
                        if (profile.Exporter == IfcExporterProfile.Revit &&
                            profile.IsStructuralGroupingAssembly(preview.Name, preview.RawIfcType))
                        {
                            preview.Role = IfcElementRole.GroupingOnly;
                            preview.AssemblyDecision = IfcAssemblyDecision.RevitStructuralBeamSystemGrouping;
                            preview.IncludeInGaPreview = false;
                            preview.DecisionReason = "Revit Structural Beam/Framing System. Assembly is grouping node; child beams unrolled as independent liftable items.";

                            foreach (var childId in nonInternalChildrenIds)
                            {
                                if (previews.TryGetValue(childId, out var childPreview))
                                {
                                    childPreview.Role = IfcElementRole.LiftableElement;
                                    childPreview.ParentGlobalId = preview.GlobalId;
                                    childPreview.ParentGlobalIdSource = "IfcDecomposition";
                                    childPreview.IncludeInGaPreview = HasGaApprovedExplicitWeight(childPreview);
                                    childPreview.VisibleGeometryOwnerGlobalIds = new List<string> { childId };
                                    childPreview.DecisionReason = childPreview.IncludeInGaPreview
                                        ? "Unrolled structural beam child from Revit Structural Beam System grouping."
                                        : $"Unrolled structural beam child from Revit Structural Beam System grouping, but blocked from GA because {DescribeWeightBlock(childPreview)}";
                                }
                            }
                            continue;
                        }

                        // Revit sometimes exports safe precast/structural groupings as generic assemblies.
                        // Only unwrap when geometry belongs to children and every child is a known structural product.
                        if (profile.Exporter == IfcExporterProfile.Revit &&
                            IsSafeRevitChildGroupingAssembly(assemblyDetail, nonInternalChildrenIds, previews))
                        {
                            preview.Role = IfcElementRole.GroupingOnly;
                            preview.AssemblyDecision = IfcAssemblyDecision.GenericGroupingAssembly;
                            preview.IncludeInGaPreview = false;
                            preview.DecisionReason = "Safe Revit child-geometry grouping. Parent is grouping only; structural children are unrolled as independent liftable items.";

                            foreach (var childId in nonInternalChildrenIds)
                            {
                                if (previews.TryGetValue(childId, out var childPreview))
                                {
                                    childPreview.Role = IfcElementRole.LiftableElement;
                                    childPreview.ParentGlobalId = preview.GlobalId;
                                    childPreview.ParentGlobalIdSource = "IfcDecomposition";
                                    childPreview.IncludeInGaPreview = HasGaApprovedExplicitWeight(childPreview);
                                    childPreview.VisibleGeometryOwnerGlobalIds = new List<string> { childId };
                                    childPreview.DecisionReason = childPreview.IncludeInGaPreview
                                        ? "Unrolled structural child from safe Revit grouping assembly."
                                        : $"Unrolled structural child from safe Revit grouping assembly, but blocked from GA because {DescribeWeightBlock(childPreview)}";
                                }
                            }
                            continue;
                        }

                        // Tekla Precast Cast-Unit / Welded Lift Assembly Rule
                        if (profile.Exporter == IfcExporterProfile.Tekla &&
                            profile.IsPrecastLiftAssembly(preview.Name, preview.RawIfcType))
                        {
                            preview.Role = IfcElementRole.LiftAssembly;
                            preview.AssemblyDecision = IfcAssemblyDecision.TeklaCastUnitOrWeldedLiftAssembly;

                            // Check geometry owners: parent has no geometry in ChildrenOnly pattern, so visible owners are children
                            if (assemblyDetail.GeometryOwnershipPattern == "ChildrenOnly")
                            {
                                preview.VisibleGeometryOwnerGlobalIds = nonInternalChildrenIds.ToList();
                            }
                            else
                            {
                                preview.VisibleGeometryOwnerGlobalIds = new List<string> { preview.GlobalId };
                            }

                            if (HasGaApprovedExplicitWeight(preview))
                            {
                                preview.IncludeInGaPreview = true;
                                preview.DecisionReason = "Tekla precast cast-unit/welded aggregate lift item. Parent assembly has trusted total weight; children double-counting excluded.";

                                // Exclude structural children from GA to avoid double-counting weight
                                foreach (var childId in nonInternalChildrenIds)
                                {
                                    if (previews.TryGetValue(childId, out var childPreview))
                                    {
                                        childPreview.Role = IfcElementRole.Excluded;
                                        childPreview.IncludeInGaPreview = false;
                                        childPreview.DecisionReason = "Aggregated component of Tekla Precast Cast-Unit. Excluded from independent lifting to avoid double-counting.";
                                    }
                                }
                            }
                            else
                            {
                                preview.IncludeInGaPreview = false;
                                preview.Role = IfcElementRole.NeedsReview;
                                preview.Warnings.Add("Tekla precast cast-unit total weight is missing or untrusted. Lift calculation blocked until weight is resolved.");
                                preview.DecisionReason = "Tekla Precast Cast-Unit lacks trusted total weight. Element review required.";
                            }
                            continue;
                        }

                        // Generic grouping fallback
                        preview.Role = IfcElementRole.NeedsReview;
                        preview.AssemblyDecision = IfcAssemblyDecision.UnknownNeedsReview;
                        preview.IncludeInGaPreview = false;
                        preview.DecisionReason = "Ambiguous assembly container with multiple children. Engineer verification required.";
                        continue;
                    }
                }

                // Rule 3: Structural physical leaf elements
                if (preview.Role == IfcElementRole.NeedsReview) // not yet overwritten by assembly rules
                {
                    if (preview.ParentGlobalId != null && previews.TryGetValue(preview.ParentGlobalId, out var parentCP))
                    {
                        // If parent assembly requires review, block the child as well (BlockedDueToParentNeedsReview)
                        if (parentCP.Role == IfcElementRole.NeedsReview)
                        {
                            preview.Role = IfcElementRole.NeedsReview;
                            preview.IncludeInGaPreview = false;
                            preview.DecisionReason = "Blocked due to parent assembly requiring review (BlockedDueToParentNeedsReview).";
                            continue;
                        }

                        // If parent assembly is a Tekla precast lift assembly, child elements are components of the assembly, not independent lift items
                        if (parentCP.AssemblyDecision == IfcAssemblyDecision.TeklaCastUnitOrWeldedLiftAssembly)
                        {
                            preview.Role = IfcElementRole.Excluded;
                            preview.IncludeInGaPreview = false;
                            preview.DecisionReason = "Swallowed as child component of Tekla Lift Assembly.";
                            continue;
                        }
                    }

                    preview.Role = IfcElementRole.LiftableElement;
                    preview.IncludeInGaPreview = HasGaApprovedExplicitWeight(preview);
                    if (preview.VisibleGeometryOwnerGlobalIds.Count == 0)
                    {
                        preview.VisibleGeometryOwnerGlobalIds.Add(preview.GlobalId);
                    }
                    preview.DecisionReason = preview.IncludeInGaPreview
                        ? "Direct structural physical leaf element with explicit GA-approved weight."
                        : $"Direct structural physical leaf element blocked from GA because {DescribeWeightBlock(preview)}";
                }
            }

            // Populate overall counts
            summary.Elements = previews.Values.ToList();

            foreach (var elem in summary.Elements)
            {
                switch (elem.Role)
                {
                    case IfcElementRole.LiftableElement:
                        summary.NormalizedLiftableCount++;
                        break;
                    case IfcElementRole.GroupingOnly:
                        summary.GroupingOnlyCount++;
                        break;
                    case IfcElementRole.LiftAssembly:
                        summary.LiftAssemblyCount++;
                        break;
                    case IfcElementRole.InternalAuxiliary:
                        summary.InternalAuxiliaryCount++;
                        break;
                    case IfcElementRole.Excluded:
                        summary.ExcludedCount++;
                        break;
                    case IfcElementRole.NeedsReview:
                        summary.NeedsReviewCount++;
                        break;
                }

                if (elem.IncludeInRenderPreview)
                {
                    summary.VisibleRenderGeometryCount += elem.VisibleGeometryOwnerGlobalIds.Count;
                }

                summary.HiddenInternalGeometryCount += elem.HiddenInternalGeometryOwnerGlobalIds.Count;

                if (!elem.IncludeInGaPreview && (elem.Role == IfcElementRole.LiftableElement || elem.Role == IfcElementRole.LiftAssembly))
                {
                    if (elem.WeightTrustLevel == IfcWeightTrustLevel.Missing)
                    {
                        summary.BlockedGaMissingWeightCount++;
                    }
                    else if (elem.WeightTrustLevel == IfcWeightTrustLevel.ExplicitZeroFromIFC)
                    {
                        summary.BlockedGaZeroWeightCount++;
                    }
                    else if (elem.WeightTrustLevel == IfcWeightTrustLevel.EstimatedFallback_NotAllowedForGA)
                    {
                        summary.BlockedGaFallbackWeightCount++;
                    }
                }
            }

            return summary;
        }

        private static IfcWeightTrustLevel MapWeightTrustLevel(ElementWeightAuditDetail? weightDetail)
        {
            if (weightDetail == null) return IfcWeightTrustLevel.Missing;

            return weightDetail.WeightStatus switch
            {
                "ExplicitPositive" => weightDetail.WeightSource.Contains("Quantity") ? IfcWeightTrustLevel.ExplicitQuantity : IfcWeightTrustLevel.ExplicitProperty,
                "ExplicitZeroFromIFC" => IfcWeightTrustLevel.ExplicitZeroFromIFC,
                "EstimatedByFallback" => IfcWeightTrustLevel.EstimatedFallback_NotAllowedForGA,
                "MissingWeight" => IfcWeightTrustLevel.Missing,
                "InvalidNegative" => IfcWeightTrustLevel.SuspiciousExtreme,
                "SuspiciousExtreme" => IfcWeightTrustLevel.SuspiciousExtreme,
                "CannotConvertUnit" => IfcWeightTrustLevel.AmbiguousUnit,
                _ => IfcWeightTrustLevel.Missing
            };
        }

        private static bool HasGaApprovedExplicitWeight(IfcNormalizedElementPreview preview)
        {
            return preview.WeightTrustLevel == IfcWeightTrustLevel.ExplicitQuantity ||
                   preview.WeightTrustLevel == IfcWeightTrustLevel.ExplicitProperty;
        }

        private static string DescribeWeightBlock(IfcNormalizedElementPreview preview)
        {
            return preview.WeightTrustLevel switch
            {
                IfcWeightTrustLevel.Missing => "no explicit weight was found",
                IfcWeightTrustLevel.ExplicitZeroFromIFC => "IFC explicitly reports zero weight",
                IfcWeightTrustLevel.EstimatedFallback_NotAllowedForGA => "weight was estimated by density fallback and needs explicit approval",
                IfcWeightTrustLevel.RequiresUserApproval => "weight requires user approval before GA",
                IfcWeightTrustLevel.AmbiguousUnit => "the weight unit is ambiguous",
                IfcWeightTrustLevel.SuspiciousExtreme => "the weight value is suspicious and needs review",
                _ => $"weight trust level is {preview.WeightTrustLevel}"
            };
        }

        private static bool IsSafeRevitChildGroupingAssembly(
            AssemblyDecompositionDetail assemblyDetail,
            IReadOnlyList<string> nonInternalChildrenIds,
            IReadOnlyDictionary<string, IfcNormalizedElementPreview> previews)
        {
            if (nonInternalChildrenIds.Count == 0)
                return false;

            if (assemblyDetail.ParentHasGeometry || !assemblyDetail.ChildrenHaveGeometry)
                return false;

            if (!string.Equals(assemblyDetail.GeometryOwnershipPattern, "ChildrenOnly", StringComparison.OrdinalIgnoreCase))
                return false;

            foreach (var childId in nonInternalChildrenIds)
            {
                if (!previews.TryGetValue(childId, out var child))
                    return false;

                if (child.RawIfcType.Equals("IfcElementAssembly", StringComparison.OrdinalIgnoreCase))
                    return false;

                if (!IsKnownStructuralRevitChild(child))
                    return false;

                var childDetail = assemblyDetail.ChildDetails.FirstOrDefault(c =>
                    string.Equals(c.ChildGlobalId, childId, StringComparison.Ordinal));
                if (childDetail != null && !childDetail.HasGeometry)
                    return false;
            }

            return true;
        }

        private static bool IsKnownStructuralRevitChild(IfcNormalizedElementPreview child)
        {
            var rawType = child.RawIfcType ?? string.Empty;
            var category = child.CandidateCategory ?? string.Empty;

            return rawType.ContainsAny("IfcBeam", "IfcColumn", "IfcSlab", "IfcWall", "IfcMember", "IfcPlate", "IfcFooting") ||
                   category.ContainsAny("Beam", "Column", "Slab", "Wall", "Member", "Plate", "Footing");
        }

        private static bool HasDirectGeometry(IIfcProduct product, Xbim3DModelContext geometryContext)
        {
            try
            {
                return geometryContext.ShapeInstancesOf(product).Any();
            }
            catch
            {
                return false;
            }
        }

        private string GeneratePreviewReportsCore(IfcNormalizationPreviewSummary preview, string? projectFolderPath, CancellationToken cancellationToken)
        {
            if (string.IsNullOrWhiteSpace(projectFolderPath))
            {
                throw new ArgumentException("projectFolderPath must not be null or whitespace. Ensure a project is loaded.", nameof(projectFolderPath));
            }
            if (!Directory.Exists(projectFolderPath))
            {
                throw new DirectoryNotFoundException($"The specified project folder path does not exist: {projectFolderPath}");
            }

            string outputDirectory = Path.Combine(projectFolderPath, "Reports", "IFC_Audit");
            Directory.CreateDirectory(outputDirectory);

            var timestamp = DateTime.UtcNow.ToString("yyyyMMdd_HHmmss");
            var jsonPath = Path.Combine(outputDirectory, $"debug_ifc_normalization_preview_details_{timestamp}.json");
            var csvPath = Path.Combine(outputDirectory, $"debug_ifc_normalization_preview_elements_{timestamp}.csv");

            cancellationToken.ThrowIfCancellationRequested();
            // 1. JSON details
            var jsonOptions = new JsonSerializerOptions
            {
                WriteIndented = true,
                DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
            };
            var jsonContent = JsonSerializer.Serialize(preview, jsonOptions);
            File.WriteAllText(jsonPath, jsonContent, Encoding.UTF8);

            cancellationToken.ThrowIfCancellationRequested();
            // 2. CSV Elements Matrix (Metadata included at top per user request)
            GeneratePreviewCsv(preview, csvPath);

            Debug.WriteLine($"[IfcNormalizationPreview] JSON and CSV Reports generated successfully at: {outputDirectory}");
            return outputDirectory;
        }

        private static void GeneratePreviewCsv(IfcNormalizationPreviewSummary preview, string csvPath)
        {
            var sb = new StringBuilder();

            // Append professional IFC normalization preview metadata header (Phase 2A request)
            sb.AppendLine("# ==========================================================================");
            sb.AppendLine("# IFC NORMALIZATION DECISION PREVIEW PROFESSIONAL REPORT");
            sb.AppendLine(string.Format(CultureInfo.InvariantCulture, "# File Name: {0}", preview.FileName));
            sb.AppendLine(string.Format(CultureInfo.InvariantCulture, "# Exporter Application: {0}", preview.ExporterApplication));
            sb.AppendLine(string.Format(CultureInfo.InvariantCulture, "# Resolved Exporter Profile: {0}", preview.DetectedProfile));
            sb.AppendLine(string.Format(CultureInfo.InvariantCulture, "# Total Physical Elements Ingested: {0}", preview.TotalRawProductCount));
            sb.AppendLine(string.Format(CultureInfo.InvariantCulture, "# Proposed LiftableElements (Leafs): {0}", preview.NormalizedLiftableCount));
            sb.AppendLine(string.Format(CultureInfo.InvariantCulture, "# Proposed GroupingOnly Container Nodes: {0}", preview.GroupingOnlyCount));
            sb.AppendLine(string.Format(CultureInfo.InvariantCulture, "# Proposed LiftAssemblies: {0}", preview.LiftAssemblyCount));
            sb.AppendLine(string.Format(CultureInfo.InvariantCulture, "# Proposed InternalAuxiliary (Reinforcements): {0}", preview.InternalAuxiliaryCount));
            sb.AppendLine(string.Format(CultureInfo.InvariantCulture, "# Proposed Excluded Elements (Swallowed Components): {0}", preview.ExcludedCount));
            sb.AppendLine(string.Format(CultureInfo.InvariantCulture, "# Proposed NeedsReview Elements: {0}", preview.NeedsReviewCount));
            sb.AppendLine(string.Format(CultureInfo.InvariantCulture, "# Proposed GA-Blocked (Missing Weight): {0}", preview.BlockedGaMissingWeightCount));
            sb.AppendLine(string.Format(CultureInfo.InvariantCulture, "# Proposed GA-Blocked (Zero Weight): {0}", preview.BlockedGaZeroWeightCount));
            sb.AppendLine(string.Format(CultureInfo.InvariantCulture, "# Proposed GA-Blocked (Fallback Density Weight): {0}", preview.BlockedGaFallbackWeightCount));
            sb.AppendLine(string.Format(CultureInfo.InvariantCulture, "# Generated At (UTC): {0}", DateTime.UtcNow.ToString("yyyy-MM-dd HH:mm:ss")));
            sb.AppendLine("# ==========================================================================");
            sb.AppendLine();

            sb.AppendLine("GlobalId,Name,RawIfcType,ResolvedRole,AssemblyDecision,WeightTons,WeightTrustLevel,IncludeInGaPreview,VisibleGeometryOwners,HiddenGeometryOwners,DecisionReason");

            foreach (var elem in preview.Elements)
            {
                sb.AppendLine(string.Format(CultureInfo.InvariantCulture,
                    "\"{0}\",\"{1}\",\"{2}\",\"{3}\",\"{4}\",{5},\"{6}\",\"{7}\",\"{8}\",\"{9}\",\"{10}\"",
                    elem.GlobalId,
                    EscapeCsv(elem.Name),
                    elem.RawIfcType,
                    elem.Role,
                    elem.AssemblyDecision,
                    elem.WeightStandardValueTons?.ToString("F4", CultureInfo.InvariantCulture) ?? "null",
                    elem.WeightTrustLevel,
                    elem.IncludeInGaPreview,
                    string.Join("; ", elem.VisibleGeometryOwnerGlobalIds),
                    string.Join("; ", elem.HiddenInternalGeometryOwnerGlobalIds),
                    EscapeCsv(elem.DecisionReason)
                ));
            }

            File.WriteAllText(csvPath, sb.ToString(), Encoding.UTF8);
        }

        private static void GeneratePreviewMarkdown(IfcNormalizationPreviewSummary preview, string mdPath)
        {
            var sb = new StringBuilder();
            sb.AppendLine("# IFC Normalization Decision Preview Report");
            sb.AppendLine();
            sb.AppendLine("## 1. Executive Summary");
            sb.AppendLine("This non-destructive report previews the normalization decisions that will classify, consolidate, and unroll elements when the Phase 2 production changes are active.");
            sb.AppendLine();
            sb.AppendLine($"- **Exporter Profile Resolved:** `{preview.DetectedProfile}` (Originating System: `{preview.ExporterApplication}`)");
            sb.AppendLine($"- **Total Raw Elements Ingested:** `{preview.TotalRawProductCount}`");
            sb.AppendLine($"- **Proposed Liftable Elements (Leafs):** `{preview.NormalizedLiftableCount}`");
            sb.AppendLine($"- **Proposed Grouping Container Nodes:** `{preview.GroupingOnlyCount}`");
            sb.AppendLine($"- **Proposed Consolidated Lift Assemblies:** `{preview.LiftAssemblyCount}`");
            sb.AppendLine($"- **Proposed Hidden Reinforcement / Auxiliary Items:** `{preview.InternalAuxiliaryCount}`");
            sb.AppendLine($"- **Proposed Excluded Components (Double-Counting Mitigated):** `{preview.ExcludedCount}`");
            sb.AppendLine($"- **Proposed Elements Requiring Review:** `{preview.NeedsReviewCount}`");
            sb.AppendLine();

            sb.AppendLine("## 2. Geometry Representation Preview");
            sb.AppendLine($"- **Proposed Visible Render Geometry Owners:** `{preview.VisibleRenderGeometryCount}`");
            sb.AppendLine($"- **Proposed Hidden Internal Geometry Owners:** `{preview.HiddenInternalGeometryCount}`");
            sb.AppendLine();

            sb.AppendLine("## 3. Crane GA Calculation Blockers");
            sb.AppendLine("To protect crane optimization safety, elements are blocked from genetic calculations if they lack explicit, trusted weights.");
            sb.AppendLine();
            sb.AppendLine($"| GA Status | Block Reason | Element Count | Impact on Crane Path Optimizer |");
            sb.AppendLine($"| :--- | :--- | :---: | :--- |");
            sb.AppendLine($"| **Blocked** | Missing weight property | `{preview.BlockedGaMissingWeightCount}` | Excludes element from lift checks until rule is provided. |");
            sb.AppendLine($"| **Blocked** | Explicit zero weight in IFC | `{preview.BlockedGaZeroWeightCount}` | Excludes element from lift checks until operator verifies zero-state. |");
            sb.AppendLine($"| **Blocked** | Fallback density estimation | `{preview.BlockedGaFallbackWeightCount}` | Blocked from calculations. Fallbacks must not act as GA-ready weights without approved rules. |");
            sb.AppendLine();

            sb.AppendLine("## 4. Key Structural Normalization Decisions");

            // Revit Beam Systems
            var revitDecisions = preview.Elements.Where(e => e.AssemblyDecision == IfcAssemblyDecision.RevitStructuralBeamSystemGrouping).ToList();
            sb.AppendLine("### Revit Structural Beam Systems");
            if (revitDecisions.Count == 0)
            {
                sb.AppendLine("*No Revit Structural Beam Systems detected in this file.*");
            }
            else
            {
                sb.AppendLine("| System Assembly GlobalId | Name | Proposed Unrolled Children Beams | Decision / Action |");
                sb.AppendLine("| :--- | :--- | :---: | :--- |");
                foreach (var rd in revitDecisions.Take(10))
                {
                    sb.AppendLine($"| `{rd.GlobalId}` | {rd.Name} | {rd.ChildGlobalIds.Count} | Assembly parent marked as `GroupingOnly`; children beams unrolled. |");
                }
                if (revitDecisions.Count > 10) sb.AppendLine($"| *... and {revitDecisions.Count - 10} more beam systems* | | | |");
            }
            sb.AppendLine();

            // Tekla precast cast units
            var teklaDecisions = preview.Elements.Where(e => e.AssemblyDecision == IfcAssemblyDecision.TeklaCastUnitOrWeldedLiftAssembly).ToList();
            sb.AppendLine("### Tekla Precast Cast Units & Welded Assemblies");
            if (teklaDecisions.Count == 0)
            {
                sb.AppendLine("*No Tekla precast assemblies detected.*");
            }
            else
            {
                sb.AppendLine("| Parent Assembly GlobalId | Name | Components Excluded | Total Assembly Weight (Tons) | Status / GA Status |");
                sb.AppendLine("| :--- | :--- | :---: | :---: | :--- |");
                foreach (var td in teklaDecisions.Take(15))
                {
                    sb.AppendLine($"| `{td.GlobalId}` | {td.Name} | {td.ChildGlobalIds.Count} | {td.WeightStandardValueTons?.ToString("F3") ?? "Missing"} | {(td.IncludeInGaPreview ? "`GA Ready`" : "`Blocked — Needs Weight`")} |");
                }
                if (teklaDecisions.Count > 15) sb.AppendLine($"| *... and {teklaDecisions.Count - 15} more Tekla assemblies* | | | | |");
            }
            sb.AppendLine();

            // Reinforcement
            var auxiliaryDecisions = preview.Elements.Where(e => e.Role == IfcElementRole.InternalAuxiliary).ToList();
            sb.AppendLine("### Reinforcement Steel & Auxiliary Handling");
            sb.AppendLine($"- **Total rebar/reinforcement elements:** `{auxiliaryDecisions.Count}`");
            sb.AppendLine("- **Render visibility state:** `Hidden` (reinforcements will not render to protect 3D viewport performance).");
            sb.AppendLine("- **GA optimization status:** `Excluded` (reinforcement bars will never become independent lifting items).");
            sb.AppendLine();

            var rebarWithContributions = preview.Elements.Where(e => e.ReinforcementWeightContributionTons.HasValue).ToList();
            sb.AppendLine("#### Reinforcement Weight Roll-Up Summary");
            if (rebarWithContributions.Count == 0)
            {
                sb.AppendLine("*No roll-up opportunities found.*");
            }
            else
            {
                sb.AppendLine("| Host Element GlobalId | Host Type | Reinforcement Weight (Tons) | Host Weight Trust Level | Action Proposed |");
                sb.AppendLine("| :--- | :--- | :---: | :--- | :--- |");
                foreach (var hc in rebarWithContributions)
                {
                    sb.AppendLine($"| `{hc.GlobalId}` | {hc.CandidateCategory} | {hc.ReinforcementWeightContributionTons:F4} | {hc.WeightTrustLevel} | Roll-up reinforcement weight as precast aggregate. Requires User Approval. |");
                }
            }
            sb.AppendLine();

            // Needs Review
            var reviewDecisions = preview.Elements.Where(e => e.Role == IfcElementRole.NeedsReview).ToList();
            sb.AppendLine("## 5. Unknown or NeedsReview Summary");
            if (reviewDecisions.Count == 0)
            {
                sb.AppendLine("*None detected. Excellent schema conformance!*");
            }
            else
            {
                sb.AppendLine("| Element GlobalId | Raw Type | Name | Weight Trust Level | Reason / Action Required |");
                sb.AppendLine("| :--- | :--- | :--- | :--- | :--- |");
                foreach (var rd in reviewDecisions.Take(15))
                {
                    sb.AppendLine($"| `{rd.GlobalId}` | `{rd.RawIfcType}` | {rd.Name} | {rd.WeightTrustLevel} | {rd.DecisionReason} |");
                }
                if (reviewDecisions.Count > 15) sb.AppendLine($"| *... and {reviewDecisions.Count - 15} more elements requiring review* | | | | |");
            }
            sb.AppendLine();

            sb.AppendLine("## 6. Recommendations for Phase 2 Production Integration");
            sb.AppendLine("> [!IMPORTANT]");
            sb.AppendLine("> The normalization layer resolved **Tekla Precast Cast Units** and **Revit Beam Systems** successfully.");
            sb.AppendLine("> During the next phase, we must configure the primary engineering extraction pipeline to consume these exact candidate roles:");
            sb.AppendLine("> 1. **Do not lift Swallowed components:** Child structural components inside precast aggregates must not appear in the viewport elements list or database.");
            sb.AppendLine("> 2. **Apply Visible Geometry Owners list:** When drawing unrolled children or assemblies, feed the `VisibleGeometryOwnerGlobalIds` list into Helix viewport rendering to draw composite meshes correctly.");
            sb.AppendLine("> 3. **User-Approved Rule Editor:** Add a simple rule dialog for `NeedsReview` and `MissingWeight` elements to let users define density fallbacks or material properties before activating GA path planning.");
            sb.AppendLine();

            File.WriteAllText(mdPath, sb.ToString(), Encoding.UTF8);
        }

        private static string EscapeCsv(string value)
        {
            return value.Replace("\"", "\"\"");
        }
    }

    internal static class ElementExtensions
    {
        internal static bool HasGeometryOwner(this IfcNormalizedElementPreview element)
        {
            return element.VisibleGeometryOwnerGlobalIds.Count > 0 ||
                   element.HiddenInternalGeometryOwnerGlobalIds.Count > 0;
        }
    }
}

using System;
using System.Collections.Generic;
using System.Linq;
using CraneOptimizer.Domain.Diagnostics;
using CraneOptimizer.Domain.Schemas;

namespace CraneOptimizer.Application.Services
{
    public class ImportReadinessService : IImportReadinessService
    {
        public ImportReadinessSummary BuildSummary(IEnumerable<PrecastElement> elements, bool isCheckerEnabled = true)
        {
            var summary = new ImportReadinessSummary();
            if (elements == null)
            {
                summary.OverallStatus = ValidationStatus.Valid;
                return summary;
            }

            var elementList = elements.ToList();
            summary.TotalElements = elementList.Count;
            summary.RenderableElements = elementList.Count(e => e.IncludeInRender);

            // Build a lookup map of elements to check parent-child double-counting risk
            var allElementsMap = new Dictionary<string, PrecastElement>(StringComparer.Ordinal);
            foreach (var el in elementList)
            {
                if (!string.IsNullOrEmpty(el.ElementID) && !allElementsMap.ContainsKey(el.ElementID))
                {
                    allElementsMap[el.ElementID] = el;
                }
            }

            // Calculate active GA candidates and blocked elements based on safety gates
            summary.GaCandidateElements = elementList.Count(e => IsGaCandidate(e, allElementsMap));
            summary.GaBlockedElements = elementList.Count(e => e.IncludeInGa && !IsGaCandidate(e, allElementsMap));

            var messages = new List<ValidationMessage>();

            if (!isCheckerEnabled)
            {
                // Element Checker is disabled - skip diagnostics and return neutral Not Checked status
                summary.OverallStatus = ValidationStatus.Valid; // Neutral status

                messages.Add(new ValidationMessage
                {
                    Code = "ElementCheckerDisabled",
                    Severity = "Info",
                    Message = "Element Checker is disabled. Readiness diagnostics were not evaluated.",
                    Category = "GA",
                    RecommendedAction = "Enable the Element Checker toggle to run validation diagnostics."
                });

                summary.Messages = messages;
                return summary;
            }

            // --- Running Diagnostics ---
            
            // 1. MissingAssemblyMark
            var missingMarkIds = new List<string>();
            foreach (var el in elementList)
            {
                if (!el.AssemblyMarkIsExplicit)
                {
                    missingMarkIds.Add(el.ElementID);
                }
            }
            summary.MissingAssemblyMarkCount = missingMarkIds.Count;
            if (missingMarkIds.Count > 0)
            {
                messages.Add(new ValidationMessage
                {
                    Code = "MissingAssemblyMark",
                    Severity = "Warning",
                    Message = "No explicit assembly-level or element-level mark found; falling back to display name.",
                    AffectedCount = missingMarkIds.Count,
                    AffectedElementIds = missingMarkIds,
                    RecommendedAction = "Assign an explicit assembly mark in the source modeling application.",
                    SourceSystem = GetFirstSourceSystem(elementList, missingMarkIds),
                    Category = "Identity"
                });
            }

            // 2. LowConfidenceMapping
            var lowConfidenceIds = new List<string>();
            foreach (var el in elementList)
            {
                if (el.AssemblyMarkConfidence == AssemblyMarkConfidence.Low || el.AssemblyMarkConfidence == AssemblyMarkConfidence.None)
                {
                    lowConfidenceIds.Add(el.ElementID);
                }
            }
            summary.LowConfidenceMappingCount = lowConfidenceIds.Count;
            if (lowConfidenceIds.Count > 0)
            {
                messages.Add(new ValidationMessage
                {
                    Code = "LowConfidenceMapping",
                    Severity = "Warning",
                    Message = "Assembly mark mapping confidence is low or none.",
                    AffectedCount = lowConfidenceIds.Count,
                    AffectedElementIds = lowConfidenceIds,
                    RecommendedAction = "Review your source mapping profiles or update mapping rules to use a higher-confidence property.",
                    SourceSystem = GetFirstSourceSystem(elementList, lowConfidenceIds),
                    Category = "Mapping"
                });
            }

            // 3. EstimatedWeightBlockedFromGA & MissingTrustedWeight
            var estimatedWeightIds = new List<string>();
            var missingTrustedWeightIds = new List<string>();
            foreach (var el in elementList)
            {
                if (el.WeightTrustLevel == IfcWeightTrustLevel.EstimatedFallback_NotAllowedForGA)
                {
                    estimatedWeightIds.Add(el.ElementID);
                }
                
                var isWeightTrusted = el.WeightTrustLevel == IfcWeightTrustLevel.ExplicitQuantity ||
                                      el.WeightTrustLevel == IfcWeightTrustLevel.ExplicitProperty;
                if (!isWeightTrusted)
                {
                    missingTrustedWeightIds.Add(el.ElementID);
                }
            }
            summary.EstimatedWeightBlockedCount = estimatedWeightIds.Count;
            summary.MissingTrustedWeightCount = missingTrustedWeightIds.Count;

            if (estimatedWeightIds.Count > 0)
            {
                // Error only when it leaves no valid candidates or materially blocks GA readiness
                var severity = (summary.GaCandidateElements == 0) ? "Error" : "Warning";
                messages.Add(new ValidationMessage
                {
                    Code = "EstimatedWeightBlockedFromGA",
                    Severity = severity,
                    Message = "Estimated fallback weight is used and strictly blocked from GA.",
                    AffectedCount = estimatedWeightIds.Count,
                    AffectedElementIds = estimatedWeightIds,
                    RecommendedAction = "Verify weight properties in your CAD/BIM software, or provide explicit quantities so the element is not blocked from GA.",
                    SourceSystem = GetFirstSourceSystem(elementList, estimatedWeightIds),
                    Category = "Weight"
                });
            }

            if (missingTrustedWeightIds.Count > 0)
            {
                messages.Add(new ValidationMessage
                {
                    Code = "MissingTrustedWeight",
                    Severity = "Warning",
                    Message = "Element weight is not from an explicit trusted source.",
                    AffectedCount = missingTrustedWeightIds.Count,
                    AffectedElementIds = missingTrustedWeightIds,
                    RecommendedAction = "Ensure elements have explicit mass/weight quantities exported from your modeling software.",
                    SourceSystem = GetFirstSourceSystem(elementList, missingTrustedWeightIds),
                    Category = "Weight"
                });
            }

            // 4. MissingAssemblyVolume
            var missingVolumeIds = new List<string>();
            foreach (var el in elementList)
            {
                if (!el.VolumeM3.HasValue || el.VolumeM3.Value <= 0)
                {
                    missingVolumeIds.Add(el.ElementID);
                }
            }
            summary.MissingVolumeCount = missingVolumeIds.Count;
            if (missingVolumeIds.Count > 0)
            {
                messages.Add(new ValidationMessage
                {
                    Code = "MissingAssemblyVolume",
                    Severity = "Warning",
                    Message = "Assembly volume is missing or zero.",
                    AffectedCount = missingVolumeIds.Count,
                    AffectedElementIds = missingVolumeIds,
                    RecommendedAction = "Check export settings to include volume quantities for precast elements.",
                    SourceSystem = GetFirstSourceSystem(elementList, missingVolumeIds),
                    Category = "Geometry"
                });
            }

            // 5. MissingAssemblyMaterial
            var missingMaterialIds = new List<string>();
            foreach (var el in elementList)
            {
                if (string.IsNullOrWhiteSpace(el.ElementMaterial) || el.ElementMaterial == "-")
                {
                    missingMaterialIds.Add(el.ElementID);
                }
            }
            summary.MissingMaterialCount = missingMaterialIds.Count;
            if (missingMaterialIds.Count > 0)
            {
                messages.Add(new ValidationMessage
                {
                    Code = "MissingAssemblyMaterial",
                    Severity = "Info", // Info since fallback material is available
                    Message = "Assembly material is not specified; fallback density applied.",
                    AffectedCount = missingMaterialIds.Count,
                    AffectedElementIds = missingMaterialIds,
                    RecommendedAction = "Assign materials to elements in your modeling environment to enable correct density fallbacks.",
                    SourceSystem = GetFirstSourceSystem(elementList, missingMaterialIds),
                    Category = "Mapping"
                });
            }

            // 6. MissingAssemblyCOG
            var missingCogIds = new List<string>();
            foreach (var el in elementList)
            {
                if (el.ElementCOG == null ||
                    (Math.Abs(el.ElementCOG.X) < 0.0001 &&
                     Math.Abs(el.ElementCOG.Y) < 0.0001 &&
                     Math.Abs(el.ElementCOG.Z) < 0.0001) ||
                    double.IsNaN(el.ElementCOG.X) || double.IsInfinity(el.ElementCOG.X) ||
                    double.IsNaN(el.ElementCOG.Y) || double.IsInfinity(el.ElementCOG.Y) ||
                    double.IsNaN(el.ElementCOG.Z) || double.IsInfinity(el.ElementCOG.Z))
                {
                    missingCogIds.Add(el.ElementID);
                }
            }
            summary.MissingCogCount = missingCogIds.Count;
            if (missingCogIds.Count > 0)
            {
                messages.Add(new ValidationMessage
                {
                    Code = "MissingAssemblyCOG",
                    Severity = "Warning",
                    Message = "Assembly Center of Gravity (COG) is missing, zero, or invalid.",
                    AffectedCount = missingCogIds.Count,
                    AffectedElementIds = missingCogIds,
                    RecommendedAction = "Review element export settings or center of gravity properties in your CAD/BIM tool.",
                    SourceSystem = GetFirstSourceSystem(elementList, missingCogIds),
                    Category = "Geometry"
                });
            }

            // 7. ParentChildDoubleCountRisk
            var parentChildRiskIds = new List<string>();
            foreach (var el in elementList)
            {
                if (!string.IsNullOrWhiteSpace(el.ParentGlobalId) && el.IncludeInGa)
                {
                    if (allElementsMap.TryGetValue(el.ParentGlobalId, out var parent))
                    {
                        if (parent.IncludeInGa)
                        {
                            parentChildRiskIds.Add(el.ElementID);
                        }
                    }
                }
            }
            summary.ParentChildDoubleCountRiskCount = parentChildRiskIds.Count;
            if (parentChildRiskIds.Count > 0)
            {
                messages.Add(new ValidationMessage
                {
                    Code = "ParentChildDoubleCountRisk",
                    Severity = "Warning",
                    Message = "Child component is enabled for GA alongside its parent assembly.",
                    AffectedCount = parentChildRiskIds.Count,
                    AffectedElementIds = parentChildRiskIds,
                    RecommendedAction = "Ensure child parts are excluded from GA when their parent assembly is already included.",
                    SourceSystem = GetFirstSourceSystem(elementList, parentChildRiskIds),
                    Category = "GA"
                });
            }

            // 8. NoGaCandidates
            if (summary.GaCandidateElements == 0)
            {
                messages.Add(new ValidationMessage
                {
                    Code = "NoGaCandidates",
                    Severity = "Error",
                    Message = "No valid GA candidate elements found in the scene.",
                    AffectedCount = 0,
                    AffectedElementIds = Array.Empty<string>(),
                    RecommendedAction = "Check that your elements are imported, have trusted weights, and are enabled for GA.",
                    Category = "GA"
                });
            }

            // 9. IfcImportHasWarnings
            if (messages.Any(m => m.Severity == "Warning" || m.Severity == "Error"))
            {
                messages.Add(new ValidationMessage
                {
                    Code = "IfcImportHasWarnings",
                    Severity = "Info",
                    Message = "General import quality notes: Warnings or errors are present.",
                    AffectedCount = messages.Count(m => m.Severity == "Warning" || m.Severity == "Error"),
                    RecommendedAction = "Review validation warnings and resolve model data quality issues in your authoring tool.",
                    Category = "Mapping"
                });
            }

            summary.Messages = messages;

            // Set OverallStatus based on message severities
            if (messages.Any(m => m.Severity == "Error"))
            {
                summary.OverallStatus = ValidationStatus.Error;
            }
            else if (messages.Any(m => m.Severity == "Warning"))
            {
                summary.OverallStatus = ValidationStatus.Warning;
            }
            else
            {
                summary.OverallStatus = ValidationStatus.Valid;
            }

            return summary;
        }

        private static bool IsGaCandidate(PrecastElement element, Dictionary<string, PrecastElement> allElementsMap)
        {
            if (!element.IncludeInGa)
                return false;

            if (element.Role == IfcElementRole.NeedsReview ||
                element.Role == IfcElementRole.GroupingOnly ||
                element.Role == IfcElementRole.InternalAuxiliary ||
                element.Role == IfcElementRole.Excluded)
                return false;

            if (!element.WeightTons.HasValue || element.WeightTons.Value <= 0)
                return false;

            var hasExplicitTrustedWeight =
                element.WeightTrustLevel == IfcWeightTrustLevel.ExplicitQuantity ||
                element.WeightTrustLevel == IfcWeightTrustLevel.ExplicitProperty;

            if (!hasExplicitTrustedWeight)
                return false;

            // Double count risk: child excluded if parent assembly is also a GA target unit and has trusted weight
            if (!string.IsNullOrWhiteSpace(element.ParentGlobalId) && allElementsMap.TryGetValue(element.ParentGlobalId, out var parentElement))
            {
                if (parentElement.IncludeInGa &&
                    (parentElement.Role == IfcElementRole.LiftAssembly ||
                     parentElement.Role == IfcElementRole.LiftableElement))
                {
                    var parentWeightTrusted = parentElement.WeightTrustLevel == IfcWeightTrustLevel.ExplicitQuantity ||
                                              parentElement.WeightTrustLevel == IfcWeightTrustLevel.ExplicitProperty;
                    if (parentWeightTrusted)
                    {
                        return false;
                    }
                }
            }

            if (element.ElementCOG == null ||
                (Math.Abs(element.ElementCOG.X) < 0.0001 &&
                 Math.Abs(element.ElementCOG.Y) < 0.0001 &&
                 Math.Abs(element.ElementCOG.Z) < 0.0001) ||
                double.IsNaN(element.ElementCOG.X) || double.IsInfinity(element.ElementCOG.X) ||
                double.IsNaN(element.ElementCOG.Y) || double.IsInfinity(element.ElementCOG.Y) ||
                double.IsNaN(element.ElementCOG.Z) || double.IsInfinity(element.ElementCOG.Z))
            {
                return false;
            }

            return true;
        }

        private static SourceSystem? GetFirstSourceSystem(List<PrecastElement> elements, List<string> ids)
        {
            if (ids == null || ids.Count == 0)
                return null;

            var firstId = ids[0];
            var match = elements.FirstOrDefault(e => e.ElementID == firstId);
            return match?.SourceSystem;
        }
    }
}

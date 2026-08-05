using System;
using System.Collections.Generic;
using System.Linq;
using CraneOptimizer.Domain.Common;
using CraneOptimizer.Domain.Diagnostics;
using CraneOptimizer.Domain.Schemas;

namespace CraneOptimizer.Domain.Services
{
    public static class AssemblyRollupService
    {
        /// <summary>
        /// Reconstructs the assembly graph classifications and metadata in memory after project load.
        /// This avoids database migrations (Option C) and operates without needing the raw IFC file.
        /// </summary>
        public static void ReconstructAssemblyClassifications(List<PrecastElement> elements)
        {
            if (elements == null || elements.Count == 0) return;

            // 0. Classify all elements initially
            foreach (var element in elements)
            {
                var childClass = element.RawIfcType?.ToUpperInvariant() ?? string.Empty;
                var childName = element.ElementRevitId?.ToUpperInvariant() ?? element.ElementName?.ToUpperInvariant() ?? string.Empty;

                if (childClass == "IFCGRID" || childClass == "IFCANNOTATION" || childClass == "IFCOPENINGELEMENT" || childClass == "IFCVIRTUALELEMENT" || childClass == "IFCSPACE" ||
                    childName.Contains("OPENING") || childName.Contains("VOID") || childName.Contains("GRID") || childName.Contains("LINE"))
                {
                    element.ChildClassification = AssemblyChildClassification.IgnoredReference;
                }
                else if (childClass.Contains("REINFORCING") || childClass.Contains("TENDON") || childClass.Contains("TENDONANCHOR") ||
                         childName.Contains("REBAR") || childName.Contains("REINFORCING") || childName.Contains("STIRRUP") || childName.Contains("TIE-WIRE"))
                {
                    element.ChildClassification = AssemblyChildClassification.HiddenWeightContributor;
                }
                else if (childClass.Contains("PLATE") || childClass.Contains("FASTENER") || childClass.Contains("MECHANICALFASTENER") ||
                         childClass.Contains("ANCHOR") || childClass.Contains("BOLT") || childClass.Contains("NUT") || childClass.Contains("WASHERS") ||
                         childClass.Contains("WELD") ||
                         childName.Contains("PLATE") || childName.Contains("ANCHOR") || childName.Contains("EMBED") || childName.Contains("INSERT") ||
                         childName.Contains("BOLT") || childName.Contains("CONNECTION") || childName.Contains("PAINT") || childName.Contains("GROUT"))
                {
                    element.ChildClassification = AssemblyChildClassification.AccessoryWeightContributor;
                }
                else
                {
                    element.ChildClassification = AssemblyChildClassification.VisibleSecondaryPart;
                }
            }

            // 1. Locate all parent assembly elements
            var parents = elements
                .Where(e => e.Role == IfcElementRole.LiftAssembly || e.Role == IfcElementRole.GroupingOnly || (e.ChildGlobalIds != null && e.ChildGlobalIds.Count > 0))
                .ToList();

            var elementsMap = elements
                .Where(e => !string.IsNullOrEmpty(e.ElementID))
                .ToDictionary(e => e.ElementID, e => e, StringComparer.Ordinal);

            foreach (var parent in parents)
            {
                // Find all child elements associated with this parent
                var children = elements
                    .Where(e => string.Equals(e.ParentGlobalId, parent.ElementID, StringComparison.Ordinal))
                    .ToList();

                // If parent has a ChildGlobalIds list populated in DB, resolve missing children
                if (parent.ChildGlobalIds != null)
                {
                    foreach (var cid in parent.ChildGlobalIds)
                    {
                        if (elementsMap.TryGetValue(cid, out var child) && !children.Contains(child))
                        {
                            children.Add(child);
                        }
                    }
                }

                if (children.Count == 0) continue;

                // Sync the parent's ChildGlobalIds list
                parent.ChildGlobalIds = children.Select(c => c.ElementID).Distinct().ToList();

                // Classify child elements counts
                int visibleSecCount = 0;
                int hiddenCount = 0;
                int accessoryCount = 0;
                int ignoredCount = 0;
                var structuralCandidates = new List<PrecastElement>();

                foreach (var child in children)
                {
                    if (child.ChildClassification == AssemblyChildClassification.IgnoredReference)
                    {
                        ignoredCount++;
                    }
                    else if (child.ChildClassification == AssemblyChildClassification.HiddenWeightContributor)
                    {
                        hiddenCount++;
                    }
                    else if (child.ChildClassification == AssemblyChildClassification.AccessoryWeightContributor)
                    {
                        accessoryCount++;
                    }
                    else
                    {
                        structuralCandidates.Add(child);
                    }
                }

                // Infer Main Part
                PrecastElement mainPart = null;
                AssemblyMainPartDecision detectionMethod = AssemblyMainPartDecision.NotDetectable;

                if (structuralCandidates.Count == 1)
                {
                    mainPart = structuralCandidates[0];
                    detectionMethod = AssemblyMainPartDecision.InferredHighConfidence;
                }
                else if (structuralCandidates.Count > 1)
                {
                    // Check if explicit main part was flagged in Phase 3
                    mainPart = structuralCandidates.FirstOrDefault(c =>
                        string.Equals(c.WeightSource, "ExplicitMainPart", StringComparison.OrdinalIgnoreCase));

                    if (mainPart != null)
                    {
                        detectionMethod = AssemblyMainPartDecision.ExplicitMainPart;
                    }
                    else
                    {
                        var sorted = structuralCandidates
                            .OrderByDescending(c => c.WeightTons ?? c.ElementWT ?? 0.0)
                            .ToList();

                        var heaviest = sorted[0];
                        var runnerUp = sorted[1];

                        double heaviestW = heaviest.WeightTons ?? heaviest.ElementWT ?? 0.0;
                        double runnerUpW = runnerUp.WeightTons ?? runnerUp.ElementWT ?? 0.0;

                        if (heaviestW >= runnerUpW * 1.5 && heaviestW > 0.0)
                        {
                            mainPart = heaviest;
                            detectionMethod = AssemblyMainPartDecision.InferredHighConfidence;
                        }
                        else
                        {
                            mainPart = heaviest;
                            detectionMethod = AssemblyMainPartDecision.InferredLowConfidence;
                        }
                    }
                }

                if (mainPart != null)
                {
                    mainPart.ChildClassification = AssemblyChildClassification.MainPart;
                }

                visibleSecCount = structuralCandidates.Count(c => c != mainPart);

                // Set parent classification fields
                parent.MainPartGlobalId = mainPart?.ElementID;
                parent.MainPartName = mainPart?.ElementName;
                parent.MainPartCategory = mainPart?.ResolvedCategory;
                parent.MainPartDetectionMethod = detectionMethod.ToString();

                // Secondary safety net: if the parent's persisted ElementType is still generic
                // ("Assembly", "Unknown", or empty) but the inferred MainPartCategory is a
                // concrete physical type, promote it. This corrects assemblies whose children
                // carry proper IFC structural classes (e.g. IfcColumn, IfcBeam) even when the
                // parent container was classified generically at import time.
                // Guard: never overwrite a good category; never promote back to a generic one.
                if (IsGenericElementType(parent.ElementType) &&
                    !IsGenericElementType(parent.MainPartCategory))
                {
                    parent.ElementType = parent.MainPartCategory!;
                }
                parent.ChildCount = children.Count;
                parent.VisibleSecondaryPartCount = visibleSecCount;
                parent.HiddenWeightContributorCount = hiddenCount;
                parent.AccessoryWeightContributorCount = accessoryCount;
                parent.IgnoredReferenceCount = ignoredCount;
                parent.HasSecondaryParts = visibleSecCount > 0;
                parent.HasHiddenContributors = hiddenCount > 0;

                // Sync Warnings
                if (parent.ExtractionWarnings == null) parent.ExtractionWarnings = new List<string>();
                if (detectionMethod == AssemblyMainPartDecision.InferredLowConfidence && !parent.ExtractionWarnings.Contains("NoMainPartInferred"))
                {
                    parent.ExtractionWarnings.Add("NoMainPartInferred");
                }
                var categories = children.Select(c => c.ResolvedCategory).Where(c => !string.IsNullOrEmpty(c)).Distinct().ToList();
                if (categories.Count > 1 && !parent.ExtractionWarnings.Contains("MixedCategoryButValidAssembly"))
                {
                    parent.ExtractionWarnings.Add("MixedCategoryButValidAssembly");
                }

                // Compute rollup values for the parent
                double totalWeight = 0.0;
                double parentWeight = parent.WeightTons ?? parent.ElementWT ?? 0.0;
                bool hasParentExplicitWeight = CraneOptimizer.Domain.Policy.IfcWeightTrustPolicy.IsGaApproved(parent.WeightTrustLevel);

                var eligibleContributors = children.Where(c => c.ChildClassification != AssemblyChildClassification.IgnoredReference).ToList();
                double childWeightSum = eligibleContributors.Sum(c => c.WeightTons ?? c.ElementWT ?? 0.0);

                if (hasParentExplicitWeight && parentWeight > 0.0)
                {
                    totalWeight = parentWeight;
                }
                else
                {
                    totalWeight = childWeightSum;
                }

                parent.AssemblyLiftWeight = totalWeight > 0.0 ? totalWeight : (double?)null;

                // COG Rollup Calculation (Weighted Centroid)
                DomainPoint3D rollupCog = null;
                var cogContributors = eligibleContributors
                    .Where(c => c.PhysicalCOGAvailable && (c.WeightTons ?? c.ElementWT ?? 0.0) > 0.0 && c.ElementCOG != null)
                    .ToList();

                if (cogContributors.Count > 0)
                {
                    double weightedSumX = 0.0;
                    double weightedSumY = 0.0;
                    double weightedSumZ = 0.0;
                    double totalCogWeight = 0.0;

                    foreach (var c in eligibleContributors)
                    {
                        double w = c.WeightTons ?? c.ElementWT ?? 0.0;
                        if (w <= 0.0) continue;

                        if (c.PhysicalCOGAvailable && c.ElementCOG != null)
                        {
                            weightedSumX += c.ElementCOG.X * w;
                            weightedSumY += c.ElementCOG.Y * w;
                            weightedSumZ += c.ElementCOG.Z * w;
                            totalCogWeight += w;
                        }
                        else
                        {
                            bool isHiddenOrAccessory = c.ChildClassification == AssemblyChildClassification.HiddenWeightContributor ||
                                                       c.ChildClassification == AssemblyChildClassification.AccessoryWeightContributor;

                            double pct = w / totalWeight;
                            if (isHiddenOrAccessory && pct <= 0.02 && mainPart != null)
                            {
                                weightedSumX += mainPart.ElementCOG.X * w;
                                weightedSumY += mainPart.ElementCOG.Y * w;
                                weightedSumZ += mainPart.ElementCOG.Z * w;
                                totalCogWeight += w;
                            }
                        }
                    }

                    if (totalCogWeight > 0.0)
                    {
                        rollupCog = new DomainPoint3D(
                            weightedSumX / totalCogWeight,
                            weightedSumY / totalCogWeight,
                            weightedSumZ / totalCogWeight
                        );
                    }
                }

                if (rollupCog == null && mainPart != null)
                {
                    rollupCog = mainPart.ElementCOG;
                }

                parent.AssemblyLiftCOG = rollupCog;
                parent.AssemblyMemberIds = children.Select(c => c.ElementID).ToList();
            }
        }

        /// <summary>
        /// Builds the representative assembly lift unit for the GA input using the physical MainPartGlobalId (Option E).
        /// All sibling children inherit result statuses in WPF viewports.
        /// </summary>
        public static PrecastElement BuildAssemblyLiftUnit(
            PrecastElement parent,
            List<PrecastElement> children,
            out List<string> warnings)
        {
            if (parent == null) throw new ArgumentNullException(nameof(parent));

            warnings = new List<string>();

            if (children == null || children.Count == 0)
            {
                var fallbackLiftUnit = new PrecastElement
                {
                    ElementID = parent.MainPartGlobalId ?? parent.ElementID,
                    ElementRevitId = parent.ElementRevitId,
                    ElementMark = parent.ElementMark,
                    AssemblyMark = parent.AssemblyMark,
                    ElementName = parent.MainPartName ?? parent.ElementName,
                    ResolvedDisplayMark = parent.ElementMark ?? parent.ElementName,
                    ElementType = parent.MainPartCategory ?? "Wall",
                    WeightTons = parent.AssemblyLiftWeight ?? parent.WeightTons ?? 0.0,
                    ElementWT = parent.AssemblyLiftWeight ?? parent.ElementWT ?? 0.0,
                    ElementCOG = parent.AssemblyLiftCOG ?? parent.ElementCOG ?? new DomainPoint3D(0, 0, 0),
                    ParentGlobalId = parent.ElementID,
                    Role = IfcElementRole.LiftAssembly,
                    IncludeInGa = true,
                    WeightTrustLevel = parent.WeightTrustLevel,
                    WeightSource = parent.WeightSource ?? "ParentExplicitWeight",
                    AssemblyLiftWeight = parent.AssemblyLiftWeight,
                    AssemblyLiftCOG = parent.AssemblyLiftCOG,
                    AssemblyMemberIds = parent.AssemblyMemberIds ?? new List<string> { parent.MainPartGlobalId ?? parent.ElementID }
                };
                return fallbackLiftUnit;
            }

            // 1. Resolve Main Part
            var mainPart = children.FirstOrDefault(c => string.Equals(c.ElementID, parent.MainPartGlobalId, StringComparison.Ordinal));
            if (mainPart == null)
            {
                // Fallback to heaviest structural candidate
                mainPart = children
                    .OrderByDescending(c => c.WeightTons ?? c.ElementWT ?? 0.0)
                    .FirstOrDefault();
            }

            if (mainPart == null)
            {
                throw new InvalidOperationException($"Cannot build lift unit for assembly '{parent.ElementID}': no main part detected.");
            }

            // 2. Weight Rollup Calculation
            double totalWeight = 0.0;
            string weightSource = "";
            IfcWeightTrustLevel trustLevel = IfcWeightTrustLevel.Missing;

            double parentWeight = parent.WeightTons ?? parent.ElementWT ?? 0.0;
            bool hasParentExplicitWeight = CraneOptimizer.Domain.Policy.IfcWeightTrustPolicy.IsGaApproved(parent.WeightTrustLevel);

            // Filter contributors: MainPart, VisibleSecondaryPart, HiddenWeightContributor, AccessoryWeightContributor
            var eligibleContributors = children.Where(c => {
                var childClass = c.RawIfcType?.ToUpperInvariant() ?? string.Empty;
                var childName = c.ElementRevitId?.ToUpperInvariant() ?? c.ElementName?.ToUpperInvariant() ?? string.Empty;

                bool isIgnored = childClass == "IFCGRID" || childClass == "IFCANNOTATION" || childClass == "IFCOPENINGELEMENT" || childClass == "IFCVIRTUALELEMENT" || childClass == "IFCSPACE" ||
                                 childName.Contains("OPENING") || childName.Contains("VOID") || childName.Contains("GRID") || childName.Contains("LINE");
                return !isIgnored;
            }).ToList();

            double childWeightSum = eligibleContributors.Sum(c => c.WeightTons ?? c.ElementWT ?? 0.0);

            if (hasParentExplicitWeight && parentWeight > 0.0)
            {
                totalWeight = parentWeight;
                weightSource = parent.WeightSource ?? "ParentExplicitWeight";
                trustLevel = parent.WeightTrustLevel;

                // Validate reconciliation and flag warnings
                if (childWeightSum > 0.0)
                {
                    double diff = Math.Abs(parentWeight - childWeightSum);
                    double max1Pct = Math.Max(0.1, parentWeight * 0.01);
                    double max5Pct = Math.Max(0.5, parentWeight * 0.05);

                    if (diff > max5Pct)
                    {
                        warnings.Add($"SevereWeightMismatch: Parent explicit weight ({parentWeight:F4}t) differs from child sum ({childWeightSum:F4}t) by {diff:F4}t (>5%).");
                    }
                    else if (diff > max1Pct)
                    {
                        warnings.Add($"WeightMismatchWarning: Parent explicit weight ({parentWeight:F4}t) differs from child sum ({childWeightSum:F4}t) by {diff:F4}t (>1%).");
                    }
                }
            }
            else
            {
                totalWeight = childWeightSum;
                weightSource = "ChildSumRollup";
                trustLevel = CalculateRollupTrustLevel(eligibleContributors);
            }

            // 3. COG Rollup Calculation (Weighted Centroid)
            DomainPoint3D rollupCog = null;
            string cogSource = "";

            var cogContributors = eligibleContributors
                .Where(c => c.PhysicalCOGAvailable && (c.WeightTons ?? c.ElementWT ?? 0.0) > 0.0 && c.ElementCOG != null)
                .ToList();

            if (cogContributors.Count > 0)
            {
                double weightedSumX = 0.0;
                double weightedSumY = 0.0;
                double weightedSumZ = 0.0;
                double totalCogWeight = 0.0;

                foreach (var c in eligibleContributors)
                {
                    double w = c.WeightTons ?? c.ElementWT ?? 0.0;
                    if (w <= 0.0) continue;

                    if (c.PhysicalCOGAvailable && c.ElementCOG != null)
                    {
                        weightedSumX += c.ElementCOG.X * w;
                        weightedSumY += c.ElementCOG.Y * w;
                        weightedSumZ += c.ElementCOG.Z * w;
                        totalCogWeight += w;
                    }
                    else
                    {
                        // Check if Hidden or Accessory with weight <= 2% of assembly total
                        bool isHiddenOrAccessory = c.RawIfcType?.ToUpperInvariant().Contains("REINFORCING") == true ||
                                                   c.RawIfcType?.ToUpperInvariant().Contains("PLATE") == true ||
                                                   c.RawIfcType?.ToUpperInvariant().Contains("FASTENER") == true ||
                                                   c.ElementRevitId?.ToUpperInvariant().Contains("REBAR") == true;

                        double pct = w / totalWeight;
                        if (isHiddenOrAccessory && pct <= 0.02)
                        {
                            // Fallback to main part COG with warning
                            weightedSumX += mainPart.ElementCOG.X * w;
                            weightedSumY += mainPart.ElementCOG.Y * w;
                            weightedSumZ += mainPart.ElementCOG.Z * w;
                            totalCogWeight += w;
                            warnings.Add($"HiddenAccessoryMissingCogFallback: Child '{c.ElementID}' lacks physical COG but is minor ({pct:P2}). Fell back to main part COG.");
                        }
                        else
                        {
                            warnings.Add($"UnsafeCogRollup: Child '{c.ElementID}' has missing COG (weight={w:F4}t, {pct:P2}) and exceeds 2% threshold.");
                        }
                    }
                }

                if (totalCogWeight > 0.0)
                {
                    rollupCog = new DomainPoint3D(
                        weightedSumX / totalCogWeight,
                        weightedSumY / totalCogWeight,
                        weightedSumZ / totalCogWeight
                    );
                    cogSource = "ChildWeightedSum";
                }
            }

            if (rollupCog == null)
            {
                rollupCog = mainPart.ElementCOG;
                cogSource = "MainPartFallback";
                warnings.Add("NoPhysicalChildrenWithCog: Fallback to main part COG.");
            }

            // Create representative PrecastElement
            var memberIds = children.Select(c => c.ElementID).ToList();

            var liftUnit = new PrecastElement
            {
                ElementID = mainPart.ElementID, // GA LiftUnitId = MainPartGlobalId
                ElementRevitId = mainPart.ElementRevitId,
                ElementMark = parent.ElementMark ?? mainPart.ElementMark,
                AssemblyMark = parent.AssemblyMark ?? mainPart.AssemblyMark,
                ElementName = mainPart.ElementName,
                ResolvedDisplayMark = parent.ElementMark ?? mainPart.ElementMark ?? mainPart.ElementName,
                ElementType = mainPart.ElementType,
                ElementMaterial = mainPart.ElementMaterial,
                ElementLength = mainPart.ElementLength,
                ElementWidth = mainPart.ElementWidth,
                ElementHeight = mainPart.ElementHeight,
                ElementVolume = parent.ElementVolume ?? children.Sum(c => c.ElementVolume ?? 0.0),
                VolumeM3 = parent.VolumeM3 ?? children.Sum(c => c.VolumeM3 ?? 0.0),

                // Separated assembly rollup properties
                ElementWT = totalWeight,
                WeightTons = totalWeight,
                WeightSource = weightSource,
                WeightTrustLevel = trustLevel,
                ElementCOG = rollupCog,
                COG_M = rollupCog,
                PhysicalCOGAvailable = true,

                // Sibling & Parent identity mappings
                ParentGlobalId = parent.ElementID,
                ParentGlobalIdSource = parent.ParentGlobalIdSource ?? "IfcDecomposition",
                ChildGlobalIds = memberIds,

                Role = IfcElementRole.LiftAssembly,
                IncludeInGa = true,
                IncludeInRender = true,
                IncludeInElementTree = true,
                NormalizationDecisionReason = $"Assembly lift unit rollup. Main Part: {mainPart.ElementID}. Weight source: {weightSource}."
            };

            foreach (var warning in warnings)
            {
                if (liftUnit.ExtractionWarnings == null) liftUnit.ExtractionWarnings = new List<string>();
                if (!liftUnit.ExtractionWarnings.Contains(warning))
                {
                    liftUnit.ExtractionWarnings.Add(warning);
                }
            }

            return liftUnit;
        }

        /// <summary>
        /// Returns true when <paramref name="category"/> is a generic, non-informative
        /// placeholder ("Assembly", "Unknown", null, or empty).
        /// Must stay consistent with the same helper in IfcNormalizationDecisionService.
        /// </summary>
        private static bool IsGenericElementType(string? category)
            => string.IsNullOrWhiteSpace(category)
               || string.Equals(category, "Assembly", StringComparison.OrdinalIgnoreCase)
               || string.Equals(category, "Unknown", StringComparison.OrdinalIgnoreCase);

        private static IfcWeightTrustLevel CalculateRollupTrustLevel(IReadOnlyCollection<PrecastElement> contributors)
        {
            if (contributors == null || contributors.Count == 0)
                return IfcWeightTrustLevel.Missing;

            bool allApproved = true;
            bool hasCalculatedApproved = false;
            bool hasExplicitQuantity = false;

            foreach (var c in contributors)
            {
                double weight = c.WeightTons ?? c.ElementWT ?? 0.0;
                if (weight <= 0.0 || !CraneOptimizer.Domain.Policy.IfcWeightTrustPolicy.IsGaApproved(c.WeightTrustLevel))
                {
                    allApproved = false;
                    break;
                }

                if (c.WeightTrustLevel == IfcWeightTrustLevel.ValidatedCalculated_ApprovedForGA)
                {
                    hasCalculatedApproved = true;
                }
                else if (c.WeightTrustLevel == IfcWeightTrustLevel.ExplicitQuantity)
                {
                    hasExplicitQuantity = true;
                }
            }

            if (!allApproved)
                return IfcWeightTrustLevel.EstimatedFallback_NotAllowedForGA;

            if (hasCalculatedApproved)
                return IfcWeightTrustLevel.ValidatedCalculated_ApprovedForGA;

            return hasExplicitQuantity ? IfcWeightTrustLevel.ExplicitQuantity : IfcWeightTrustLevel.ExplicitProperty;
        }
    }
}

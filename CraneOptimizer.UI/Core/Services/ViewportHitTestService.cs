using System.Windows;
using System.Windows.Media.Media3D;
using HelixToolkit.Wpf;
using CraneOptimizer.UI.Core.Interfaces;
using CraneOptimizer.UI.Core.Models;
using System.Linq;
using System;
using CraneOptimizer.UI.Features.F2_IfcViewer;
using CraneOptimizer.Domain.Schemas;
using System.Windows.Media;

namespace CraneOptimizer.UI.Core.Services
{
    public class ViewportHitTestService : IViewportHitTestService
    {
        public bool TryGetWorldPoint(HelixViewport3D viewport, Point screenPt, out Point3D worldPoint)
        {
            worldPoint = new Point3D();

            try
            {
                var innerPt = ToInnerViewportPoint(viewport, screenPt);
                bool ok = Viewport3DHelper.Point2DtoPoint3D(viewport.Viewport, innerPt, out Point3D near, out Point3D far);
                if (!ok) return false;

                var dir = far - near;
                if (Math.Abs(dir.Z) <= 1e-9)
                    return false;

                double t = -near.Z / dir.Z;
                if (t <= 0)
                    return false;

                worldPoint = near + t * dir;
                return true;
            }
            catch { }

            try
            {
                var hit = viewport.FindNearestPoint(ToInnerViewportPoint(viewport, screenPt));
                if (hit.HasValue)
                {
                    worldPoint = hit.Value;
                    return true;
                }
            }
            catch { }

            return false;
        }

        public bool TryGetPlacementPoint(HelixViewport3D viewport, Point screenPt, out Point3D worldPoint)
        {
            return TryGetWorldPoint(viewport, screenPt, out worldPoint);
        }

        public bool TryGetIfcHitPoint(HelixViewport3D viewport, Point screenPt, out Point3D worldPoint)
        {
            worldPoint = default;

            try
            {
                var innerPoint = ToInnerViewportPoint(viewport, screenPt);
                foreach (PointHitResult hit in viewport.Viewport.FindHits(innerPoint))
                {
                    if (hit.Visual == null ||
                        hit.Mesh == null ||
                        hit.RayHit is not RayMeshGeometry3DHitTestResult ||
                        !IsIfcVisual(hit.Visual) ||
                        !IsFinite(hit.Position))
                    {
                        continue;
                    }

                    worldPoint = hit.Position;
                    return true;
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine(
                    $"[ViewportHitTestService] IFC point hit test failed: {ex.Message}");
            }

            return false;
        }

        private static bool IsIfcVisual(Visual3D visual)
        {
            DependencyObject? current = visual;
            while (current != null)
            {
                object? tag = current.GetValue(FrameworkElement.TagProperty);
                if (tag is PrecastElement)
                    return true;

                if (tag is string || tag is SceneCrane || tag is GreenAreaZone)
                    return false;

                current = VisualTreeHelper.GetParent(current);
            }

            return false;
        }

        private static bool IsFinite(Point3D point) =>
            double.IsFinite(point.X) &&
            double.IsFinite(point.Y) &&
            double.IsFinite(point.Z);

        public bool TryGetHitCrane(HelixViewport3D viewport, Point screenPt, out SceneCrane crane)
        {
            crane = null!;

            try
            {
                var hits = viewport.Viewport.FindHits(screenPt);
                foreach (var h in hits)
                {
                    if (h.Visual is ModelVisual3D mv)
                    {
                        var tag = mv.GetValue(FrameworkElement.TagProperty);
                        if (tag is SceneCrane sc)
                        {
                            crane = sc;
                            return true;
                        }
                    }

                    if (h.Visual is Visual3D v3)
                    {
                        var tag = v3.GetValue(FrameworkElement.TagProperty);
                        if (tag is SceneCrane sc2)
                        {
                            crane = sc2;
                            return true;
                        }
                    }
                }
            }
            catch { }

            return false;
        }

        public static int SpatialHitTestCandidateCount { get; set; }
        public static double SpatialHitTestDurationMs { get; set; }
        public static string? PickedElementId { get; set; }
        public static bool UsedSpatialHitTesting { get; set; }
        public static bool FellBackToNativeHitTesting { get; set; }
        public static int HitTestRegistryCount { get; set; }

        public static int TotalHitTestCount { get; set; }
        public static double TotalHitTestDurationMs { get; set; }
        public static double MaxHitTestDurationMs { get; set; }

        // Narrow-phase configuration and timing diagnostics
        public static int MaxNarrowPhaseCandidates { get; set; } = 50;
        public static double SafeNarrowPhaseThresholdMs { get; set; } = 50.0;
        public static double NarrowPhaseDurationMs { get; set; }
        public static double BroadPhaseDurationMs { get; set; }
        public static int NarrowPhaseTestsCount { get; set; }
        public static bool NarrowPhaseTimedOut { get; set; }

        // Phase 2.4.2 diagnostics: mesh-derived AABB tracking
        public static int BroadPhaseCandidateCount { get; set; }
        public static int MeshDerivedAABBCount { get; set; }
        public static int CogFallbackAABBCount { get; set; }
        public static bool PickedUsedMeshDerivedBounds { get; set; }

        public static void ResetDiagnostics()
        {
            SpatialHitTestCandidateCount = 0;
            SpatialHitTestDurationMs = 0;
            PickedElementId = null;
            UsedSpatialHitTesting = false;
            FellBackToNativeHitTesting = false;
            HitTestRegistryCount = 0;

            TotalHitTestCount = 0;
            TotalHitTestDurationMs = 0;
            MaxHitTestDurationMs = 0;

            NarrowPhaseDurationMs = 0;
            BroadPhaseDurationMs = 0;
            NarrowPhaseTestsCount = 0;
            NarrowPhaseTimedOut = false;

            BroadPhaseCandidateCount = 0;
            MeshDerivedAABBCount = 0;
            CogFallbackAABBCount = 0;
            PickedUsedMeshDerivedBounds = false;
        }

        private static bool RayIntersectsAABB(Point3D rayOrigin, Vector3D rayDir, Point3D min, Point3D max, out double tNear)
        {
            tNear = 0;
            double tMin = double.NegativeInfinity;
            double tMax = double.PositiveInfinity;

            // X axis
            if (Math.Abs(rayDir.X) > 1e-9)
            {
                double t1 = (min.X - rayOrigin.X) / rayDir.X;
                double t2 = (max.X - rayOrigin.X) / rayDir.X;
                tMin = Math.Max(tMin, Math.Min(t1, t2));
                tMax = Math.Min(tMax, Math.Max(t1, t2));
            }
            else if (rayOrigin.X < min.X || rayOrigin.X > max.X)
            {
                return false;
            }

            // Y axis
            if (Math.Abs(rayDir.Y) > 1e-9)
            {
                double t1 = (min.Y - rayOrigin.Y) / rayDir.Y;
                double t2 = (max.Y - rayOrigin.Y) / rayDir.Y;
                tMin = Math.Max(tMin, Math.Min(t1, t2));
                tMax = Math.Min(tMax, Math.Max(t1, t2));
            }
            else if (rayOrigin.Y < min.Y || rayOrigin.Y > max.Y)
            {
                return false;
            }

            // Z axis
            if (Math.Abs(rayDir.Z) > 1e-9)
            {
                double t1 = (min.Z - rayOrigin.Z) / rayDir.Z;
                double t2 = (max.Z - rayOrigin.Z) / rayDir.Z;
                tMin = Math.Max(tMin, Math.Min(t1, t2));
                tMax = Math.Min(tMax, Math.Max(t1, t2));
            }
            else if (rayOrigin.Z < min.Z || rayOrigin.Z > max.Z)
            {
                return false;
            }

            if (tMax >= tMin && tMax >= 0)
            {
                tNear = tMin < 0 ? 0 : tMin;
                return true;
            }

            return false;
        }

        private static bool RayTriangleIntersect(
            Point3D rayOrigin, Vector3D rayDir,
            Point3D v0, Point3D v1, Point3D v2,
            out double t)
        {
            t = 0;
            const double EPSILON = 1e-8;

            Vector3D edge1 = v1 - v0;
            Vector3D edge2 = v2 - v0;
            Vector3D h = Vector3D.CrossProduct(rayDir, edge2);
            double a = Vector3D.DotProduct(edge1, h);

            if (a > -EPSILON && a < EPSILON)
                return false; // Ray is parallel to this triangle.

            double f = 1.0 / a;
            Vector3D s = rayOrigin - v0;
            double u = f * Vector3D.DotProduct(s, h);

            if (u < 0.0 || u > 1.0)
                return false;

            Vector3D q = Vector3D.CrossProduct(s, edge1);
            double v = f * Vector3D.DotProduct(rayDir, q);

            if (v < 0.0 || u + v > 1.0)
                return false;

            // At this stage we can compute t to find out where the intersection point is on the line.
            double tTemp = f * Vector3D.DotProduct(edge2, q);

            if (tTemp > EPSILON) // ray intersection
            {
                t = tTemp;
                return true;
            }

            return false;
        }

        private static double ComputeFallbackScore(Point3D rayOrigin, Vector3D rayDir, double tNear, ElementVisualBuilder.SpatialHitTestElement candidate)
        {
            var el = candidate.Element;
            var center = new Point3D(
                (candidate.Min.X + candidate.Max.X) / 2.0,
                (candidate.Min.Y + candidate.Max.Y) / 2.0,
                (candidate.Min.Z + candidate.Max.Z) / 2.0
            );

            var toCenter = center - rayOrigin;
            var projectedLength = Vector3D.DotProduct(toCenter, rayDir);

            double rayToCenterDistance;
            if (projectedLength < 0)
            {
                rayToCenterDistance = toCenter.Length;
            }
            else
            {
                var projectedPoint = rayOrigin + projectedLength * rayDir;
                rayToCenterDistance = (projectedPoint - center).Length;
            }

            var volume = (el.BBoxLength ?? 1.0) * (el.BBoxWidth ?? 1.0) * (el.BBoxHeight ?? 1.0);

            // Lower score is better. Volume penalty increased in Phase 2.4.2 from 0.001 to 0.05
            // to prevent large slabs/walls from dominating smaller clicked elements.
            double score = tNear * 1.0 
                           + rayToCenterDistance * 2.0 
                           + Math.Max(0, projectedLength) * 0.1
                           + volume * 0.05;

            return score;
        }

        private static Point ToInnerViewportPoint(HelixViewport3D viewport, Point helixPoint)
        {
            try
            {
                return viewport.TranslatePoint(helixPoint, viewport.Viewport);
            }
            catch
            {
                return helixPoint;
            }
        }

        public object? GetHitObject(HelixViewport3D viewport, Point screenPt)
        {
            var totalSw = System.Diagnostics.Stopwatch.StartNew();
            try
            {
                ResetDiagnostics();
                HitTestRegistryCount = ElementVisualBuilder.SpatialHitTestElementCount;
                var innerPt = ToInnerViewportPoint(viewport, screenPt);

                if (ElementVisualBuilder.UseSpatialHitTestingInsteadOfTransparentBoxes)
                {
                    UsedSpatialHitTesting = true;
                    var sw = System.Diagnostics.Stopwatch.StartNew();
                    var broadSw = System.Diagnostics.Stopwatch.StartNew();
                    try
                    {
                        bool ok = Viewport3DHelper.Point2DtoPoint3D(viewport.Viewport, innerPt, out Point3D near, out Point3D far);
                        if (ok)
                        {
                            var dir = far - near;
                            if (dir.LengthSquared > 1e-18)
                            {
                                dir.Normalize();
                                var candidates = ElementVisualBuilder.GetSpatialHitTestElements();
                                SpatialHitTestCandidateCount = candidates.Count;
                                MeshDerivedAABBCount = candidates.Count(c => c.UsedMeshDerivedBounds);
                                CogFallbackAABBCount = candidates.Count(c => !c.UsedMeshDerivedBounds);

                                // Broad-phase: AABB raycasts
                                var intersectedCandidates = new List<(ElementVisualBuilder.SpatialHitTestElement Candidate, double TNear)>();
                                foreach (var candidate in candidates)
                                {
                                    if (!candidate.IsVisible) continue;
                                    if (candidate.Element == null) continue;

                                    if (RayIntersectsAABB(near, dir, candidate.Min, candidate.Max, out double tNear))
                                    {
                                        intersectedCandidates.Add((candidate, tNear));
                                    }
                                }

                                broadSw.Stop();
                                BroadPhaseDurationMs = broadSw.Elapsed.TotalMilliseconds;
                                BroadPhaseCandidateCount = intersectedCandidates.Count;

                                System.Diagnostics.Debug.WriteLine($"[ViewportHitTestService] Broad-phase: Selected {intersectedCandidates.Count} intersected candidates out of {candidates.Count} total. MeshDerived count: {MeshDerivedAABBCount}, CogFallback count: {CogFallbackAABBCount}.");

                                if (intersectedCandidates.Count > 0)
                                {
                                    // Sort candidates by tNear ascending
                                    var sortedCandidates = intersectedCandidates
                                        .OrderBy(c => c.TNear)
                                        .ToList();

                                    // Select up to MaxNarrowPhaseCandidates
                                    var narrowCandidates = sortedCandidates
                                        .Take(MaxNarrowPhaseCandidates)
                                        .ToList();

                                    var narrowSw = System.Diagnostics.Stopwatch.StartNew();
                                    PrecastElement? bestTriangleElement = null;
                                    double bestTriangleDist = double.MaxValue;

                                    bool hasMeshCandidates = narrowCandidates.Any(c => c.Candidate.Meshes != null && c.Candidate.Meshes.Count > 0);

                                    if (hasMeshCandidates)
                                    {
                                        foreach (var c in narrowCandidates)
                                        {
                                            var candidate = c.Candidate;
                                            if (candidate.Meshes == null || candidate.Meshes.Count == 0)
                                                continue;

                                            foreach (var m in candidate.Meshes)
                                            {
                                                var mesh = m.Mesh;
                                                var transform = m.Transform;
                                                var positions = mesh.Positions;
                                                var indices = mesh.TriangleIndices;

                                                for (int i = 0; i + 2 < indices.Count; i += 3)
                                                {
                                                    var p0 = transform.Transform(positions[indices[i]]);
                                                    var p1 = transform.Transform(positions[indices[i + 1]]);
                                                    var p2 = transform.Transform(positions[indices[i + 2]]);

                                                    NarrowPhaseTestsCount++;

                                                    if (RayTriangleIntersect(near, dir, p0, p1, p2, out double tTriangle))
                                                    {
                                                        if (tTriangle < bestTriangleDist)
                                                        {
                                                            bestTriangleDist = tTriangle;
                                                            bestTriangleElement = candidate.Element;
                                                        }
                                                    }
                                                }
                                            }

                                            // Safety check
                                            if (narrowSw.Elapsed.TotalMilliseconds > SafeNarrowPhaseThresholdMs)
                                            {
                                                NarrowPhaseTimedOut = true;
                                                System.Diagnostics.Debug.WriteLine($"[ViewportHitTestService] Warning: Narrow phase refinement exceeded {SafeNarrowPhaseThresholdMs}ms threshold. Graceful fallback triggered.");
                                                break;
                                            }
                                        }

                                        narrowSw.Stop();
                                        NarrowPhaseDurationMs = narrowSw.Elapsed.TotalMilliseconds;

                                        if (bestTriangleElement != null)
                                        {
                                            var pickedCandidate = narrowCandidates.FirstOrDefault(c => c.Candidate.Element == bestTriangleElement).Candidate;
                                            PickedUsedMeshDerivedBounds = pickedCandidate?.UsedMeshDerivedBounds ?? false;
                                            sw.Stop();
                                            SpatialHitTestDurationMs = sw.Elapsed.TotalMilliseconds;
                                            PickedElementId = bestTriangleElement.ElementID;

                                            // Diagnostics log
                                            System.Diagnostics.Debug.WriteLine($"[HitTest Diagnosis] Selected ElementID: {bestTriangleElement.ElementID}");
                                            System.Diagnostics.Debug.WriteLine($"  Type: {bestTriangleElement.ElementType}");
                                            System.Diagnostics.Debug.WriteLine($"  COG: {bestTriangleElement.ElementCOG}");
                                            System.Diagnostics.Debug.WriteLine($"  COG BBox Size: {bestTriangleElement.BBoxLength} x {bestTriangleElement.BBoxWidth} x {bestTriangleElement.BBoxHeight}");
                                            if (pickedCandidate != null)
                                            {
                                                System.Diagnostics.Debug.WriteLine($"  Mesh Bounds: Min={pickedCandidate.Min}, Max={pickedCandidate.Max}");
                                            }
                                            System.Diagnostics.Debug.WriteLine($"  Reason: Ray-Triangle intersection in narrow phase (distance={bestTriangleDist:F4})");

                                            return bestTriangleElement;
                                        }
                                        else
                                        {
                                            // If meshes are available but no triangle is hit, then it's an empty-space click!
                                            // Return null immediately and do not perform fallback AABB scoring.
                                            sw.Stop();
                                            SpatialHitTestDurationMs = sw.Elapsed.TotalMilliseconds;
                                            return null;
                                        }
                                    }

                                    // Fallback: no triangles intersected, or mesh data unavailable
                                    // Use fallback scoring to choose the best candidate
                                    double bestScore = double.MaxValue;
                                    PrecastElement? bestFallbackElement = null;

                                    foreach (var c in sortedCandidates)
                                    {
                                        double score = ComputeFallbackScore(near, dir, c.TNear, c.Candidate);
                                        if (score < bestScore)
                                        {
                                            bestScore = score;
                                            bestFallbackElement = c.Candidate.Element;
                                        }
                                    }

                                    if (bestFallbackElement != null)
                                    {
                                        var pickedCandidate = candidates.FirstOrDefault(c => c.Element == bestFallbackElement);
                                        PickedUsedMeshDerivedBounds = pickedCandidate?.UsedMeshDerivedBounds ?? false;
                                        sw.Stop();
                                        SpatialHitTestDurationMs = sw.Elapsed.TotalMilliseconds;
                                        PickedElementId = bestFallbackElement.ElementID;

                                        // Diagnostics log
                                        System.Diagnostics.Debug.WriteLine($"[HitTest Diagnosis] Selected ElementID (Fallback): {bestFallbackElement.ElementID}");
                                        System.Diagnostics.Debug.WriteLine($"  Type: {bestFallbackElement.ElementType}");
                                        System.Diagnostics.Debug.WriteLine($"  COG: {bestFallbackElement.ElementCOG}");
                                        System.Diagnostics.Debug.WriteLine($"  COG BBox Size: {bestFallbackElement.BBoxLength} x {bestFallbackElement.BBoxWidth} x {bestFallbackElement.BBoxHeight}");
                                        if (pickedCandidate != null)
                                        {
                                            System.Diagnostics.Debug.WriteLine($"  AABB Bounds: Min={pickedCandidate.Min}, Max={pickedCandidate.Max}");
                                        }
                                        System.Diagnostics.Debug.WriteLine($"  Reason: Fallback AABB proximity score (score={bestScore:F4})");

                                        return bestFallbackElement;
                                    }
                                }
                            }
                        }
                    }
                    catch (Exception ex)
                    {
                        System.Diagnostics.Debug.WriteLine($"[ViewportHitTestService] Error in spatial hit testing: {ex.Message}");
                    }
                    sw.Stop();
                    SpatialHitTestDurationMs = sw.Elapsed.TotalMilliseconds;
                }

                // Fallback to legacy native hit-testing (same coordinate space as SelectionHandler.FindHits)
                FellBackToNativeHitTesting = true;
                try
                {
                    var hits = viewport.Viewport.FindHits(innerPt);
                    if (hits != null && hits.Count > 0)
                    {
                        foreach (var hit in hits)
                        {
                            if (hit.Visual is not ModelVisual3D mv)
                                continue;

                            var tag = mv.GetValue(FrameworkElement.TagProperty);
                            if (tag != null && tag is not string)
                            {
                                if (tag is PrecastElement el)
                                {
                                    PickedElementId = el.ElementID;
                                }
                                return tag;
                            }
                        }
                    }
                }
                catch { }

                return null;
            }
            finally
            {
                totalSw.Stop();
                var duration = totalSw.Elapsed.TotalMilliseconds;
                TotalHitTestCount++;
                TotalHitTestDurationMs += duration;
                MaxHitTestDurationMs = Math.Max(MaxHitTestDurationMs, duration);
            }
        }
    }
}

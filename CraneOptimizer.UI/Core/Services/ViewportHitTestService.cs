using System.Windows;
using System.Windows.Media.Media3D;
using HelixToolkit.Wpf;
using CraneOptimizer.UI.Core.Interfaces;
using CraneOptimizer.UI.Core.Models;
using System.Linq;
using System;
using CraneOptimizer.UI.Features.F2_IfcViewer;
using CraneOptimizer.Domain.Schemas;

namespace CraneOptimizer.UI.Core.Services
{
    public class ViewportHitTestService : IViewportHitTestService
    {
        public bool TryGetWorldPoint(HelixViewport3D viewport, Point screenPt, out Point3D worldPoint)
        {
            worldPoint = new Point3D();

            try
            {
                bool ok = Viewport3DHelper.Point2DtoPoint3D(viewport.Viewport, screenPt, out Point3D near, out Point3D far);
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
                var hit = viewport.FindNearestPoint(screenPt);
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

        // Narrow-phase configuration and timing diagnostics
        public static int MaxNarrowPhaseCandidates { get; set; } = 50;
        public static double SafeNarrowPhaseThresholdMs { get; set; } = 50.0;
        public static double NarrowPhaseDurationMs { get; set; }
        public static double BroadPhaseDurationMs { get; set; }
        public static int NarrowPhaseTestsCount { get; set; }
        public static bool NarrowPhaseTimedOut { get; set; }

        public static void ResetDiagnostics()
        {
            SpatialHitTestCandidateCount = 0;
            SpatialHitTestDurationMs = 0;
            PickedElementId = null;
            UsedSpatialHitTesting = false;
            FellBackToNativeHitTesting = false;
            HitTestRegistryCount = 0;

            NarrowPhaseDurationMs = 0;
            BroadPhaseDurationMs = 0;
            NarrowPhaseTestsCount = 0;
            NarrowPhaseTimedOut = false;
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

            // Lower score is better.
            double score = tNear * 1.0 
                           + rayToCenterDistance * 2.0 
                           + Math.Max(0, projectedLength) * 0.1
                           + volume * 0.001;

            return score;
        }

        public object? GetHitObject(HelixViewport3D viewport, Point screenPt)
        {
            ResetDiagnostics();
            HitTestRegistryCount = ElementVisualBuilder.SpatialHitTestElementCount;

            if (ElementVisualBuilder.UseSpatialHitTestingInsteadOfTransparentBoxes)
            {
                UsedSpatialHitTesting = true;
                var sw = System.Diagnostics.Stopwatch.StartNew();
                var broadSw = System.Diagnostics.Stopwatch.StartNew();
                try
                {
                    bool ok = Viewport3DHelper.Point2DtoPoint3D(viewport.Viewport, screenPt, out Point3D near, out Point3D far);
                    if (ok)
                    {
                        var dir = far - near;
                        if (dir.LengthSquared > 1e-18)
                        {
                            dir.Normalize();
                            var candidates = ElementVisualBuilder.GetSpatialHitTestElements();
                            SpatialHitTestCandidateCount = candidates.Count;

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
                                        sw.Stop();
                                        SpatialHitTestDurationMs = sw.Elapsed.TotalMilliseconds;
                                        PickedElementId = bestTriangleElement.ElementID;
                                        return bestTriangleElement;
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
                                    sw.Stop();
                                    SpatialHitTestDurationMs = sw.Elapsed.TotalMilliseconds;
                                    PickedElementId = bestFallbackElement.ElementID;
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

            // Fallback to legacy native hit-testing
            FellBackToNativeHitTesting = true;
            try
            {
                var hits = viewport.Viewport.FindHits(screenPt);
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
    }
}

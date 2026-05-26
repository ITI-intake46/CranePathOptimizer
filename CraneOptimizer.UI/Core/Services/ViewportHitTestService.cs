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

        public static void ResetDiagnostics()
        {
            SpatialHitTestCandidateCount = 0;
            SpatialHitTestDurationMs = 0;
            PickedElementId = null;
            UsedSpatialHitTesting = false;
            FellBackToNativeHitTesting = false;
            HitTestRegistryCount = 0;
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

        public object? GetHitObject(HelixViewport3D viewport, Point screenPt)
        {
            ResetDiagnostics();
            HitTestRegistryCount = ElementVisualBuilder.SpatialHitTestElementCount;

            if (ElementVisualBuilder.UseSpatialHitTestingInsteadOfTransparentBoxes)
            {
                UsedSpatialHitTesting = true;
                var sw = System.Diagnostics.Stopwatch.StartNew();
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

                            double closestDist = double.MaxValue;
                            PrecastElement? closestElement = null;

                            foreach (var candidate in candidates)
                            {
                                // Skip hidden IncludeInRender=false elements
                                if (!candidate.IsVisible) continue;
                                if (candidate.Element == null) continue;

                                if (RayIntersectsAABB(near, dir, candidate.Min, candidate.Max, out double t))
                                {
                                    if (t < closestDist)
                                    {
                                        closestDist = t;
                                        closestElement = candidate.Element;
                                    }
                                }
                            }

                            if (closestElement != null)
                            {
                                sw.Stop();
                                SpatialHitTestDurationMs = sw.Elapsed.TotalMilliseconds;
                                PickedElementId = closestElement.ElementID;
                                return closestElement;
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

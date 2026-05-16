using System.Windows;
using System.Windows.Media.Media3D;
using HelixToolkit.Wpf;
using CraneOptimizer.UI.Core.Interfaces;
using CraneOptimizer.UI.Core.Models;
using System.Linq;
using System;

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

        public object? GetHitObject(HelixViewport3D viewport, Point screenPt)
        {
            try
            {
                var hits = viewport.Viewport.FindHits(screenPt);
                if (hits == null || hits.Count == 0)
                    return null;

                foreach (var hit in hits)
                {
                    if (hit.Visual is not ModelVisual3D mv)
                        continue;

                    var tag = mv.GetValue(FrameworkElement.TagProperty);
                    if (tag != null && tag is not string)
                    {
                        return tag;
                    }
                }
            }
            catch { }

            return null;
        }
    }
}

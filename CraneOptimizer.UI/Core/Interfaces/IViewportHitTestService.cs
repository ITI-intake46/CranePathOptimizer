using System.Windows;
using System.Windows.Media.Media3D;
using HelixToolkit.Wpf;
using CraneOptimizer.UI.Core.Models;

namespace CraneOptimizer.UI.Core.Interfaces
{
    public interface IViewportHitTestService
    {
        bool TryGetWorldPoint(HelixViewport3D viewport, Point screenPt, out Point3D worldPoint);
        bool TryGetPlacementPoint(HelixViewport3D viewport, Point screenPt, out Point3D worldPoint);
        bool TryGetIfcHitPoint(HelixViewport3D viewport, Point screenPt, out Point3D worldPoint)
        {
            worldPoint = default;
            return false;
        }
        bool TryGetHitCrane(HelixViewport3D viewport, Point screenPt, out SceneCrane crane);
        object? GetHitObject(HelixViewport3D viewport, Point screenPt);
    }
}

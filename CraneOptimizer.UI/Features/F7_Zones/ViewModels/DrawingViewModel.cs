using CraneOptimizer.UI.Core.Interfaces;
using CraneOptimizer.UI.Core.Scene;
using CraneOptimizer.UI.Core.Services;
using System.Security.Policy;
using DomainPoint = CraneOptimizer.Domain.Common.DomainPoint3D;
using WpfPoint = System.Windows.Media.Media3D.Point3D;


namespace CraneOptimizer.UI.Features.F7_Zones.ViewModels
{
    /// <summary>
    /// Controls drawing workflow.
    /// Does NOT render directly.
    /// </summary>
    public class DrawingViewModel 
    {
        private readonly DrawingService _drawing;
        private readonly SceneViewModel _scene;
        private readonly IRenderingService _render;
        public DrawingViewModel(
                                DrawingService drawing,
                                SceneViewModel scene,
                                IRenderingService render)
        {
            _drawing = drawing;
            _scene = scene;
            _render = render;
        }
        public bool IsDrawing { get; private set; }

        public void Start()
        {
            IsDrawing = true;
            _drawing.Start();
        }

        public void AddPoint(DomainPoint point)
        {
            if (!IsDrawing) return;

            _drawing.AddPoint(point);

            _render.UpdatePreview(_drawing.GetPoints());
        }

        public void Finish()
        {
            var zone = _drawing.BuildZone();

            if (zone != null)
                _scene.AddZone(zone);

            _render.ClearPreview();

            IsDrawing = false;
        }
    }
}
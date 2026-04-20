using CraneOptimizer.Domain.Common;
using CraneOptimizer.UI.Core.Base;
using CraneOptimizer.UI.Core.Interfaces;
using CraneOptimizer.UI.Core.Rendering;
using CraneOptimizer.UI.Core.Services;


namespace CraneOptimizer.UI.Features.F7_Zones.ViewModels
{
    /// <summary>
    /// Controls drawing workflow.
    /// Does NOT render directly.
    /// </summary>
    public class DrawingViewModel : ViewModelBase
    {
        private readonly DrawingService _drawing;
        private readonly SceneService _scene;
        private readonly IRenderingService _render;
        public DrawingViewModel(
                                DrawingService drawing,
                                SceneService scene,
                                IRenderingService render)
        {
            _drawing = drawing;
            _scene = scene;
            _render = render;
        }
        private bool _isDrawing;

        public bool IsDrawing
        {
            get => _isDrawing;
            private set
            {
                _isDrawing = value;
                OnPropertyChanged();
            }
        }

        public void Start()
        {
            IsDrawing = true;
            _drawing.Start();
        }

        public void AddPoint(DomainPoint3D point)
        {
            if (!IsDrawing) return;

            _drawing.AddPoint(point);

            _render.Render(
                new[] { _drawing.GetPoints() },
                new PreviewLinesBuilder(),
                "Preview"
            );
        }

        public void Finish()
        {
            var zone = _drawing.BuildZone();

            if (zone != null)
                _scene.AddZone(zone);

            _render.ClearLayer("Preview");

            IsDrawing = false;
        }
    }
}
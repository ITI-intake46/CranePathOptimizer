using CraneOptimizer.Domain.Common;
using CraneOptimizer.Domain.Schemas.Enums;
using CraneOptimizer.UI.Core.Base;
using CraneOptimizer.UI.Core.Commands;
using CraneOptimizer.UI.Core.Interfaces;
using CraneOptimizer.UI.Core.Services;
using System.Windows.Input;

namespace CraneOptimizer.UI.Features.F7_Zones.ViewModels
{
    public class DrawingViewModel : ViewModelBase
    {
        private readonly DrawingService _drawing;
        private readonly ISceneService _scene;
        private readonly IRenderingService _render;
        private DomainPoint3D? _currentHover;

        public DrawingViewModel(
            DrawingService drawing,
            ISceneService scene,
            IRenderingService render)
        {
            _drawing = drawing;
            _scene = scene;
            _render = render;

            StartAllowedCommand = new RelayCommand(StartAllowed);
            StartForbiddenCommand = new RelayCommand(StartForbidden);

            FinishCommand = new RelayCommand(Finish, () => IsDrawing);
            CancelCommand = new RelayCommand(Cancel, () => IsDrawing);
            UndoCommand = new RelayCommand(Undo, () => IsDrawing);
        }

        // ─────────────────────────────
        // State
        // ─────────────────────────────
        private bool _isDrawing;
        public bool IsDrawing
        {
            get => _isDrawing;
            private set
            {
                _isDrawing = value;
                OnPropertyChanged();
                CommandManager.InvalidateRequerySuggested();
            }
        }

        private ZoneType _currentType = ZoneType.Allowed;

        // ─────────────────────────────
        // Commands
        // ─────────────────────────────
        public ICommand StartAllowedCommand { get; }
        public ICommand StartForbiddenCommand { get; }
        public ICommand FinishCommand { get; }
        public ICommand CancelCommand { get; }
        public ICommand UndoCommand { get; }

        // ─────────────────────────────
        // Start
        // ─────────────────────────────
        public void Start()
        {
            _drawing.Start();
            _currentHover = null;
            _render.ClearLayer("Preview");
            IsDrawing = true;
        }

        private void StartAllowed()
        {
            _currentType = ZoneType.Allowed;
            Start();
        }

        private void StartForbidden()
        {
            _currentType = ZoneType.Forbidden;
            Start();
        }

        // ─────────────────────────────
        // Add Point
        // ─────────────────────────────
        public void AddPoint(DomainPoint3D point)
        {
            if (!IsDrawing) return;

            if (!_drawing.TryAddPoint(point))
                return;

            _currentHover = null;
            RenderPreview();
        }

        public void SetHoverPoint(DomainPoint3D point)
        {
            if (!IsDrawing) return;

            _currentHover = point;
            RenderPreview();
        }

        // ─────────────────────────────
        // Finish
        // ─────────────────────────────
        public void Finish()
        {
            if (!IsDrawing) return;

            var pts = _drawing.GetPoints();

            if (pts.Count >= 3)
            {
                var first = pts[0];
                var last = pts[^1];

                if (!ArePointsEqual(first, last))
                {
                    pts.Add(new DomainPoint3D(first.X, first.Y, first.Z));
                }
            }

            var zone = _drawing.BuildZone();

            if (zone != null)
            {
                zone.Type = _currentType;
                _scene.AddZone(zone);
            }

            _render.ClearLayer("Preview");
            _currentHover = null;
            _drawing.Start();
            IsDrawing = false;
        }

        // ─────────────────────────────
        // Cancel
        // ─────────────────────────────
        private void Cancel()
        {
            _currentHover = null;
            _drawing.Start();
            _render.ClearLayer("Preview");
            IsDrawing = false;
        }

        // ─────────────────────────────
        // Undo
        // ─────────────────────────────
        private void Undo()
        {
            var pts = _drawing.GetPoints();
            if (pts.Count == 0) return;

            pts.RemoveAt(pts.Count - 1);
            RenderPreview();
        }

        private List<DomainPoint3D> GetPreviewPoints()
        {
            var previewPoints = new List<DomainPoint3D>(_drawing.GetPoints());

            if (_currentHover != null)
                previewPoints.Add(_currentHover);

            return previewPoints;
        }

        private void RenderPreview()
        {
            var previewPoints = GetPreviewPoints();

            if (previewPoints.Count < 2)
            {
                _render.ClearLayer("Preview");
                return;
            }

            _render.Render(
                new[] { previewPoints },
                new PreviewLinesBuilder(),
                "Preview"
            );
        }

        private bool ArePointsEqual(DomainPoint3D a, DomainPoint3D b)
        {
            return System.Math.Abs(a.X - b.X) < 0.001 &&
                   System.Math.Abs(a.Y - b.Y) < 0.001 &&
                   System.Math.Abs(a.Z - b.Z) < 0.001;
        }
    }
}

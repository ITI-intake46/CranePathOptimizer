using CraneOptimizer.Domain.Common;
using CraneOptimizer.Domain.Schemas;
using CraneOptimizer.Domain.Schemas.Enums;
using CraneOptimizer.UI.Core.Base;
using CraneOptimizer.UI.Core.Commands;
using CraneOptimizer.UI.Core.Interfaces;
using CraneOptimizer.UI.Core.Services;
using CraneOptimizer.UI.Features.F7_Zones.Views;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Input;

namespace CraneOptimizer.UI.Features.F7_Zones.ViewModels
{
    public class ZoneDrawingViewModel : ViewModelBase, IDisposable
    {
        private readonly DrawingService _drawing;
        private readonly ISceneService _scene;
        private readonly IRenderingService _render;
        private DomainPoint3D? _currentHover;
        private bool _disposed;

        public event Action? DrawingEnded;
        public event Action? DrawingStarted;

        public ZoneDrawingViewModel(
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

            // Move subscriptions from Viewer to here
            _scene.ZoneAdded += OnZoneChanged;
            _scene.ZoneUpdated += OnZoneChanged;
            _scene.ZoneRemoved += OnZoneChanged;
            _scene.ZonesCleared += OnZonesCleared;
            _scene.ZoneSelected += OnZoneChanged;
        }

        // ─────────────────────────────
        // State
        // ─────────────────────────────
        private bool _isDrawing;
        public bool IsDrawing
        {
            get => _isDrawing;
            set
            {
                _isDrawing = value;
                OnPropertyChanged();
                CommandManager.InvalidateRequerySuggested();

                (StartAllowedCommand as RelayCommand)?.RaiseCanExecuteChanged();
                (StartForbiddenCommand as RelayCommand)?.RaiseCanExecuteChanged();
                (FinishCommand as RelayCommand)?.RaiseCanExecuteChanged();
                (CancelCommand as RelayCommand)?.RaiseCanExecuteChanged();
                (UndoCommand as RelayCommand)?.RaiseCanExecuteChanged();
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
        // Drawing Logic
        // ─────────────────────────────
        public void Start()
        {
            _drawing.Start();
            _currentHover = null;
            _render.ClearLayer("Preview");
            IsDrawing = true;
            DrawingStarted?.Invoke();
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
            DrawingEnded?.Invoke();
        }

        public void Cancel()
        {
            _currentHover = null;
            _drawing.Start();
            _render.ClearLayer("Preview");
            IsDrawing = false;
            DrawingEnded?.Invoke();
        }

        public void Undo()
        {
            var pts = _drawing.GetPoints();
            if (pts.Count == 0) return;

            pts.RemoveAt(pts.Count - 1);
            RenderPreview();
        }

        private void RenderPreview()
        {
            var pts = _drawing.GetPoints();
            var previewPoints = new List<DomainPoint3D>(pts);

            if (_currentHover != null)
                previewPoints.Add(_currentHover);

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
            return Math.Abs(a.X - b.X) < 0.001 &&
                   Math.Abs(a.Y - b.Y) < 0.001 &&
                   Math.Abs(a.Z - b.Z) < 0.001;
        }

        // ─────────────────────────────
        // Rendering Sync (Moved from View)
        // ─────────────────────────────
        private void OnZoneChanged(GreenAreaZone? zone) => SyncZones();
        private void OnZonesCleared() => SyncZones();

        private void SyncZones()
        {
            var dispatcher = System.Windows.Application.Current.Dispatcher;

            if (dispatcher.CheckAccess())
                RenderZones();
            else
                dispatcher.InvokeAsync(RenderZones);
        }

        private void RenderZones()
        {
            _render.Render(
                _scene.GetZones(),
                new ZoneVisualBuilder(_scene),
                "Zones");
        }

        // ─────────────────────────────
        // Lifecycle
        // ─────────────────────────────
        public void Dispose()
        {
            if (_disposed) return;
            _disposed = true;

            _scene.ZoneAdded -= OnZoneChanged;
            _scene.ZoneUpdated -= OnZoneChanged;
            _scene.ZoneRemoved -= OnZoneChanged;
            _scene.ZonesCleared -= OnZonesCleared;
            _scene.ZoneSelected -= OnZoneChanged;
        }
    }
}

using CraneOptimizer.Application.Optimization.GeneticAlgorithms.Services;
using CraneOptimizer.Application.Optimization.GeneticAlgorithms.Settings;
using CraneOptimizer.Domain.Common;
using CraneOptimizer.Domain.Entities;
using CraneOptimizer.Domain.Schemas.Enums;
using CraneOptimizer.UI.Core.Base;
using CraneOptimizer.UI.Core.Commands;
using CraneOptimizer.UI.Core.Interfaces;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Windows.Input;

namespace CraneOptimizer.UI.Features.F6_CranePlacement.ViewModels
{
    public class CranePlacementViewModel : ViewModelBase, IDisposable
    {
        private readonly IRenderingService _render;
        private readonly ICraneConstraintService _constraintService;
        private readonly ISceneService _scene;
        private readonly GASettings _settings;

        private bool _isPlacing;
        private DomainPoint3D? _previewPoint;
        private DomainPoint3D? _selectedPoint;
        private Crane? _selectedCrane;
        private bool _disposed;

        public event Action? PlacementEnded;

        public ObservableCollection<DomainPoint3D> CandidatePoints { get; } = new();

        public ICommand SelectCandidatePointCommand { get; }

        public CranePlacementViewModel(
            IRenderingService render,
            ICraneConstraintService constraintService,
            ISceneService scene,
            GASettings settings)
        {
            _render = render;
            _constraintService = constraintService;
            _scene = scene;
            _settings = settings;

            StartPlacementCommand = new RelayCommand(StartPlacement);
            ConfirmPlacementCommand = new RelayCommand(ConfirmPlacement, () => IsPlacing && SelectedPoint != null);
            CancelPlacementCommand = new RelayCommand(CancelPlacement, () => IsPlacing);
            SelectCandidatePointCommand = new RelayCommand<DomainPoint3D>(SelectCandidatePoint);
        }

        public bool IsPlacing
        {
            get => _isPlacing;
            set
            {
                _isPlacing = value;
                OnPropertyChanged();
                CommandManager.InvalidateRequerySuggested();
            }
        }

        public DomainPoint3D? PreviewPoint
        {
            get => _previewPoint;
            set { _previewPoint = value; OnPropertyChanged(); }
        }

        public DomainPoint3D? SelectedPoint
        {
            get => _selectedPoint;
            set 
            { 
                _selectedPoint = value; 
                OnPropertyChanged();
                
                if (value != null)
                {
                    var result = ValidatePoint(value);
                    _render.RenderCranePreview(value, result);
                }

                (ConfirmPlacementCommand as RelayCommand)?.RaiseCanExecuteChanged();
            }
        }

        public Crane? SelectedCrane
        {
            get => _selectedCrane;
            set { _selectedCrane = value; OnPropertyChanged(); }
        }

        public ICommand StartPlacementCommand { get; }
        public ICommand ConfirmPlacementCommand { get; }
        public ICommand CancelPlacementCommand { get; }

        private void StartPlacement()
        {
            IsPlacing = true;
            SelectedPoint = null;
            PreviewPoint = null;
        }

        public void SetPlacementPoint(DomainPoint3D point)
        {
            if (!IsPlacing) return;
            SelectedPoint = point;
        }

        public void SetPreviewPoint(DomainPoint3D point)
        {
            if (!IsPlacing) return;

            PreviewPoint = point;
            
            var result = ValidatePoint(point);
            _render.RenderCranePreview(point, result);
        }

        private CraneConstraintResult? ValidatePoint(DomainPoint3D point)
        {
            if (SelectedCrane == null) return null;

            var spec = SelectedCrane.Specifications.FirstOrDefault();
            if (spec == null) return null;

            var elements = _scene.GetElements().ToList();
            var centers = elements.Select(e => e.ElementCOG).ToList();
            var weights = elements.Select(e => e.ElementWT ?? 0).ToList();

            return _constraintService.Validate(
                point,
                centers,
                weights,
                _scene.GetZones().Where(z => z.Type == ZoneType.Allowed).ToList(),
                _scene.GetZones().Where(z => z.Type == ZoneType.Forbidden).ToList(),
                spec,
                _settings);
        }

        private void ConfirmPlacement()
        {
            if (SelectedPoint == null) return;

            _render.RenderCraneFinal(SelectedPoint);
            _render.ClearCranePreview();

            IsPlacing = false;
            PlacementEnded?.Invoke();
        }

        private void CancelPlacement()
        {
            _render.ClearCranePreview();

            SelectedPoint = null;
            PreviewPoint = null;

            IsPlacing = false;
            PlacementEnded?.Invoke();
        }

        public void LoadCandidatePoints(IEnumerable<DomainPoint3D> points)
        {
            CandidatePoints.Clear();

            foreach (var p in points)
                CandidatePoints.Add(p);

            if (CandidatePoints.Any())
            {
                SelectCandidatePoint(CandidatePoints.First());
            }
        }

        public void SelectCandidatePoint(DomainPoint3D point)
        {
            if (point == null) return;
 
            SelectedPoint = point;
            var result = ValidatePoint(point);
            _render.RenderCranePreview(point, result);
        }

        public void Dispose()
        {
            if (_disposed) return;
            _disposed = true;
            
            _render.ClearCranePreview();
        }
    }
}

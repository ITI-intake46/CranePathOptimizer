using CraneOptimizer.Domain.Common;
using CraneOptimizer.Domain.Entities;
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
        private bool _isPlacing;
        private DomainPoint3D? _previewPoint;
        private DomainPoint3D? _selectedPoint;
        private Crane? _selectedCrane;
        private bool _disposed;

        public event Action? PlacementEnded;

        public ObservableCollection<DomainPoint3D> CandidatePoints { get; } = new();

        public ICommand SelectCandidatePointCommand { get; }

        public CranePlacementViewModel(IRenderingService render)
        {
            _render = render;

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
                    _render.RenderCranePreview(value);
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
            _render.RenderCranePreview(point);
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
            _render.RenderCranePreview(point);
        }

        public void Dispose()
        {
            if (_disposed) return;
            _disposed = true;
            
            _render.ClearCranePreview();
        }
    }
}

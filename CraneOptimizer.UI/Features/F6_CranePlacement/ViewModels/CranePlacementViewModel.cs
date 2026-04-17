using CraneOptimizer.Domain.Common;
using CraneOptimizer.Domain.Entities;
using CraneOptimizer.UI.Core.Base;
using CraneOptimizer.UI.Core.Interfaces;
using System.Windows.Input;
using CraneOptimizer.UI.Core.Commands;

namespace CraneOptimizer.UI.Features.F6_CranePlacement.ViewModels
{
    public class CranePlacementViewModel : ViewModelBase 
    {

        private readonly IRenderingService _render;

        private bool _isPlacing;
        private DomainPoint3D? _currentPosition;
        private Crane? _selectedCrane;

        public bool IsPlacing
        {
            get => _isPlacing;
            set
            {
                _isPlacing = value;
                OnPropertyChanged();
            }
        }

        public DomainPoint3D? CurrentPosition
        {
            get => _currentPosition;
            set
            {
                _currentPosition = value;
                OnPropertyChanged();
            }
        }

        public Crane? SelectedCrane
        {
            get => _selectedCrane;
            set
            {
                _selectedCrane = value;
                OnPropertyChanged();
            }
        }

        public ICommand StartPlacementCommand { get; }


        public CranePlacementViewModel(IRenderingService render)
        {
            _render = render;

            StartPlacementCommand = new RelayCommand(StartPlacement);
        }

        private void StartPlacement()
        {
            IsPlacing = true;
        }

        public void Place(DomainPoint3D point)
        {
            if (!IsPlacing || SelectedCrane == null)
                return;

            CurrentPosition = point;

            // 🔥 render crane position
            _render.RenderBestPosition(point.X, point.Y);

            IsPlacing = false;
        }
    }
}
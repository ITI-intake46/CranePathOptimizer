using CraneOptimizer.Domain.Common;
using CraneOptimizer.Domain.Entities;
using CraneOptimizer.Domain.Schemas;
using CraneOptimizer.UI.Core.Base;
using CraneOptimizer.UI.Core.Commands;
using CraneOptimizer.UI.Core.Interfaces;
using CraneOptimizer.UI.Core.Models;
using System.Windows.Input;
using System.Windows.Media.Media3D;

namespace CraneOptimizer.UI.Features.F3_CraneSetup.ViewModels
{
    public class CraneSetupViewModel : ViewModelBase
    {
        // ── Dependencies ──────────────────────────────────────────────────
        private readonly IRenderingService _render;
        private readonly ISceneService _scene;
        private readonly ICraneValidationService _craneValidation;

        // ── State ─────────────────────────────────────────────────────────
        private ModelVisual3D? _craneVisual;

        private bool _isPositionLocked;
        public bool IsPositionLocked
        {
            get => _isPositionLocked;
            set { _isPositionLocked = value; OnPropertyChanged(); }
        }

        // ── Bound Properties ──────────────────────────────────────────────
        private Crane? _selectedCrane;
        public Crane? SelectedCrane
        {
            get => _selectedCrane;
            set
            {
                _selectedCrane = value;
                OnPropertyChanged();
                OnCraneChanged(value);
            }
        }

        private DomainPoint3D? _currentPosition;
        public DomainPoint3D? CurrentPosition
        {
            get => _currentPosition;
            set { _currentPosition = value; OnPropertyChanged(); }
        }

        private bool _isDraggingActive;
        public bool IsDraggingActive
        {
            get => _isDraggingActive;
            set { _isDraggingActive = value; OnPropertyChanged(); }
        }

        private bool _isCranePickerOpen;
        public bool IsCranePickerOpen
        {
            get => _isCranePickerOpen;
            set { _isCranePickerOpen = value; OnPropertyChanged(); }
        }

        private bool _isPositionValid = true;
        public bool IsPositionValid
        {
            get => _isPositionValid;
            set { _isPositionValid = value; OnPropertyChanged(); }
        }

        // ── Commands ──────────────────────────────────────────────────
        public ICommand UnlockPositionCommand { get; private set; }
        public ICommand ToggleCranePickerCommand { get; private set; }

        // ── Constructor ───────────────────────────────────────────────────
        public CraneSetupViewModel(
            IRenderingService render,
            ISceneService scene,
            ICraneValidationService craneValidation)
        {
            _render = render;
            _scene = scene;
            _craneValidation = craneValidation;

            UnlockPositionCommand = new RelayCommand(UnlockPosition);
            ToggleCranePickerCommand = new RelayCommand(() => IsCranePickerOpen = !IsCranePickerOpen);
        }

        // ══════════════════════════════════════════════════════════════════
        // CRANE SELECTION
        // ══════════════════════════════════════════════════════════════════

        /// <summary>
        /// When a crane is selected from the picker, just store the selection
        /// and reset state. The crane is NOT placed in the scene yet — the
        /// user must click in the viewport to place it (handled by
        /// MainViewModel.PlaceSelectedCrane via CranePlacement.IsPlacing).
        /// </summary>
        private void OnCraneChanged(Crane? crane)
        {
            _craneVisual = null;

            // Reset lock state
            _isPositionLocked = false;
            IsPositionLocked = false;
            CurrentPosition = null;

            if (crane == null)
                return;

            // Just store the selection — don't add to scene.
            // The crane will appear when the user clicks in the viewport.
        }

        // ══════════════════════════════════════════════════════════════════
        // POSITION LOCK (used by GA optimizer)
        // ══════════════════════════════════════════════════════════════════

        /// <summary>
        /// Locks the crane position after GA optimization.
        /// </summary>
        public void LockPosition(DomainPoint3D lockedPosition)
        {
            CurrentPosition = lockedPosition;
            _isPositionLocked = true;
            IsPositionLocked = true;
        }

        /// <summary>
        /// Unlocks the crane position, allowing it to be dragged again.
        /// </summary>
        public void UnlockPosition()
        {
            _isPositionLocked = false;
            IsPositionLocked = false;
        }

        /// <summary>
        /// Called by CraneVisualBuilder after it creates the visual.
        /// Kept for backward compatibility.
        /// </summary>
        public void RegisterCraneVisual(ModelVisual3D visual)
        {
            _craneVisual = visual;
        }
    }
}
using System.Windows;
using CraneOptimizer.Domain.CreatedProject;
using CraneOptimizer.UI.Core.Base;
using CraneOptimizer.UI.Features.F2_IfcViewer.DTOs;
using CraneOptimizer.UI.Features.F2_IfcViewer.Services;

namespace CraneOptimizer.UI.Features.F2_IfcViewer.ViewModels
{
    public sealed class IfcOptionsDialogViewModel : ViewModelBase
    {
        private bool _hideGrids;
        private bool _hideLevels;
        private bool _hideAnnotations;
        private bool _hideSite;
        private bool _hideSpaces;
        private bool _hideOpenings;
        private bool _hideVirtualElements;
        private CoordinateReferenceMode _coordinateMode;

        public IfcOptionsDialogViewModel(
            IfcOptionsMode mode,
            string? filePath,
            IfcLoadOptionsDto options,
            IReadOnlyList<IfcVisibilityOptionDefinition> optionDefinitions)
        {
            Mode = mode;
            FilePath = filePath ?? string.Empty;
            OptionDefinitions = optionDefinitions;

            _hideGrids = options.HideGrids;
            _hideLevels = options.HideLevels;
            _hideAnnotations = options.HideAnnotations;
            _hideSite = options.HideSite;
            _hideSpaces = options.HideSpaces;
            _hideOpenings = options.HideOpenings;
            _hideVirtualElements = options.HideVirtualElements;
            _coordinateMode = options.CoordinateMode;
        }

        public IfcOptionsMode Mode { get; }
        public IReadOnlyList<IfcVisibilityOptionDefinition> OptionDefinitions { get; }
        public string FilePath { get; }
        public string DialogTitle => Mode == IfcOptionsMode.Display ? "IFC Display Options" : "IFC Load Options";
        public string PrimaryButtonText => Mode == IfcOptionsMode.Display ? "Apply" : "Load";
        public Visibility FilePathVisibility => string.IsNullOrWhiteSpace(FilePath) ? Visibility.Collapsed : Visibility.Visible;
        public Visibility CoordinateReferenceVisibility => Mode == IfcOptionsMode.Display ? Visibility.Collapsed : Visibility.Visible;

        public bool HideGrids
        {
            get => _hideGrids;
            set => SetOption(ref _hideGrids, value);
        }

        public bool HideLevels
        {
            get => _hideLevels;
            set => SetOption(ref _hideLevels, value);
        }

        public bool HideAnnotations
        {
            get => _hideAnnotations;
            set => SetOption(ref _hideAnnotations, value);
        }

        public bool HideSite
        {
            get => _hideSite;
            set => SetOption(ref _hideSite, value);
        }

        public bool HideSpaces
        {
            get => _hideSpaces;
            set => SetOption(ref _hideSpaces, value);
        }

        public bool HideOpenings
        {
            get => _hideOpenings;
            set => SetOption(ref _hideOpenings, value);
        }

        public bool HideVirtualElements
        {
            get => _hideVirtualElements;
            set => SetOption(ref _hideVirtualElements, value);
        }

        public CoordinateReferenceMode CoordinateMode
        {
            get => _coordinateMode;
            set
            {
                if (_coordinateMode == value)
                    return;

                _coordinateMode = value;
                OnPropertyChanged();
            }
        }

        public IfcLoadOptionsDto ToOptions()
        {
            return new IfcLoadOptionsDto
            {
                HideGrids = HideGrids,
                HideLevels = HideLevels,
                HideAnnotations = HideAnnotations,
                HideSite = HideSite,
                HideSpaces = HideSpaces,
                HideOpenings = HideOpenings,
                HideVirtualElements = HideVirtualElements,
                CoordinateMode = CoordinateMode
            };
        }

        public void SelectAll()
        {
            HideGrids = true;
            HideLevels = true;
            HideAnnotations = true;
            HideSite = true;
            HideSpaces = true;
            HideOpenings = true;
            HideVirtualElements = true;
        }

        public void ClearAll()
        {
            HideGrids = false;
            HideLevels = false;
            HideAnnotations = false;
            HideSite = false;
            HideSpaces = false;
            HideOpenings = false;
            HideVirtualElements = false;
        }

        private void SetOption(ref bool field, bool value, [System.Runtime.CompilerServices.CallerMemberName] string? propertyName = null)
        {
            if (field == value)
                return;

            field = value;
            OnPropertyChanged(propertyName);
        }
    }
}

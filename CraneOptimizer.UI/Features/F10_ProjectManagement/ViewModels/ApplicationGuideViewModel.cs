using CraneOptimizer.UI.Core.Base;
using CraneOptimizer.UI.Core.Commands;
using CraneOptimizer.UI.Features.F10_ProjectManagement.Models;
using System.Collections.ObjectModel;
using System.Windows;
using System.Windows.Input;

namespace CraneOptimizer.UI.Features.F10_ProjectManagement.ViewModels
{
    /// <summary>
    /// Carousel-style Application Guide ViewModel.
    /// Shows one step at a time; user navigates with Previous/Next commands.
    /// No auto-advance timer.
    /// </summary>
    public sealed class ApplicationGuideViewModel : ViewModelBase
    {
        private int _selectedIndex;

        public ApplicationGuideViewModel()
        {
            GuideSteps = new ObservableCollection<GuideStepModel>
            {
                new GuideStepModel
                {
                    StepNumber  = 1,
                    ShortTitle  = "1  Create/Open",
                    DetailTitle = "Create or Open a Project",
                    Description = "Create a new crane optimization project or open an existing workspace. " +
                                  "Name your project, set the project folder, and add optional company and country details.",
                    IsSelected  = true
                },
                new GuideStepModel
                {
                    StepNumber  = 2,
                    ShortTitle  = "2  Load IFC",
                    DetailTitle = "Load IFC Model",
                    Description = "Load the IFC model and verify geometry, elements, weights, and metadata. " +
                                  "The 3D viewer automatically calculates element weights from material densities."
                },
                new GuideStepModel
                {
                    StepNumber  = 3,
                    ShortTitle  = "3  Place Cranes",
                    DetailTitle = "Place Cranes",
                    Description = "Add Tower or Mobile cranes in the 3D view. " +
                                  "Verify crane setup and review load chart information to confirm lifting capacity."
                },
                new GuideStepModel
                {
                    StepNumber  = 4,
                    ShortTitle  = "4  Run Optimization",
                    DetailTitle = "Run Optimization",
                    Description = "Run the Genetic Algorithm optimizer to find optimal crane positions. " +
                                  "Review diagnostics, coverage zones, and export results or reports."
                }
            };

            _selectedIndex = 0;

            NextGuideStepCommand     = new RelayCommand(GoNext,     CanGoNext);
            PreviousGuideStepCommand = new RelayCommand(GoPrevious, CanGoPrevious);
            SelectGuideStepCommand   = new RelayCommand<GuideStepModel>(SelectStep);
        }

        public ObservableCollection<GuideStepModel> GuideSteps { get; }

        public int SelectedGuideStepIndex
        {
            get => _selectedIndex;
            private set
            {
                if (_selectedIndex == value) return;
                _selectedIndex = value;
                OnPropertyChanged();
                OnPropertyChanged(nameof(SelectedGuideStep));
                OnPropertyChanged(nameof(StepCountText));
                // Re-evaluate CanExecute for both nav commands
                CommandManager.InvalidateRequerySuggested();
            }
        }

        public GuideStepModel SelectedGuideStep => GuideSteps[_selectedIndex];

        public string StepCountText => $"Step {_selectedIndex + 1} of {GuideSteps.Count}";

        public ICommand NextGuideStepCommand     { get; }
        public ICommand PreviousGuideStepCommand { get; }
        public ICommand SelectGuideStepCommand   { get; }

        private bool CanGoNext()     => _selectedIndex < GuideSteps.Count - 1;
        private bool CanGoPrevious() => _selectedIndex > 0;

        private void GoNext()
        {
            if (!CanGoNext()) return;
            GuideSteps[_selectedIndex].IsSelected = false;
            SelectedGuideStepIndex++;
            GuideSteps[_selectedIndex].IsSelected = true;
        }

        private void GoPrevious()
        {
            if (!CanGoPrevious()) return;
            GuideSteps[_selectedIndex].IsSelected = false;
            SelectedGuideStepIndex--;
            GuideSteps[_selectedIndex].IsSelected = true;
        }

        private void SelectStep(GuideStepModel? step)
        {
            if (step == null) return;
            var idx = GuideSteps.IndexOf(step);
            if (idx < 0) return;
            GuideSteps[_selectedIndex].IsSelected = false;
            SelectedGuideStepIndex = idx;
            GuideSteps[_selectedIndex].IsSelected = true;
        }
    }
}

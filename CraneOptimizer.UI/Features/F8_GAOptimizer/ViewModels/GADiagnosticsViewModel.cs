using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Windows;
using System.Windows.Input;
using CraneOptimizer.Application.Optimization.GeneticAlgorithms.DTOs;
using CraneOptimizer.Application.Optimization.GeneticAlgorithms.Interfaces;
using CraneOptimizer.Application.Optimization.GeneticAlgorithms.Models;
using CraneOptimizer.UI.Core.Base;
using CraneOptimizer.UI.Core.Commands;
using CraneOptimizer.UI.Features.F10_ProjectManagement.Services;

namespace CraneOptimizer.UI.Features.F8_GAOptimizer.ViewModels
{
    public class GADiagnosticsViewModel : ViewModelBase
    {
        private readonly IGADiagnosticsReaderService _readerService;
        private readonly IProjectWorkspaceService _workspaceService;

        private ObservableCollection<GARunHistoryDto> _runHistory = new();
        private GARunHistoryDto? _selectedRun;

        private ObservableCollection<GAFailureBreakdownDto> _failureBreakdown = new();
        private ObservableCollection<GAElementDiagnosticDto> _detailedDiagnostics = new();

        // Filter variables
        private string _searchText = string.Empty;
        private string _selectedHealthFilter = "All";
        private string _selectedCategoryFilter = "All";
        private string _selectedElementTypeFilter = "All";
        private bool _showFailedOnly;

        private List<string> _elementTypeFilters = new() { "All" };

        public GADiagnosticsViewModel(
            IGADiagnosticsReaderService readerService,
            IProjectWorkspaceService workspaceService)
        {
            _readerService = readerService;
            _workspaceService = workspaceService;

            OpenDiagnosticsCsvCommand = new RelayCommand(ExecuteOpenDiagnosticsCsv, CanOpenDiagnosticsCsv);
            OpenBreakdownCsvCommand = new RelayCommand(ExecuteOpenBreakdownCsv, CanOpenBreakdownCsv);

            LoadHistory();
        }

        #region Properties

        public GAViewMode CurrentGAViewMode { get; set; } = GAViewMode.FinalAssignment;

        public ObservableCollection<GARunHistoryDto> RunHistory
        {
            get => _runHistory;
            set
            {
                _runHistory = value;
                OnPropertyChanged();
                OnPropertyChanged(nameof(HasHistory));
            }
        }

        public bool HasHistory => RunHistory != null && RunHistory.Count > 0;

        public GARunHistoryDto? SelectedRun
        {
            get => _selectedRun;
            set
            {
                if (ReferenceEquals(_selectedRun, value)) return;
                _selectedRun = value;
                OnPropertyChanged();
                OnPropertyChanged(nameof(IsSelectedRunFilesMissing));
                OnPropertyChanged(nameof(MostCommonFailureReason));

                LoadSelectedRunDetails();

                CommandManager.InvalidateRequerySuggested();
            }
        }

        public ObservableCollection<GAFailureBreakdownDto> FailureBreakdown
        {
            get => _failureBreakdown;
            set
            {
                _failureBreakdown = value;
                OnPropertyChanged();
                OnPropertyChanged(nameof(MostCommonFailureReason));
            }
        }

        public ObservableCollection<GAElementDiagnosticDto> DetailedDiagnostics
        {
            get => _detailedDiagnostics;
            set
            {
                _detailedDiagnostics = value;
                OnPropertyChanged();
                UpdateElementTypeFilters();
                ApplyFilters();
            }
        }

        private List<GAElementDiagnosticDto> _filteredDiagnostics = new();
        public List<GAElementDiagnosticDto> FilteredDiagnostics
        {
            get => _filteredDiagnostics;
            set
            {
                _filteredDiagnostics = value;
                OnPropertyChanged();
            }
        }

        public bool IsSelectedRunFilesMissing
        {
            get
            {
                if (SelectedRun == null) return false;
                bool diagMissing = !string.IsNullOrEmpty(SelectedRun.DetailedDiagnosticsCsvPath) && !System.IO.File.Exists(SelectedRun.DetailedDiagnosticsCsvPath);
                bool breakdownMissing = !string.IsNullOrEmpty(SelectedRun.FailureBreakdownCsvPath) && !System.IO.File.Exists(SelectedRun.FailureBreakdownCsvPath);
                return diagMissing || breakdownMissing;
            }
        }

        #region Summary Metrics

        public string MostCommonFailureReason
        {
            get
            {
                if (FailureBreakdown == null || FailureBreakdown.Count == 0) return "None";
                int maxCount = 0;
                string commonCat = "None";
                foreach (var breakdown in FailureBreakdown)
                {
                    if (string.Equals(breakdown.FailureCategory, "Liftable", StringComparison.OrdinalIgnoreCase)) continue;
                    if (breakdown.Count > maxCount)
                    {
                        maxCount = breakdown.Count;
                        commonCat = breakdown.FailureCategory;
                    }
                }
                return maxCount > 0 ? $"{commonCat} ({maxCount})" : "None";
            }
        }

        #endregion

        #region Filtering Options

        public string SearchText
        {
            get => _searchText;
            set
            {
                _searchText = value;
                OnPropertyChanged();
                ApplyFilters();
            }
        }

        public string SelectedHealthFilter
        {
            get => _selectedHealthFilter;
            set
            {
                _selectedHealthFilter = value;
                OnPropertyChanged();
                ApplyFilters();
            }
        }

        public string SelectedCategoryFilter
        {
            get => _selectedCategoryFilter;
            set
            {
                _selectedCategoryFilter = value;
                OnPropertyChanged();
                ApplyFilters();
            }
        }

        public string SelectedElementTypeFilter
        {
            get => _selectedElementTypeFilter;
            set
            {
                _selectedElementTypeFilter = value;
                OnPropertyChanged();
                ApplyFilters();
            }
        }

        public bool ShowFailedOnly
        {
            get => _showFailedOnly;
            set
            {
                _showFailedOnly = value;
                OnPropertyChanged();
                ApplyFilters();
            }
        }

        public List<string> HealthFilters { get; } = new() { "All", "Excellent", "Good", "Warning", "Critical" };

        public List<string> CategoryFilters { get; } = new()
        {
            "All", "OutOfRadius", "Overweight", "HeightFail", "BoomLengthExceeded", "NoValidSetupPosition",
            "ForbiddenZoneBlocked", "NotAssigned", "MissingWeightOrInvalidData", "SpecialCase", "Unknown",
            "RadiusBeyondLoadChart", "BoomBeyondLoadChart", "NoDiscreteLoadChartEntry", "NoListedCapacityForBoomRadius",
            "OutsideChartEnvelope", "InvalidBlankCell", "InvalidZeroCell", "MissingLoadChartData"
        };

        public List<string> ElementTypeFilters
        {
            get => _elementTypeFilters;
            private set
            {
                _elementTypeFilters = value;
                OnPropertyChanged();
            }
        }

        #endregion

        #region Commands

        public ICommand OpenDiagnosticsCsvCommand { get; }
        public ICommand OpenBreakdownCsvCommand { get; }

        #endregion

        #endregion

        #region Actions

        private void LoadHistory()
        {
            string? projectFolder = _workspaceService?.CurrentSession?.ProjectFolderPath;
            var history = _readerService.ReadRunHistory(projectFolder);

            var reversed = history.OrderByDescending(h => h.Timestamp).ToList();
            RunHistory = new ObservableCollection<GARunHistoryDto>(reversed);

            if (RunHistory.Count > 0)
            {
                SelectedRun = RunHistory[0];
            }
        }

        private void LoadSelectedRunDetails()
        {
            if (SelectedRun == null)
            {
                FailureBreakdown = new ObservableCollection<GAFailureBreakdownDto>();
                DetailedDiagnostics = new ObservableCollection<GAElementDiagnosticDto>();
                return;
            }

            // Load Breakdown
            var breakdownList = _readerService.ReadFailureBreakdown(SelectedRun.FailureBreakdownCsvPath);
            FailureBreakdown = new ObservableCollection<GAFailureBreakdownDto>(breakdownList);

            // Load Detailed
            var detailedList = _readerService.ReadDetailedDiagnostics(SelectedRun.DetailedDiagnosticsCsvPath);
            DetailedDiagnostics = new ObservableCollection<GAElementDiagnosticDto>(detailedList);
        }

        private void UpdateElementTypeFilters()
        {
            var types = new List<string> { "All" };
            if (_detailedDiagnostics != null)
            {
                foreach (var row in _detailedDiagnostics)
                {
                    if (!string.IsNullOrWhiteSpace(row.ElementType) && !types.Contains(row.ElementType))
                    {
                        types.Add(row.ElementType);
                    }
                }
            }
            types.Sort((a, b) => a == "All" ? -1 : b == "All" ? 1 : string.Compare(a, b, StringComparison.OrdinalIgnoreCase));
            ElementTypeFilters = types;
            _selectedElementTypeFilter = "All";
            OnPropertyChanged(nameof(SelectedElementTypeFilter));
        }

        private void ApplyFilters()
        {
            if (_detailedDiagnostics == null)
            {
                FilteredDiagnostics = new List<GAElementDiagnosticDto>();
                return;
            }

            var query = _detailedDiagnostics.AsEnumerable();

            // Search Filter
            if (!string.IsNullOrWhiteSpace(SearchText))
            {
                string search = SearchText.Trim();
                query = query.Where(r => 
                    (!string.IsNullOrEmpty(r.ElementId) && r.ElementId.Contains(search, StringComparison.OrdinalIgnoreCase)) ||
                    (!string.IsNullOrEmpty(r.ElementName) && r.ElementName.Contains(search, StringComparison.OrdinalIgnoreCase))
                );
            }

            // Health Label Filter (Only filters elements matching LiftStatus from the current health logic if needed, but the requirement is HealthLabel filter. 
            // Wait, is HealthLabel filter meant to filter history runs or elements? 
            // "Filters: Filter by HealthLabel, Filter by FailureCategory, Filter by ElementType"
            // Let's filter elements! In elements, we can map HealthLabel of elements or just check r.LiftStatus/FailureReason/Category. 
            // Wait, if it means history, history is already in a grid, and typically filtering is for the elements. 
            // Let's filter elements by their Health label if they have one, or check history HealthLabel. Let's do both! We can filter the detailed element list by r.IsLifted status or if we can support a health filter on the history itself, or elements.
            // Let's check how we can filter elements by their individual status or map HealthLabel. Since HealthLabel is on the run, but we can also filter elements where the category or status matches. 
            // Let's filter elements by their lift status: 
            // - Excellent/Good -> Liftable (IsLifted == true)
            // - Warning/Critical -> Failed (IsLifted == false)
            // Let's make SelectedHealthFilter filter elements by their lift status if selected:
            if (!string.Equals(SelectedHealthFilter, "All", StringComparison.OrdinalIgnoreCase))
            {
                if (string.Equals(SelectedHealthFilter, "Excellent", StringComparison.OrdinalIgnoreCase) || 
                    string.Equals(SelectedHealthFilter, "Good", StringComparison.OrdinalIgnoreCase))
                {
                    query = query.Where(r => r.IsLifted);
                }
                else
                {
                    query = query.Where(r => !r.IsLifted);
                }
            }

            // Failure Category Filter
            if (!string.Equals(SelectedCategoryFilter, "All", StringComparison.OrdinalIgnoreCase))
            {
                query = query.Where(r => string.Equals(r.FailureCategory, SelectedCategoryFilter, StringComparison.OrdinalIgnoreCase));
            }

            // Element Type Filter
            if (!string.Equals(SelectedElementTypeFilter, "All", StringComparison.OrdinalIgnoreCase))
            {
                query = query.Where(r => string.Equals(r.ElementType, SelectedElementTypeFilter, StringComparison.OrdinalIgnoreCase));
            }

            // Show Failed Only
            if (ShowFailedOnly)
            {
                query = query.Where(r => !r.IsLifted);
            }

            FilteredDiagnostics = query.ToList();
        }

        private bool CanOpenDiagnosticsCsv()
        {
            return SelectedRun != null && System.IO.File.Exists(SelectedRun.DetailedDiagnosticsCsvPath);
        }

        private void ExecuteOpenDiagnosticsCsv()
        {
            if (SelectedRun != null)
            {
                OpenCsvFile(SelectedRun.DetailedDiagnosticsCsvPath);
            }
        }

        private bool CanOpenBreakdownCsv()
        {
            return SelectedRun != null && System.IO.File.Exists(SelectedRun.FailureBreakdownCsvPath);
        }

        private void ExecuteOpenBreakdownCsv()
        {
            if (SelectedRun != null)
            {
                OpenCsvFile(SelectedRun.FailureBreakdownCsvPath);
            }
        }

        private void OpenCsvFile(string path)
        {
            try
            {
                var psi = new System.Diagnostics.ProcessStartInfo
                {
                    FileName = path,
                    UseShellExecute = true
                };
                System.Diagnostics.Process.Start(psi);
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Could not open file: {ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        #endregion
    }
}

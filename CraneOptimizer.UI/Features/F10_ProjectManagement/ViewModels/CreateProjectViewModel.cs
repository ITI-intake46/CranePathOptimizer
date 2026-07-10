using CraneOptimizer.Domain.Units;
using CraneOptimizer.UI.Core.Base;
using CraneOptimizer.UI.Core.Commands;
using Microsoft.Win32;
using System.IO;
using System.Windows.Input;
using System.Windows.Media.Imaging;

namespace CraneOptimizer.UI.Features.F10_ProjectManagement.ViewModels
{
    public sealed class CreateProjectViewModel : ViewModelBase
    {
        private string _projectName = string.Empty;
        private string _parentFolderPath = System.IO.Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments),
            "CraneOptimizer",
            "Projects");

        private string _country = "Egypt";
        private string _siteLocation = string.Empty;
        private string _companyName = string.Empty;
        private string? _projectImagePath;
        private BitmapImage? _projectImagePreview;

        public CreateProjectViewModel()
        {
            CreateCommand = new RelayCommand(Create, CanCreate);
            BrowseLocationCommand = new RelayCommand(BrowseLocation);
            BrowseImageCommand = new RelayCommand(BrowseImage);
            ClearImageCommand = new RelayCommand(ClearImage, () => _projectImagePath != null);
            CancelCommand = new RelayCommand(Cancel);
        }

        public event EventHandler? CreateRequested;
        public event EventHandler? BrowseLocationRequested;
        public event EventHandler? CancelRequested;

        // ── Basic fields ─────────────────────────────────────────────────

        public string ProjectName
        {
            get => _projectName;
            set
            {
                if (_projectName == value) return;
                _projectName = value;
                OnPropertyChanged();
                CommandManager.InvalidateRequerySuggested();
            }
        }

        public string ParentFolderPath
        {
            get => _parentFolderPath;
            set
            {
                if (_parentFolderPath == value) return;
                _parentFolderPath = value;
                OnPropertyChanged();
                CommandManager.InvalidateRequerySuggested();
            }
        }

        // ── Optional metadata ────────────────────────────────────────────

        public string Country
        {
            get => _country;
            set { _country = value; OnPropertyChanged(); }
        }

        public string SiteLocation
        {
            get => _siteLocation;
            set { _siteLocation = value; OnPropertyChanged(); }
        }

        public string CompanyName
        {
            get => _companyName;
            set { _companyName = value; OnPropertyChanged(); }
        }

        public string? ProjectImagePath
        {
            get => _projectImagePath;
            private set
            {
                _projectImagePath = value;
                OnPropertyChanged();
                CommandManager.InvalidateRequerySuggested();
            }
        }

        public BitmapImage? ProjectImagePreview
        {
            get => _projectImagePreview;
            private set { _projectImagePreview = value; OnPropertyChanged(); }
        }

        public bool HasImage => _projectImagePath != null;

        // ── Unit selection ───────────────────────────────────────────────

        private int _selectedLengthIndex = 1;   // 0=mm, 1=m
        public int SelectedLengthIndex
        {
            get => _selectedLengthIndex;
            set { _selectedLengthIndex = value; OnPropertyChanged(); }
        }

        private int _selectedAreaIndex;         // 0=m², 1=ft²
        public int SelectedAreaIndex
        {
            get => _selectedAreaIndex;
            set { _selectedAreaIndex = value; OnPropertyChanged(); }
        }

        private int _selectedMassIndex = 1;     // 0=kg, 1=t
        public int SelectedMassIndex
        {
            get => _selectedMassIndex;
            set { _selectedMassIndex = value; OnPropertyChanged(); }
        }

        private int _selectedPrecisionIndex = 2;
        public int SelectedPrecisionIndex
        {
            get => _selectedPrecisionIndex;
            set { _selectedPrecisionIndex = value; OnPropertyChanged(); }
        }

        public UnitSettings BuildUnitSettings() => new()
        {
            DisplayLengthUnit = SelectedLengthIndex == 0 ? LengthUnit.Millimeter : LengthUnit.Meter,
            DisplayAreaUnit   = (AreaUnit)Math.Max(0, SelectedAreaIndex),
            DisplayMassUnit   = (MassUnit)Math.Max(0, SelectedMassIndex),
            DecimalPlaces     = Math.Clamp(SelectedPrecisionIndex, 0, 6)
        };

        // ── Commands ─────────────────────────────────────────────────────

        public ICommand CreateCommand { get; }
        public ICommand BrowseLocationCommand { get; }
        public ICommand BrowseImageCommand { get; }
        public ICommand ClearImageCommand { get; }
        public ICommand CancelCommand { get; }

        private bool CanCreate() =>
            !string.IsNullOrWhiteSpace(ProjectName) &&
            !string.IsNullOrWhiteSpace(ParentFolderPath);

        private void Create() => CreateRequested?.Invoke(this, EventArgs.Empty);
        private void BrowseLocation() => BrowseLocationRequested?.Invoke(this, EventArgs.Empty);
        private void Cancel() => CancelRequested?.Invoke(this, EventArgs.Empty);

        private void BrowseImage()
        {
            var dlg = new OpenFileDialog
            {
                Title = "Select Project Image",
                Filter = "Image files (*.png;*.jpg;*.jpeg;*.bmp)|*.png;*.jpg;*.jpeg;*.bmp|All files (*.*)|*.*",
                Multiselect = false
            };

            if (dlg.ShowDialog() != true) return;

            try
            {
                var bmp = new BitmapImage();
                bmp.BeginInit();
                bmp.UriSource = new Uri(dlg.FileName);
                bmp.CacheOption = BitmapCacheOption.OnLoad;
                bmp.EndInit();

                ProjectImagePath = dlg.FileName;
                ProjectImagePreview = bmp;
                OnPropertyChanged(nameof(HasImage));
            }
            catch { /* silently ignore bad image files */ }
        }

        private void ClearImage()
        {
            ProjectImagePath = null;
            ProjectImagePreview = null;
            OnPropertyChanged(nameof(HasImage));
        }
    }
}

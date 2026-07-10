using CraneOptimizer.UI.Core.Base;
using CraneOptimizer.UI.Core.Commands;
using CraneOptimizer.UI.Features.F10_ProjectManagement.Services;
using System.IO;
using System.Windows.Input;

namespace CraneOptimizer.UI.Features.F10_ProjectManagement.ViewModels
{
    /// <summary>Possible UI-only status values for a project.</summary>
    public enum ProjectUiStatus { Opened, Closed }

    /// <summary>
    /// ViewModel for the "Edit Project Details" dialog.
    /// Edits: Name, CompanyName, Country, SiteLocation, ProjectImagePath (persisted);
    ///        Status (UI-only); RenameFolder (checkbox).
    /// </summary>
    public sealed class EditProjectViewModel : ViewModelBase
    {
        // ── backing fields ────────────────────────────────────────────────
        private string _name         = string.Empty;
        private string _companyName  = string.Empty;
        private string _country      = string.Empty;
        private string _siteLocation = string.Empty;
        private string _imagePath    = string.Empty;
        private ProjectUiStatus _status = ProjectUiStatus.Closed;
        private bool   _isBusy;
        private string _errorText    = string.Empty;
        private bool   _renameFolder = false;

        // ── read-only display ─────────────────────────────────────────────
        public string DisplayCreatedDate  { get; init; } = string.Empty;
        public string DisplayModifiedDate { get; init; } = string.Empty;
        public string DisplayFolderPath   { get; init; } = string.Empty;
        public string DisplayFolderName   { get; init; } = string.Empty;
        public string DisplayIfcFile      { get; init; } = string.Empty;
        public string DisplayFolderExists { get; init; } = string.Empty;
        public string OriginalName        { get; init; } = string.Empty;
        public bool   FolderExistsOnDisk  { get; init; }
        public Guid   ProjectId           { get; init; }

        // ── The parent dir is needed to compute the preview path ──────────
        private readonly string _parentFolderPath;

        // ── editable ──────────────────────────────────────────────────────
        public string Name
        {
            get => _name;
            set
            {
                if (_name == value) return;
                _name = value;
                OnPropertyChanged();
                OnPropertyChanged(nameof(NewFolderPreviewPath));
                OnPropertyChanged(nameof(ShowRenamePreview));
                OnPropertyChanged(nameof(TargetFolderConflict));
                OnPropertyChanged(nameof(FolderNameDiffersFromDisplayName));
            }
        }

        public string CompanyName
        {
            get => _companyName;
            set { if (_companyName == value) return; _companyName = value; OnPropertyChanged(); }
        }

        public string Country
        {
            get => _country;
            set { if (_country == value) return; _country = value; OnPropertyChanged(); }
        }

        public string SiteLocation
        {
            get => _siteLocation;
            set { if (_siteLocation == value) return; _siteLocation = value; OnPropertyChanged(); }
        }

        public string ImagePath
        {
            get => _imagePath;
            set { if (_imagePath == value) return; _imagePath = value; OnPropertyChanged(); OnPropertyChanged(nameof(HasImage)); }
        }

        public bool HasImage => !string.IsNullOrWhiteSpace(_imagePath) && File.Exists(_imagePath);

        // ── UI-only status ────────────────────────────────────────────────
        public ProjectUiStatus Status
        {
            get => _status;
            set { if (_status == value) return; _status = value; OnPropertyChanged(); }
        }

        public IEnumerable<ProjectUiStatus> AllStatuses { get; } =
            Enum.GetValues<ProjectUiStatus>();

        // ── Rename-folder checkbox ─────────────────────────────────────────
        public bool RenameFolder
        {
            get => _renameFolder;
            set
            {
                if (_renameFolder == value) return;
                _renameFolder = value;
                OnPropertyChanged();
                OnPropertyChanged(nameof(ShowRenamePreview));
                OnPropertyChanged(nameof(TargetFolderConflict));
            }
        }

        // True when the current display name differs from the physical folder name
        public bool FolderNameDiffersFromDisplayName =>
            !string.IsNullOrWhiteSpace(DisplayFolderName) &&
            !string.Equals(Name.Trim(), DisplayFolderName, StringComparison.OrdinalIgnoreCase);

        // Preview: what the new absolute path would be
        public string NewFolderPreviewPath =>
            string.IsNullOrWhiteSpace(_parentFolderPath) || string.IsNullOrWhiteSpace(Name)
                ? string.Empty
                : Path.Combine(_parentFolderPath, Name.Trim());

        // Show preview panel when: checkbox checked AND folder name will actually change
        public bool ShowRenamePreview =>
            _renameFolder &&
            !string.IsNullOrWhiteSpace(NewFolderPreviewPath) &&
            !string.Equals(Path.GetFullPath(DisplayFolderPath), Path.GetFullPath(NewFolderPreviewPath), StringComparison.OrdinalIgnoreCase);

        // True when the target folder already exists (conflict)
        public bool TargetFolderConflict =>
            ShowRenamePreview &&
            !string.IsNullOrWhiteSpace(NewFolderPreviewPath) &&
            Directory.Exists(NewFolderPreviewPath);

        // ── busy / error ──────────────────────────────────────────────────
        public bool IsBusy
        {
            get => _isBusy;
            private set { if (_isBusy == value) return; _isBusy = value; OnPropertyChanged(); }
        }

        public string ErrorText
        {
            get => _errorText;
            private set { if (_errorText == value) return; _errorText = value; OnPropertyChanged(); }
        }

        // ── commands ──────────────────────────────────────────────────────
        public ICommand SaveCommand        { get; }
        public ICommand CancelCommand      { get; }
        public ICommand BrowseImageCommand { get; }

        public event EventHandler? SaveSucceeded;
        public event EventHandler? CancelRequested;
        public event EventHandler? BrowseImageRequested;

        /// <summary>
        /// Raised after a successful rename so callers can update the active session path.
        /// Argument is the new folder path.
        /// </summary>
        public event EventHandler<string>? FolderRenamed;

        private readonly IProjectWorkspaceService _workspaceService;

        public EditProjectViewModel(IProjectWorkspaceService workspaceService, ProjectCardViewModel card)
        {
            _workspaceService = workspaceService;

            SaveCommand        = new AsyncRelayCommand(SaveAsync);
            CancelCommand      = new RelayCommand(() => CancelRequested?.Invoke(this, EventArgs.Empty));
            BrowseImageCommand = new RelayCommand(() => BrowseImageRequested?.Invoke(this, EventArgs.Empty));

            ProjectId     = card.Id;
            OriginalName  = card.Name;
            _name         = card.Name;
            _companyName  = card.CompanyName    ?? string.Empty;
            _country      = card.Country        ?? string.Empty;
            _siteLocation = card.SiteLocation   ?? string.Empty;
            _imagePath    = card.ProjectImagePath ?? string.Empty;

            // Guess initial UI status from whether folder exists
            _status = card.FolderExists ? ProjectUiStatus.Opened : ProjectUiStatus.Closed;

            DisplayCreatedDate  = card.CreatedDate      == default ? "—" : card.CreatedDate.ToLocalTime().ToString("dd MMM yyyy HH:mm");
            DisplayModifiedDate = card.LastModifiedDate == default ? "—" : card.LastModifiedDate.ToLocalTime().ToString("dd MMM yyyy HH:mm");
            DisplayFolderPath   = string.IsNullOrWhiteSpace(card.ProjectFolderPath) ? "—" : card.ProjectFolderPath;
            DisplayFolderName   = string.IsNullOrWhiteSpace(card.ProjectFolderPath) ? "—" : Path.GetFileName(card.ProjectFolderPath.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar));
            DisplayIfcFile      = card.IfcFileName;
            FolderExistsOnDisk  = card.FolderExists;
            DisplayFolderExists = card.FolderExists ? "✔ Found" : "⚠ Not found on disk";

            // Extract parent path for preview calculations
            _parentFolderPath = string.IsNullOrWhiteSpace(card.ProjectFolderPath)
                ? string.Empty
                : (Path.GetDirectoryName(card.ProjectFolderPath.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar)) ?? string.Empty);
        }

        private async Task SaveAsync()
        {
            ErrorText = string.Empty;

            var trimmedName = Name?.Trim() ?? string.Empty;

            if (string.IsNullOrWhiteSpace(trimmedName))
            {
                ErrorText = "Project name is required.";
                return;
            }

            try
            {
                IsBusy = true;

                if (_renameFolder && ShowRenamePreview)
                {
                    // Guard: conflict check
                    if (TargetFolderConflict)
                    {
                        ErrorText = $"A project folder named \"{trimmedName}\" already exists at that location. Choose a different name or uncheck 'Rename folder'.";
                        return;
                    }

                    // Rename folder + update all DBs
                    var newPath = await _workspaceService.RenameProjectFolderAsync(
                        ProjectId,
                        trimmedName,
                        string.IsNullOrWhiteSpace(CompanyName)  ? null : CompanyName.Trim(),
                        string.IsNullOrWhiteSpace(Country)      ? null : Country.Trim(),
                        string.IsNullOrWhiteSpace(SiteLocation) ? null : SiteLocation.Trim(),
                        string.IsNullOrWhiteSpace(ImagePath)    ? null : ImagePath.Trim(),
                        CancellationToken.None).ConfigureAwait(true); // stay on UI thread after await

                    FolderRenamed?.Invoke(this, newPath);
                }
                else
                {
                    // Metadata only — do NOT touch the folder
                    await _workspaceService.UpdateProjectMetadataAsync(
                        ProjectId,
                        trimmedName,
                        string.IsNullOrWhiteSpace(CompanyName)  ? null : CompanyName.Trim(),
                        string.IsNullOrWhiteSpace(Country)      ? null : Country.Trim(),
                        string.IsNullOrWhiteSpace(SiteLocation) ? null : SiteLocation.Trim(),
                        string.IsNullOrWhiteSpace(ImagePath)    ? null : ImagePath.Trim(),
                        CancellationToken.None).ConfigureAwait(true);
                }

                SaveSucceeded?.Invoke(this, EventArgs.Empty);
            }
            catch (Exception ex)
            {
                ErrorText = ex.Message;
            }
            finally
            {
                IsBusy = false;
            }
        }
    }
}

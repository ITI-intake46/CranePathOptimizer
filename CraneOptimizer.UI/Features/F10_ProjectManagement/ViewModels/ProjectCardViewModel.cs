using CraneOptimizer.UI.Core.Base;
using CraneOptimizer.UI.Features.F10_ProjectManagement.Models;
using System.IO;

namespace CraneOptimizer.UI.Features.F10_ProjectManagement.ViewModels
{
    public sealed class ProjectCardViewModel : ViewModelBase
    {
        private bool _isFavorite;

        public ProjectCardViewModel(ProjectCardModel model)
        {
            Id                = model.Id;
            Name              = model.Name;
            ProjectFolderPath = model.ProjectFolderPath;
            IfcFilePath       = model.IfcFilePath;
            CreatedDate       = model.CreatedDate;
            LastOpenedDate    = model.LastOpenedDate;
            LastModifiedDate  = model.LastModifiedDate;
            CompanyName       = model.CompanyName;
            Country           = model.Country;
            SiteLocation      = model.SiteLocation;
            ProjectImagePath  = model.ProjectImagePath;
            _isFavorite       = model.IsFavorite;
        }

        public Guid Id { get; }
        public string Name { get; }
        public string ProjectFolderPath { get; }
        public string? IfcFilePath { get; }
        public DateTime CreatedDate { get; }
        public DateTime LastOpenedDate { get; }
        public DateTime LastModifiedDate { get; }
        public string? CompanyName { get; }
        public string? Country { get; }
        public string? SiteLocation { get; }
        public string? ProjectImagePath { get; }

        public string Notes { get; } = string.Empty;

        public string IfcFileName =>
            string.IsNullOrWhiteSpace(IfcFilePath)
                ? "No IFC linked"
                : Path.GetFileName(IfcFilePath);

        public bool IsIfcMissing =>
            !string.IsNullOrWhiteSpace(IfcFilePath) && !File.Exists(IfcFilePath);

        public bool IsFavorite
        {
            get => _isFavorite;
            set
            {
                if (_isFavorite == value)
                    return;

                _isFavorite = value;
                OnPropertyChanged();
            }
        }

        /// <summary>Folder name portion of ProjectFolderPath for compact display.</summary>
        public string FolderName =>
            string.IsNullOrWhiteSpace(ProjectFolderPath)
                ? string.Empty
                : Path.GetFileName(ProjectFolderPath.TrimEnd('\\', '/'));

        /// <summary>Whether the project folder exists on disk.</summary>
        public bool FolderExists => Directory.Exists(ProjectFolderPath);

        public string DisplayCompanyName =>
            string.IsNullOrWhiteSpace(CompanyName) ? "—" : CompanyName;

        /// <summary>
        /// Shortened project folder path for display in Recent Projects.
        /// Shows "Location not found" if the folder is missing.
        /// </summary>
        public string DisplayLocation =>
            string.IsNullOrWhiteSpace(ProjectFolderPath)
                ? "Location not found"
                : FolderExists
                    ? ProjectFolderPath
                    : $"⚠ {ProjectFolderPath}";

        // ── UI-only properties (no persistence) ────────────────────────────

        private bool _isLocked;

        /// <summary>UI-only lock toggle. Does not persist or enforce real locking.</summary>
        public bool IsLocked
        {
            get => _isLocked;
            set
            {
                if (_isLocked == value) return;
                _isLocked = value;
                OnPropertyChanged();
            }
        }

        /// <summary>UI-only status placeholder. Always shows "Closed" until session tracking is implemented.</summary>
        public string DisplayStatus => "Closed";
    }
}

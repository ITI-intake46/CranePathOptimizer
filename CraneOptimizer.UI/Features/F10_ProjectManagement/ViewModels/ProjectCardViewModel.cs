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
            Id = model.Id;
            Name = model.Name;
            IfcFilePath = model.IfcFilePath;
            LastOpenedDate = model.LastOpenedDate;
            _isFavorite = model.IsFavorite;
        }

        public Guid Id { get; }
        public string Name { get; }
        public string IfcFilePath { get; }
        public DateTime LastOpenedDate { get; }

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
    }
}

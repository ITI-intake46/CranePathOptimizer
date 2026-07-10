using CraneOptimizer.Domain.Entities;
using CraneOptimizer.UI.Features.F10_ProjectManagement.DTOs;
using CraneOptimizer.UI.Features.F10_ProjectManagement.Models;

namespace CraneOptimizer.UI.Features.F10_ProjectManagement.Mappers
{
    /// <summary>
    /// Keeps project list mapping small and local to the project management feature.
    /// </summary>
    internal static class ProjectCardMapper
    {
        public static ProjectCardDto ToProjectCardDto(this Project project)
        {
            return new ProjectCardDto(
                project.Id,
                project.Name,
                project.ProjectFolderPath,
                project.IfcFiles
                    .OrderBy(file => file.FileName)
                    .Select(file => file.FilePath)
                    .FirstOrDefault(),
                ToLocalTime(project.CreatedDate),
                ToLocalTime(project.LastOpenedDate),
                ToLocalTime(project.LastModifiedDate),
                project.IsFavorite,
                project.Notes ?? string.Empty,
                project.CompanyName,
                Country: null,       // only available in per-project DB; not duplicated in global catalog
                SiteLocation: null); // only available in per-project DB
        }

        public static ProjectCardModel ToModel(this ProjectCardDto dto)
        {
            return new ProjectCardModel
            {
                Id               = dto.Id,
                Name             = dto.Name,
                ProjectFolderPath = dto.ProjectFolderPath,
                IfcFilePath      = dto.IfcFilePath,
                CreatedDate      = dto.CreatedDateUtc,
                LastOpenedDate   = dto.LastOpenedDateUtc,
                LastModifiedDate = dto.LastModifiedDateUtc,
                IsFavorite       = dto.IsFavorite,
                Notes            = dto.Notes,
                CompanyName      = dto.CompanyName,
                Country          = dto.Country,
                SiteLocation     = dto.SiteLocation,
                ProjectImagePath = dto.ProjectImagePath
            };
        }

        private static DateTime ToLocalTime(DateTime value)
        {
            if (value == default)
                return value;

            return value.Kind switch
            {
                DateTimeKind.Local => value,
                DateTimeKind.Utc => value.ToLocalTime(),
                _ => DateTime.SpecifyKind(value, DateTimeKind.Utc).ToLocalTime()
            };
        }
    }
}

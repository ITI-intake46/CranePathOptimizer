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
                project.CreatedDate,
                project.LastOpenedDate,
                project.LastModifiedDate,
                project.IsFavorite,
                project.Notes ?? string.Empty);
        }

        public static ProjectCardModel ToModel(this ProjectCardDto dto)
        {
            return new ProjectCardModel
            {
                Id = dto.Id,
                Name = dto.Name,
                ProjectFolderPath = dto.ProjectFolderPath,
                IfcFilePath = dto.IfcFilePath,
                CreatedDate = dto.CreatedDateUtc,
                LastOpenedDate = dto.LastOpenedDateUtc,
                LastModifiedDate = dto.LastModifiedDateUtc,
                IsFavorite = dto.IsFavorite,
                Notes = dto.Notes
            };
        }
    }
}

namespace CraneOptimizer.UI.Features.F10_ProjectManagement.DTOs
{
    /// <summary>
    /// Simple project data used by project list and project card screens.
    /// </summary>
    internal sealed record ProjectCardDto(
        Guid Id,
        string Name,
        string ProjectFolderPath,
        string? IfcFilePath,
        DateTime CreatedDateUtc,
        DateTime LastOpenedDateUtc,
        DateTime LastModifiedDateUtc,
        bool IsFavorite,
        string Notes,
        string? CompanyName,
        string? Country,
        string? SiteLocation,
        string? ProjectImagePath = null);
}

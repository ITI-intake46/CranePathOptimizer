namespace CraneOptimizer.UI.Features.F9_Settings.DTOs
{
    /// <summary>
    /// Simple user settings data shown on the settings screen.
    /// </summary>
    public sealed record UserSettingsDto(
        string UserId,
        string UserName,
        string OfflineUntil,
        string LicenceName);
}

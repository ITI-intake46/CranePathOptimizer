namespace CraneOptimizer.UI.Features.F3_CraneSetup.DTOs
{
    /// <summary>
    /// Simple crane option shown in the crane picker.
    /// </summary>
    public sealed record CranePickerDto(
        int Id,
        string Name,
        string Manufacturer,
        string CraneType);
}

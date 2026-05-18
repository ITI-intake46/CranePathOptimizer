using CraneOptimizer.Domain.Entities;
using CraneOptimizer.UI.Features.F3_CraneSetup.DTOs;

namespace CraneOptimizer.UI.Features.F3_CraneSetup.Mappers
{
    /// <summary>
    /// Maps crane entities to simple picker data for the UI.
    /// </summary>
    internal static class CranePickerMapper
    {
        public static CranePickerDto ToCranePickerDto(this Crane crane)
        {
            return new CranePickerDto(
                crane.Id,
                crane.Name,
                crane.Manufacturer,
                crane.CraneType.ToString());
        }
    }
}

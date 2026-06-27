using CraneOptimizer.UI.Features.F4_LoadChart.DTOs;
using CraneOptimizer.UI.Features.F4_LoadChart.Services;

namespace CraneOptimizer.Application.Tests.LoadChart
{
    public sealed class LoadChartCraneValidatorTests
    {
        [Fact]
        public void ValidateForSave_AcceptsCompleteOutriggerCrane()
        {
            LoadChartCraneDto crane = CreateValidCrane();

            LoadChartCraneValidator.ValidateForSave(crane);
        }

        [Fact]
        public void ValidateForSave_RejectsMissingManufacturerAndTransportDimensions()
        {
            LoadChartCraneDto crane = CreateValidCrane() with
            {
                Manufacturer = "",
                VehicleHeightMeters = 0d
            };

            InvalidOperationException exception = Assert.Throws<InvalidOperationException>(
                () => LoadChartCraneValidator.ValidateForSave(crane));

            Assert.Contains("Manufacturer is required.", exception.Message);
            Assert.Contains("Transport height is required", exception.Message);
        }

        [Fact]
        public void ValidateForSave_AllowsMissingOutriggerDimensionsWhenOnRubber()
        {
            LoadChartCraneDto crane = CreateValidCrane() with
            {
                OutriggerConfig = "On Rubber",
                OutriggerWidthMeters = null,
                OutriggerLengthMeters = null
            };

            LoadChartCraneValidator.ValidateForSave(crane);
        }

        [Fact]
        public void ValidateForSave_RejectsMissingOutriggerDimensionsForOutriggerOperation()
        {
            LoadChartCraneDto crane = CreateValidCrane() with
            {
                OutriggerWidthMeters = null
            };

            InvalidOperationException exception = Assert.Throws<InvalidOperationException>(
                () => LoadChartCraneValidator.ValidateForSave(crane));

            Assert.Contains("Outrigger span is required", exception.Message);
        }

        private static LoadChartCraneDto CreateValidCrane()
        {
            return new LoadChartCraneDto(
                Id: 0,
                Name: "AC 500",
                Manufacturer: "Example",
                CraneType: "Mobile",
                OutriggerConfig: "100%",
                Counterweight: 80d,
                VehicleLengthMeters: 17.2d,
                VehicleHeightMeters: 4.1d,
                VehicleWidthMeters: 3d,
                OutriggerWidthMeters: 9.6d,
                OutriggerLengthMeters: 8.8d,
                MinRadius: 3d,
                MaxRadius: 50d,
                MinBoomLength: 15d,
                MaxBoomLength: 60d,
                MaxCapacity: 500d,
                Points: new[]
                {
                    new LoadChartPointDto(3d, 15d, 500d)
                });
        }
    }
}

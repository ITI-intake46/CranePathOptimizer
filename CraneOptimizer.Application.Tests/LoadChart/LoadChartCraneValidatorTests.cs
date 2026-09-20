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

        [Theory]
        [InlineData(double.NaN, 15d, 500d)]
        [InlineData(double.PositiveInfinity, 15d, 500d)]
        [InlineData(3d, double.NegativeInfinity, 500d)]
        [InlineData(3d, 15d, double.NaN)]
        [InlineData(3d, 15d, 0d)]
        [InlineData(3d, 15d, -1d)]
        public void ValidateForSave_RejectsNonFiniteOrNonPositiveEngineeringPoints(
            double radius,
            double boomLength,
            double capacity)
        {
            LoadChartCraneDto crane = CreateValidCrane() with
            {
                Points = new[] { new LoadChartPointDto(radius, boomLength, capacity) }
            };

            Assert.Throws<InvalidOperationException>(() => LoadChartCraneValidator.ValidateForSave(crane));
        }

        [Fact]
        public void ValidateForSave_RejectsPointBelowDeclaredEnvelope()
        {
            LoadChartCraneDto crane = CreateValidCrane() with
            {
                Points = new[] { new LoadChartPointDto(2.5d, 14d, 100d) }
            };

            InvalidOperationException exception = Assert.Throws<InvalidOperationException>(
                () => LoadChartCraneValidator.ValidateForSave(crane));

            Assert.Contains("below the crane minimum operating radius", exception.Message);
            Assert.Contains("below the crane minimum boom length", exception.Message);
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

using CraneOptimizer.UI.Features.F3_CraneSetup.Parameters;
using Xunit;

namespace CraneOptimizer.Application.Tests.CraneSetup
{
    public sealed class MobileCraneParametersReachLimitTests
    {
        [Fact]
        public void WorkingRadius_ClampsToImportedLoadChartMinimumRadius()
        {
            var parameters = CreateImported180TonParameters();

            parameters.WorkingRadius = 1.14;

            Assert.Equal(2.5, parameters.WorkingRadius, precision: 3);
        }

        [Fact]
        public void WorkingRadius_ClampsToCurrentPhysicalBoomReachWhenBoomIsShorterThanChartMaximum()
        {
            var parameters = CreateImported180TonParameters();

            parameters.WorkingRadius = 100.0;

            Assert.Equal(13.1, parameters.WorkingRadius, precision: 3);
        }

        [Fact]
        public void WorkingRadius_ClampsToImportedLoadChartMaximumRadiusWhenBoomCanReachFurther()
        {
            var parameters = CreateImported180TonParameters();
            parameters.BoomLength = 62.0;

            parameters.WorkingRadius = 100.0;

            Assert.Equal(60.0, parameters.WorkingRadius, precision: 3);
        }

        private static MobileCraneParameters CreateImported180TonParameters()
        {
            return new MobileCraneParameters
            {
                MinReach = 2.5,
                MaxReach = 60.0,
                MinBoomLength = 13.1,
                MaxBoomLength = 62.0,
                BoomLength = 13.1
            };
        }
    }
}
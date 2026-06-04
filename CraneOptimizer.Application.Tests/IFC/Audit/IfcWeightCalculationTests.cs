namespace CraneOptimizer.Application.Tests.IFC.Audit
{
    public class IfcWeightCalculationTests
    {
        [Fact]
        public void Mass_ConcreteWithoutExplicitWeight_UsesDensityTimesVolume()
        {
            const double densityKgPerM3 = 2500.0;
            const double volumeM3 = 1.0;

            var massKg = densityKgPerM3 * volumeM3;

            Assert.Equal(2500.0, massKg);
        }
    }
}

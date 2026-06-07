using System.Windows;
using CraneOptimizer.UI.Features.F8_GAOptimizer.ViewModels;
using Xunit;

namespace CraneOptimizer.Application.Tests.GA
{
    public class CraneOptimizerDialogViewModelTests
    {
        [Fact]
        public void Constructor_ShouldSetDefaultValuesCorrectly()
        {
            // Arrange & Act
            var vm = new CraneOptimizerDialogViewModel();

            // Assert - Phase 16 default values
            Assert.True(vm.EnableMultiStageDiscovery);
            Assert.Equal(0.95, vm.MultiStageCoverageTarget);
            Assert.Equal(0.40, vm.AdaptiveRelocationScaleFactor);
            Assert.False(vm.UseHungarianAssignment);

            // Assert - Search default
            Assert.Equal(string.Empty, vm.SearchText);
            
            // Assert - Visibility defaults
            Assert.Equal(Visibility.Visible, vm.SectionCoreCalibration.Visibility);
            Assert.Equal(Visibility.Visible, vm.ParamPopulationSize.Visibility);
        }

        [Fact]
        public void SearchTextChange_ShouldFilterParametersCaseInsensitive()
        {
            // Arrange
            var vm = new CraneOptimizerDialogViewModel();

            // Act - Search for "population" (lowercase)
            vm.SearchText = "population";

            // Assert
            Assert.Equal(Visibility.Visible, vm.ParamPopulationSize.Visibility);
            Assert.Equal(Visibility.Collapsed, vm.ParamMaxGenerations.Visibility);
            Assert.Equal(Visibility.Visible, vm.SectionCoreCalibration.Visibility); // section has 1 matching child
        }

        [Fact]
        public void SearchTextChange_ShouldCollapseSectionIfNoChildrenMatch()
        {
            // Arrange
            var vm = new CraneOptimizerDialogViewModel();

            // Act - Search for "Hungarian"
            vm.SearchText = "Hungarian";

            // Assert
            Assert.Equal(Visibility.Visible, vm.ParamUseHungarianAssignment.Visibility);
            Assert.Equal(Visibility.Visible, vm.SectionAdvancedGA.Visibility); // contains Hungarian
            
            // Section 1 (Core Calibration) has no "Hungarian" parameters, so it should be collapsed
            Assert.Equal(Visibility.Collapsed, vm.SectionCoreCalibration.Visibility);
        }

        [Fact]
        public void ClearSearchText_ShouldRestoreAllVisibilities()
        {
            // Arrange
            var vm = new CraneOptimizerDialogViewModel();
            vm.SearchText = "Hungarian"; // Initial filter collapses other sections

            // Act - Clear search
            vm.SearchText = string.Empty;

            // Assert
            foreach (var section in vm.Sections)
            {
                Assert.Equal(Visibility.Visible, section.Visibility);
                foreach (var param in section.Parameters)
                {
                    Assert.Equal(Visibility.Visible, param.Visibility);
                }
            }
        }
    }
}

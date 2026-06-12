using System.Collections.Generic;
using System.Linq;
using System.Windows;
using CraneOptimizer.Domain.Entities;
using CraneOptimizer.UI.Features.F8_GAOptimizer.ViewModels;
using CraneOptimizer.UI.Features.F8_GAOptimizer.Workflows;
using Xunit;

namespace CraneOptimizer.Application.Tests.GA
{
    public class CraneOptimizerDialogViewModelTests
    {
        [Fact]
        public void SelectedLoadChartOperatingSetupOption_ShouldExposeEngineeringIntuitionTradeOff()
        {
            // Arrange
            var vm = new CraneOptimizerDialogViewModel();
            var crane = new Crane
            {
                Specifications = new List<CraneSpecification>
                {
                    new()
                    {
                        LoadChart = new List<TargetPoint>
                        {
                            new() { Radius = 10, BoomLength = 34.8, Capacity = 44.0, NumberOfLines = 2, VisualColumnIndex = 1 },
                            new() { Radius = 12, BoomLength = 34.8, Capacity = 26.5, NumberOfLines = 2, VisualColumnIndex = 1 },
                            new() { Radius = 10, BoomLength = 60.0, Capacity = 12.3, NumberOfLines = 8, VisualColumnIndex = 2 }
                        }
                    }
                }
            };

            // Act
            vm.RefreshLoadChartOperatingSetupOptions(crane);
            var twoLine = vm.LoadChartOperatingSetupOptions.First(option => option.NumberOfLinesConstraint == 2);
            var eightLine = vm.LoadChartOperatingSetupOptions.First(option => option.NumberOfLinesConstraint == 8);

            vm.SelectedLoadChartOperatingSetupOption = twoLine;
            double twoLineCapacityTendency = vm.CapacityTendencyPercent;
            double twoLineHookSpeedTendency = vm.HookSpeedTendencyPercent;

            vm.SelectedLoadChartOperatingSetupOption = eightLine;

            // Assert
            Assert.True(vm.CapacityTendencyPercent > twoLineCapacityTendency);
            Assert.True(vm.HookSpeedTendencyPercent < twoLineHookSpeedTendency);
            Assert.Contains("8x", vm.CapacityTendencyValue);
            Assert.Contains("Engineering intuition only", vm.OperatingSetupSafetyNote);
        }

        [Fact]
        public void SelectedLoadChartOperatingSetupSliderValue_ShouldSelectDiscreteImportedSetupStops()
        {
            // Arrange
            var vm = new CraneOptimizerDialogViewModel();
            var crane = new Crane
            {
                Specifications = new List<CraneSpecification>
                {
                    new()
                    {
                        LoadChart = new List<TargetPoint>
                        {
                            new() { Radius = 10, BoomLength = 34.8, Capacity = 44.0, NumberOfLines = 2, VisualColumnIndex = 1 },
                            new() { Radius = 12, BoomLength = 39.1, Capacity = 28.8, NumberOfLines = 3, VisualColumnIndex = 2 },
                            new() { Radius = 14, BoomLength = 43.4, Capacity = 21.8, NumberOfLines = 5, VisualColumnIndex = 3 }
                        }
                    }
                }
            };

            // Act
            vm.RefreshLoadChartOperatingSetupOptions(crane);

            // Assert default Any stop preserves current behavior.
            Assert.Equal(0d, vm.SelectedLoadChartOperatingSetupSliderValue);
            Assert.Equal(3d, vm.OperatingSetupSliderMaximum);
            Assert.Equal("Any setup / fewer lines", vm.OperatingSetupScaleStartLabel);
            Assert.Equal("More lines / slower hook", vm.OperatingSetupScaleEndLabel);
            Assert.Equal(new[] { "Any", "2", "3", "5" }, vm.OperatingSetupScaleStopLabels);
            Assert.Contains("Any", vm.OperatingSetupScaleStops);
            Assert.Contains("2", vm.OperatingSetupScaleStops);
            Assert.Contains("5", vm.OperatingSetupScaleStops);
            Assert.DoesNotContain("\u00e2", vm.OperatingSetupScaleStops);
            Assert.DoesNotContain("\u251c", vm.OperatingSetupScaleStops);
            Assert.Null(vm.SelectedLoadChartOperatingSetupOption.NumberOfLinesConstraint);

            // Act: snap to the second imported line-count stop: Any, 2, 3, 5.
            vm.SelectedLoadChartOperatingSetupSliderValue = 2d;

            // Assert
            Assert.Equal(3, vm.SelectedLoadChartOperatingSetupOption.NumberOfLinesConstraint);
            Assert.Contains("Balanced", vm.OperatingSetupSelectedTone);
            Assert.Contains("3x", vm.CapacityTendencyValue);

            // Act: selecting the option directly keeps the slider in sync.
            vm.SelectedLoadChartOperatingSetupOption = vm.LoadChartOperatingSetupOptions.First(option => option.NumberOfLinesConstraint == 5);

            // Assert
            Assert.Equal(3d, vm.SelectedLoadChartOperatingSetupSliderValue);
            Assert.Contains("Heavier", vm.OperatingSetupSelectedTone);
            Assert.Contains("20", vm.HookSpeedTendencyValue);
        }
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


        [Fact]
        public void LoadChartOperatingSetupDisclosure_ShouldDefaultCollapsedAndPreserveSelectedConstraint()
        {
            // Arrange
            var vm = new CraneOptimizerDialogViewModel();
            var crane = new Crane
            {
                Specifications = new List<CraneSpecification>
                {
                    new()
                    {
                        LoadChart = new List<TargetPoint>
                        {
                            new() { Radius = 10, BoomLength = 34.8, Capacity = 44.0, NumberOfLines = 2, VisualColumnIndex = 1 },
                            new() { Radius = 12, BoomLength = 39.1, Capacity = 28.8, NumberOfLines = 4, VisualColumnIndex = 2 }
                        }
                    }
                }
            };

            // Act
            vm.RefreshLoadChartOperatingSetupOptions(crane);

            // Assert: automatic default remains compact and preserves existing optimizer behavior.
            Assert.False(vm.IsLoadChartOperatingSetupExpanded);
            Assert.Equal(Visibility.Collapsed, vm.LoadChartOperatingSetupDetailsVisibility);
            Assert.Equal("Show details", vm.LoadChartOperatingSetupDisclosureText);
            Assert.Equal("Automatic", vm.OperatingSetupConstraintBadgeText);
            Assert.Contains("Automatic", vm.OperatingSetupCollapsedSummary);
            Assert.Null(vm.SelectedLoadChartOperatingSetupOption.NumberOfLinesConstraint);

            // Act: expanding details and choosing a constraint must not reset when collapsed again.
            vm.IsLoadChartOperatingSetupExpanded = true;
            vm.SelectedLoadChartOperatingSetupOption = vm.LoadChartOperatingSetupOptions.First(option => option.NumberOfLinesConstraint == 4);
            vm.IsLoadChartOperatingSetupExpanded = false;

            // Assert
            Assert.Equal(Visibility.Collapsed, vm.LoadChartOperatingSetupDetailsVisibility);
            Assert.Equal("Constrained", vm.OperatingSetupConstraintBadgeText);
            Assert.Contains("4 lines", vm.OperatingSetupCollapsedSummary);
            Assert.Equal(4, vm.SelectedLoadChartOperatingSetupOption.NumberOfLinesConstraint);
        }

        [Fact]
        public void RefreshLoadChartOperatingSetupOptions_ShouldExposeImportedNumberOfLinesChoices()
        {
            // Arrange
            var vm = new CraneOptimizerDialogViewModel();
            var crane = new Crane
            {
                Specifications = new List<CraneSpecification>
                {
                    new()
                    {
                        LoadChart = new List<TargetPoint>
                        {
                            new() { Radius = 10, BoomLength = 39.1, Capacity = 35.1, NumberOfLines = 4, VisualColumnIndex = 1 },
                            new() { Radius = 12, BoomLength = 39.1, Capacity = 28.8, NumberOfLines = 4, VisualColumnIndex = 1 },
                            new() { Radius = 10, BoomLength = 43.4, Capacity = 27.8, NumberOfLines = 3, VisualColumnIndex = 2 }
                        }
                    }
                }
            };

            // Act
            vm.RefreshLoadChartOperatingSetupOptions(crane);

            // Assert
            Assert.True(vm.IsLoadChartOperatingSetupSelectorEnabled);
            Assert.Contains(vm.LoadChartOperatingSetupOptions, option => option.IsDefault);
            Assert.Contains(vm.LoadChartOperatingSetupOptions, option => option.NumberOfLinesConstraint == 4);
            Assert.Contains(vm.LoadChartOperatingSetupOptions, option => option.NumberOfLinesConstraint == 3);
        }

        [Fact]
        public void BuildSettings_ShouldMapSelectedLoadChartOperatingSetupConstraints()
        {
            // Arrange
            var workflow = new CraneOptimizerDialogWorkflow(null!, null!);
            var options = CreateDefaultOptions(loadChartNumberOfLinesConstraint: 4);

            // Act
            var settings = workflow.BuildSettings(options);

            // Assert
            Assert.Equal(4, settings.LoadChartNumberOfLinesConstraint);
            Assert.Null(settings.LoadChartOperatingConditionConstraint);
        }

        [Fact]
        public void RefreshLoadChartOperatingSetupOptions_ShouldExposeOperatingConditionChoicesWithoutLineCountMetadata()
        {
            // Arrange
            var vm = new CraneOptimizerDialogViewModel();
            var crane = new Crane
            {
                Specifications = new List<CraneSpecification>
                {
                    new()
                    {
                        LoadChart = new List<TargetPoint>
                        {
                            new()
                            {
                                Radius = 10,
                                BoomLength = 34.8,
                                Capacity = 44.0,
                                OperatingConditionLabel = "On outriggers 360 degrees",
                                VisualColumnHeader = "On outriggers 360 degrees"
                            },
                            new()
                            {
                                Radius = 12,
                                BoomLength = 34.8,
                                Capacity = 26.5,
                                OperatingConditionLabel = "On outriggers 360 degrees",
                                VisualColumnHeader = "On outriggers 360 degrees"
                            }
                        }
                    }
                }
            };

            // Act
            vm.RefreshLoadChartOperatingSetupOptions(crane);
            var conditionOption = Assert.Single(
                vm.LoadChartOperatingSetupOptions,
                option => option.OperatingConditionConstraint == "On outriggers 360 degrees");
            vm.SelectedLoadChartOperatingSetupOption = conditionOption;

            // Assert
            Assert.True(vm.IsLoadChartOperatingSetupSelectorEnabled);
            Assert.Equal("Condition: On outriggers 360 degrees", conditionOption.DisplayText);
            Assert.Null(conditionOption.NumberOfLinesConstraint);
            Assert.Equal("On outriggers 360 degrees", conditionOption.OperatingConditionConstraint);
            Assert.Contains("operating-condition option", vm.LoadChartOperatingSetupStatus);
            Assert.Equal("On outriggers 360 degrees", vm.SelectedLoadChartOperatingSetupOption.OperatingConditionConstraint);
            Assert.Contains("On outriggers 360 degrees", vm.OperatingSetupScaleStopLabels);
        }

        [Fact]
        public void BuildSettings_ShouldTrimSelectedLoadChartOperatingConditionConstraint()
        {
            // Arrange
            var workflow = new CraneOptimizerDialogWorkflow(null!, null!);
            var options = CreateDefaultOptions(loadChartOperatingConditionConstraint: "  On outriggers 360 degrees  ");

            // Act
            var settings = workflow.BuildSettings(options);

            // Assert
            Assert.Null(settings.LoadChartNumberOfLinesConstraint);
            Assert.Equal("On outriggers 360 degrees", settings.LoadChartOperatingConditionConstraint);
        }

        [Fact]
        public void BuildSettings_ShouldPreserveAnyLoadChartOperatingSetupAsNullConstraints()
        {
            // Arrange
            var workflow = new CraneOptimizerDialogWorkflow(null!, null!);
            var options = CreateDefaultOptions();

            // Act
            var settings = workflow.BuildSettings(options);

            // Assert
            Assert.Null(settings.LoadChartNumberOfLinesConstraint);
            Assert.Null(settings.LoadChartOperatingConditionConstraint);
        }

        [Fact]
        public void RefreshLoadChartOperatingSetupOptions_ShouldExposeOperatingConditionChoicesWhenLineCountMetadataAlsoExists()
        {
            // Arrange
            var vm = new CraneOptimizerDialogViewModel();
            var crane = new Crane
            {
                Specifications = new List<CraneSpecification>
                {
                    new()
                    {
                        LoadChart = new List<TargetPoint>
                        {
                            new()
                            {
                                Radius = 10,
                                BoomLength = 39.1,
                                Capacity = 35.1,
                                NumberOfLines = 4,
                                OperatingConditionLabel = "On outriggers 360 degrees",
                                VisualColumnIndex = 1,
                                VisualColumnHeader = "On outriggers 360 degrees"
                            },
                            new()
                            {
                                Radius = 12,
                                BoomLength = 39.1,
                                Capacity = 28.8,
                                NumberOfLines = 4,
                                OperatingConditionLabel = "On outriggers 360 degrees",
                                VisualColumnIndex = 1,
                                VisualColumnHeader = "On outriggers 360 degrees"
                            }
                        }
                    }
                }
            };

            // Act
            vm.RefreshLoadChartOperatingSetupOptions(crane);
            var conditionOption = Assert.Single(
                vm.LoadChartOperatingSetupOptions,
                option => option.OperatingConditionConstraint == "On outriggers 360 degrees");

            // Assert
            Assert.Contains(vm.LoadChartOperatingSetupOptions, option => option.NumberOfLinesConstraint == 4);
            Assert.Null(conditionOption.NumberOfLinesConstraint);
            Assert.Equal("Condition: On outriggers 360 degrees", conditionOption.DisplayText);
            Assert.Contains("operating-condition option", vm.LoadChartOperatingSetupStatus);
        }        private static CraneOptimizerDialogOptions CreateDefaultOptions(
            int? loadChartNumberOfLinesConstraint = null,
            string? loadChartOperatingConditionConstraint = null)
        {
            return new CraneOptimizerDialogOptions(
                "10",
                "5",
                "2.0",
                "0.0",
                "0.9",
                "0.95",
                "1",
                "20",
                "0",
                "3",
                "1",
                "2",
                "10",
                "5",
                "2",
                "8",
                "1.5",
                "10",
                "3",
                "2",
                "80",
                "50",
                "30",
                "30",
                "0.10",
                "0.05",
                "0.8",
                "42",
                "0",
                new[] { "Column" },
                "false",
                "1.5",
                "0.5",
                "true",
                "0",
                "0",
                "0",
                "0",
                "0",
                "0",
                "0",
                "true",
                string.Empty,
                "true",
                "0.95",
                "0.40",
                "false",
                loadChartNumberOfLinesConstraint,
                loadChartOperatingConditionConstraint);
        }
    }
}

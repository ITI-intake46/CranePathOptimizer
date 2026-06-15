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
            Assert.Equal("Any setup", vm.OperatingSetupScaleStartLabel);
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
        }

        [Fact]
        public void RefreshLoadChartOperatingSetupOptions_ShouldSuppressExactPdfVisualProvenanceLabels()
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
                            new() { Radius = 5, BoomLength = 34.8, Capacity = 44.0, NumberOfLines = 5, VisualColumnIndex = 1, VisualColumnHeader = "34.8", OperatingConditionLabel = "34.8 / 5 lines" },
                            new() { Radius = 6, BoomLength = 39.1, Capacity = 39.9, NumberOfLines = 4, VisualColumnIndex = 2, VisualColumnHeader = "39.1", OperatingConditionLabel = "39.1 / 4 lines" },
                            new() { Radius = 7, BoomLength = 39.1, Capacity = 35.1, NumberOfLines = 4, VisualColumnIndex = 3, VisualColumnHeader = "39.1 (2)", OperatingConditionLabel = "39.1 / 4 lines" },
                            new() { Radius = 8, BoomLength = 43.4, Capacity = 29.4, NumberOfLines = 3, VisualColumnIndex = 5, VisualColumnHeader = "43.4", OperatingConditionLabel = "43.4 / 3 lines" },
                            new() { Radius = 10, BoomLength = 60.0, Capacity = 12.3, NumberOfLines = 2, VisualColumnIndex = 14, VisualColumnHeader = "60", OperatingConditionLabel = "60 / 2 lines" }
                        }
                    }
                }
            };

            // Act
            vm.RefreshLoadChartOperatingSetupOptions(crane);

            // Assert
            Assert.Equal(new[] { "Any", "2", "3", "4", "5" }, vm.OperatingSetupScaleStopLabels);
            Assert.Equal(5, vm.LoadChartOperatingSetupOptions.Count);
            Assert.DoesNotContain(vm.LoadChartOperatingSetupOptions, option => option.OperatingConditionConstraint != null);
            Assert.DoesNotContain("operating-condition option", vm.LoadChartOperatingSetupStatus);
            Assert.DoesNotContain("39.1", vm.OperatingSetupScaleStops);
            Assert.DoesNotContain("60", vm.OperatingSetupScaleStops);
        }

        [Fact]
        public void RefreshLoadChartOperatingSetupOptions_ShouldRetainSemanticOperatingConditionLabelsContainingNumbers()
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
                                VisualColumnIndex = 1,
                                VisualColumnHeader = "39.1",
                                OperatingConditionLabel = "On outriggers 360 degrees"
                            },
                            new()
                            {
                                Radius = 12,
                                BoomLength = 43.4,
                                Capacity = 24.8,
                                NumberOfLines = 3,
                                VisualColumnIndex = 2,
                                VisualColumnHeader = "43.4",
                                OperatingConditionLabel = "7.6m x 7.6m outriggers"
                            }
                        }
                    }
                }
            };

            // Act
            vm.RefreshLoadChartOperatingSetupOptions(crane);

            // Assert
            Assert.Contains(vm.LoadChartOperatingSetupOptions, option => option.NumberOfLinesConstraint == 3);
            Assert.Contains(vm.LoadChartOperatingSetupOptions, option => option.NumberOfLinesConstraint == 4);
            Assert.Contains(vm.LoadChartOperatingSetupOptions, option => option.OperatingConditionConstraint == "On outriggers 360 degrees");
            Assert.Contains(vm.LoadChartOperatingSetupOptions, option => option.OperatingConditionConstraint == "7.6m x 7.6m outriggers");
            Assert.Contains("operating-condition option", vm.LoadChartOperatingSetupStatus);
        }

        [Fact]
        public void RefreshLoadChartOperatingSetupOptions_ShouldRetainSemanticConditionWhenLineCountSuffixIsPresent()
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
                                VisualColumnIndex = 1,
                                VisualColumnHeader = "39.1",
                                OperatingConditionLabel = "On outriggers 360 degrees / 4 lines"
                            }
                        }
                    }
                }
            };

            // Act
            vm.RefreshLoadChartOperatingSetupOptions(crane);
            var conditionOption = Assert.Single(
                vm.LoadChartOperatingSetupOptions,
                option => option.OperatingConditionConstraint == "On outriggers 360 degrees / 4 lines");

            // Assert
            Assert.Equal("Condition: On outriggers 360 degrees / 4 lines", conditionOption.DisplayText);
            Assert.Contains(vm.LoadChartOperatingSetupOptions, option => option.NumberOfLinesConstraint == 4);
        }

        [Fact]
        public void RefreshLoadChartOperatingSetupOptions_ShouldDescribeLineCountOptionsAsMaximumEligibilityConstraints()
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
                            new() { Radius = 5, BoomLength = 34.8, Capacity = 44.0, NumberOfLines = 5, VisualColumnIndex = 1 },
                            new() { Radius = 6, BoomLength = 39.1, Capacity = 39.9, NumberOfLines = 4, VisualColumnIndex = 2 },
                            new() { Radius = 7, BoomLength = 43.4, Capacity = 35.1, NumberOfLines = 3, VisualColumnIndex = 3 },
                            new() { Radius = 8, BoomLength = 47.7, Capacity = 29.4, NumberOfLines = 2, VisualColumnIndex = 4 }
                        }
                    }
                }
            };

            // Act
            vm.RefreshLoadChartOperatingSetupOptions(crane);

            var upTo3 = vm.LoadChartOperatingSetupOptions.First(option => option.NumberOfLinesConstraint == 3);
            var upTo5 = vm.LoadChartOperatingSetupOptions.First(option => option.NumberOfLinesConstraint == 5);

            // Assert
            Assert.Equal("Up to 3 lines", upTo3.DisplayText);
            Assert.Contains("2 usable imported capacity values", upTo3.Description);
            Assert.Contains("requiring up to 3 lines", upTo3.Description);

            Assert.Equal("Up to 5 lines", upTo5.DisplayText);
            Assert.Contains("4 usable imported capacity values", upTo5.Description);
            Assert.Contains("requiring up to 5 lines", upTo5.Description);
            Assert.Contains("Blank/zero preserved chart cells remain non-liftable", upTo5.Description);
        }

        [Fact]
        public void RefreshLoadChartOperatingSetupOptions_ShouldCountOnlyUsablePositiveCapacityValuesInLineCountCoverageDescriptions()
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
                            new() { Radius = 8, BoomLength = 47.7, Capacity = 29.4, NumberOfLines = 2, VisualColumnIndex = 1 },
                            new() { Radius = 10, BoomLength = 47.7, Capacity = 0.0, NumberOfLines = 2, VisualColumnIndex = 2 },
                            new() { Radius = 12, BoomLength = 43.4, Capacity = 35.1, NumberOfLines = 3, VisualColumnIndex = 3 },
                            new() { Radius = 14, BoomLength = 43.4, Capacity = -1.0, NumberOfLines = 3, VisualColumnIndex = 4 },
                            new() { Radius = 16, BoomLength = 39.1, Capacity = 0.0, NumberOfLines = 4, VisualColumnIndex = 5 },
                            new() { Radius = 18, BoomLength = 34.8, Capacity = 44.0, NumberOfLines = 5, VisualColumnIndex = 6 }
                        }
                    }
                }
            };

            // Act
            vm.RefreshLoadChartOperatingSetupOptions(crane);

            var upTo3 = vm.LoadChartOperatingSetupOptions.First(option => option.NumberOfLinesConstraint == 3);
            var upTo4 = vm.LoadChartOperatingSetupOptions.First(option => option.NumberOfLinesConstraint == 4);
            var upTo5 = vm.LoadChartOperatingSetupOptions.First(option => option.NumberOfLinesConstraint == 5);

            // Assert
            Assert.Contains("2 usable imported capacity values", upTo3.Description);
            Assert.Contains("across 2 visual columns", upTo3.Description);
            Assert.Contains("2 usable imported capacity values", upTo4.Description);
            Assert.Contains("across 2 visual columns", upTo4.Description);
            Assert.Contains("3 usable imported capacity values", upTo5.Description);
            Assert.Contains("across 3 visual columns", upTo5.Description);
            Assert.DoesNotContain("6 imported chart cell(s)", upTo5.Description);
            Assert.Contains("Blank/zero preserved chart cells remain non-liftable", upTo5.Description);
        }

        [Fact]
        public void RefreshLoadChartOperatingSetupOptions_ShouldCountOnlyUsablePositiveCapacityValuesInConditionCoverageDescriptions()
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
                                VisualColumnIndex = 1,
                                OperatingConditionLabel = "On outriggers 360 degrees",
                                VisualColumnHeader = "On outriggers 360 degrees"
                            },
                            new()
                            {
                                Radius = 12,
                                BoomLength = 34.8,
                                Capacity = 0.0,
                                VisualColumnIndex = 2,
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

            // Assert
            Assert.Contains("1 usable imported capacity value", conditionOption.Description);
            Assert.Contains("across 1 visual column", conditionOption.Description);
            Assert.DoesNotContain("2 imported chart cell(s)", conditionOption.Description);
            Assert.Contains("Blank/zero preserved chart cells remain non-liftable", conditionOption.Description);
        }

        [Fact]
        public void RefreshLoadChartOperatingSetupOptions_ShouldDescribeAnyOptionWithUsableCapacityCoverage()
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
                            new() { Radius = 8, BoomLength = 47.7, Capacity = 29.4, NumberOfLines = 2, VisualColumnIndex = 1 },
                            new() { Radius = 10, BoomLength = 47.7, Capacity = 0.0, NumberOfLines = 2, VisualColumnIndex = 2 },
                            new() { Radius = 12, BoomLength = 43.4, Capacity = 35.1, NumberOfLines = 3, VisualColumnIndex = 3 },
                            new() { Radius = 14, BoomLength = 39.1, Capacity = -1.0, NumberOfLines = 4, VisualColumnIndex = 4 }
                        }
                    }
                }
            };

            // Act
            vm.RefreshLoadChartOperatingSetupOptions(crane);

            var anyOption = vm.LoadChartOperatingSetupOptions.First();

            // Assert
            Assert.True(anyOption.IsDefault);
            Assert.Same(anyOption, vm.SelectedLoadChartOperatingSetupOption);
            Assert.Contains("2 usable imported capacity values", anyOption.Description);
            Assert.Contains("across 2 visual columns", anyOption.Description);
            Assert.Contains("No line-count or operating-condition constraint is applied", anyOption.Description);
            Assert.DoesNotContain("4 imported chart cell", anyOption.Description);
            Assert.Equal(anyOption.Description, vm.OperatingSetupChartCoverageSummary);
            Assert.Contains("2 usable imported capacity values", vm.OperatingSetupCollapsedSubtext);
        }

        [Fact]
        public void RefreshLoadChartOperatingSetupOptions_ShouldPluralizeSingleUsableCapacityAndVisualColumn()
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
                            new() { Radius = 8, BoomLength = 47.7, Capacity = 29.4, NumberOfLines = 2, VisualColumnIndex = 1 },
                            new() { Radius = 10, BoomLength = 47.7, Capacity = 0.0, NumberOfLines = 2, VisualColumnIndex = 2 }
                        }
                    }
                }
            };

            // Act
            vm.RefreshLoadChartOperatingSetupOptions(crane);

            var anyOption = vm.LoadChartOperatingSetupOptions.First();
            var upTo2 = vm.LoadChartOperatingSetupOptions.First(option => option.NumberOfLinesConstraint == 2);

            // Assert
            Assert.Contains("1 usable imported capacity value", anyOption.Description);
            Assert.DoesNotContain("1 usable imported capacity values", anyOption.Description);
            Assert.Contains("across 1 visual column", anyOption.Description);
            Assert.DoesNotContain("across 1 visual columns", anyOption.Description);

            Assert.Contains("1 usable imported capacity value", upTo2.Description);
            Assert.Contains("across 1 visual column", upTo2.Description);
        }

        [Fact]
        public void RefreshLoadChartOperatingSetupOptions_ShouldDescribeNoUsableCapacityValuesForAnyOption()
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
                            new() { Radius = 8, BoomLength = 47.7, Capacity = 0.0, NumberOfLines = 2, VisualColumnIndex = 1 },
                            new() { Radius = 10, BoomLength = 47.7, Capacity = -1.0, NumberOfLines = 2, VisualColumnIndex = 2 }
                        }
                    }
                }
            };

            // Act
            vm.RefreshLoadChartOperatingSetupOptions(crane);

            // Assert
            Assert.True(vm.SelectedLoadChartOperatingSetupOption.IsDefault);
            Assert.Contains("No usable imported capacity values were found", vm.SelectedLoadChartOperatingSetupOption.Description);
            Assert.Contains("existing fallback behavior", vm.SelectedLoadChartOperatingSetupOption.Description);
        }
        private static CraneOptimizerDialogOptions CreateDefaultOptions(
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

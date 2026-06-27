using System.Globalization;
using CraneOptimizer.Application.Optimization.GeneticAlgorithms.DTOs;
using CraneOptimizer.Application.Reports.Inputs;
using CraneOptimizer.Application.Reports.Interfaces;
using CraneOptimizer.Application.Reports.Models;

namespace CraneOptimizer.Application.Reports.Builders;

[System.Obsolete("Unused legacy headless reporting foundation. UI uses UserFacingReportBuilder instead.")]
public sealed class OptimizationSummaryReportBuilder
    : IReportBuilder<OptimizationReportInput>
{
    public string ReportId => "optimization-summary";

    public ReportDocument Build(OptimizationReportInput input)
    {
        ArgumentNullException.ThrowIfNull(input);
        ArgumentNullException.ThrowIfNull(input.PlacementInput);
        ArgumentNullException.ThrowIfNull(input.PlacementResult);

        var placementInput = input.PlacementInput;
        var result = input.PlacementResult;

        return new ReportDocument
        {
            ReportId = ReportId,
            Title = "Optimization Summary Report",
            CreatedAtUtc = input.CreatedAtUtc,
            Metadata = new ReportMetadata
            {
                ProjectName = placementInput.ProjectName ?? string.Empty,
                ProjectId = placementInput.ProjectId ?? string.Empty,
                IfcFileName = placementInput.IfcFileName ?? string.Empty,
                CraneName = ResolveCraneName(placementInput),
                PreparedBy = input.PreparedBy ?? string.Empty,
                Extra =
                {
                    ["ScenarioId"] = placementInput.ScenarioId ?? string.Empty,
                    ["ScopeDefinitionHash"] = placementInput.ScopeDefinitionHash ?? string.Empty,
                    ["RequestedMaxCranePositions"] = placementInput.RequestedMaxCranePositions.ToString(CultureInfo.InvariantCulture)
                }
            },
            Sections =
            {
                BuildSummarySection(result),
                BuildCranePositionsSection(result),
                BuildFailureBreakdownSection(result),
                BuildElementLiftDetailsSection(result)
            }
        };
    }

    private static string ResolveCraneName(PlacementInput placementInput)
    {
        var spec = placementInput.CraneSpec;

        if (!string.IsNullOrWhiteSpace(spec?.Crane?.Name))
            return spec.Crane.Name;

        if (spec?.Id > 0)
            return $"Crane Specification #{spec.Id}";

        return string.Empty;
    }

    private static ReportSection BuildSummarySection(PlacementResult result)
    {
        var failed = Math.Max(0, result.TotalElementCount - result.LiftedElementCount);

        return new ReportSection
        {
            Title = "Summary",
            Description = "High-level optimization result summary.",
            Metrics =
            {
                new ReportMetric { Label = "Total Elements", Value = result.TotalElementCount.ToString(CultureInfo.InvariantCulture) },
                new ReportMetric { Label = "Lifted Elements", Value = result.LiftedElementCount.ToString(CultureInfo.InvariantCulture) },
                new ReportMetric { Label = "Failed Elements", Value = failed.ToString(CultureInfo.InvariantCulture) },
                new ReportMetric { Label = "Coverage", Value = (result.CoverageRatio * 100.0).ToString("0.##", CultureInfo.InvariantCulture), Unit = "%" },
                new ReportMetric { Label = "Fitness Score", Value = result.FitnessScore.ToString("0.###", CultureInfo.InvariantCulture) },
                new ReportMetric { Label = "Crane Setups", Value = result.Positions.Count.ToString(CultureInfo.InvariantCulture) },
                new ReportMetric { Label = "Liftable", Value = result.Statistics.Liftable.ToString(CultureInfo.InvariantCulture) },
                new ReportMetric { Label = "Out Of Radius", Value = result.Statistics.OutOfRadius.ToString(CultureInfo.InvariantCulture) },
                new ReportMetric { Label = "Overweight", Value = result.Statistics.Overweight.ToString(CultureInfo.InvariantCulture) },
                new ReportMetric { Label = "Height Fail", Value = result.Statistics.HeightFail.ToString(CultureInfo.InvariantCulture) },
                new ReportMetric { Label = "Special Case", Value = result.Statistics.SpecialCase.ToString(CultureInfo.InvariantCulture) },
                new ReportMetric { Label = "Not Charted", Value = result.Statistics.NotCharted.ToString(CultureInfo.InvariantCulture) }
            }
        };
    }

    private static ReportSection BuildCranePositionsSection(PlacementResult result)
    {
        return new ReportSection
        {
            Title = "Crane Positions",
            Description = "Final optimized crane setup locations.",
            Tables =
            {
                BuildCranePositionsTable(result)
            }
        };
    }

    private static ReportTable BuildCranePositionsTable(PlacementResult result)
    {
        var table = new ReportTable
        {
            Name = "crane_positions",
            Title = "Crane Positions",
            Columns =
            {
                new ReportColumn { Key = "SetupIndex", Header = "Setup #", Type = ReportColumnType.Number },
                new ReportColumn { Key = "X", Header = "X", Unit = "m", Type = ReportColumnType.Number },
                new ReportColumn { Key = "Y", Header = "Y", Unit = "m", Type = ReportColumnType.Number },
                new ReportColumn { Key = "Z", Header = "Z", Unit = "m", Type = ReportColumnType.Number },
                new ReportColumn { Key = "LiftableCount", Header = "Liftable Count", Type = ReportColumnType.Number },
                new ReportColumn { Key = "NewLiftableCount", Header = "New Liftable Count", Type = ReportColumnType.Number }
            }
        };

        for (int i = 0; i < result.Positions.Count; i++)
        {
            var position = result.Positions[i];

            table.Rows.Add(new ReportRow
            {
                Values =
                {
                    ["SetupIndex"] = i + 1,
                    ["X"] = position.X,
                    ["Y"] = position.Y,
                    ["Z"] = position.Z,
                    ["LiftableCount"] = position.LiftableCount,
                    ["NewLiftableCount"] = position.NewLiftableCount
                }
            });
        }

        return table;
    }

    private static ReportSection BuildFailureBreakdownSection(PlacementResult result)
    {
        return new ReportSection
        {
            Title = "Failure Breakdown",
            Description = "Breakdown of lift evaluation categories.",
            Tables =
            {
                BuildFailureBreakdownTable(result)
            }
        };
    }

    private static ReportTable BuildFailureBreakdownTable(PlacementResult result)
    {
        var total = Math.Max(1, result.TotalElementCount);

        var rows = new List<(string Category, int Count, string Action)>
        {
            ("Liftable", result.Statistics.Liftable, "No action required."),
            ("OutOfRadius", result.Statistics.OutOfRadius, "Consider additional setup locations or larger crane radius."),
            ("Overweight", result.Statistics.Overweight, "Use larger crane/load chart configuration or split element if possible."),
            ("HeightFail", result.Statistics.HeightFail, "Review boom length, hook height, and lift geometry."),
            ("SpecialCase", result.Statistics.SpecialCase, "Review missing or uncertain engineering data."),
            ("NotCharted", result.Statistics.NotCharted, "Complete load chart data for this crane configuration.")
        };

        var table = new ReportTable
        {
            Name = "failure_breakdown",
            Title = "Failure Breakdown",
            Columns =
            {
                new ReportColumn { Key = "Category", Header = "Category" },
                new ReportColumn { Key = "Count", Header = "Count", Type = ReportColumnType.Number },
                new ReportColumn { Key = "PercentageOfTotal", Header = "Percentage Of Total", Unit = "%", Type = ReportColumnType.Percentage },
                new ReportColumn { Key = "SuggestedAction", Header = "Suggested Action" }
            }
        };

        foreach (var row in rows)
        {
            table.Rows.Add(new ReportRow
            {
                Values =
                {
                    ["Category"] = row.Category,
                    ["Count"] = row.Count,
                    ["PercentageOfTotal"] = row.Count * 100.0 / total,
                    ["SuggestedAction"] = row.Action
                }
            });
        }

        return table;
    }

    private static ReportSection BuildElementLiftDetailsSection(PlacementResult result)
    {
        return new ReportSection
        {
            Title = "Element Lift Details",
            Description = "Detailed lift evaluation results for elements assigned to crane setup positions.",
            Notes =
            {
                "For large projects, PDF export may truncate this table. CSV export contains the full table."
            },
            Tables =
            {
                BuildElementLiftDetailsTable(result)
            }
        };
    }

    private static ReportTable BuildElementLiftDetailsTable(PlacementResult result)
    {
        var table = new ReportTable
        {
            Name = "element_lift_details",
            Title = "Element Lift Details",
            Columns =
            {
                new ReportColumn { Key = "SetupIndex", Header = "Setup #", Type = ReportColumnType.Number },
                new ReportColumn { Key = "ElementId", Header = "Element ID" },
                new ReportColumn { Key = "ElementMark", Header = "Mark" },
                new ReportColumn { Key = "ElementType", Header = "Type" },
                new ReportColumn { Key = "Weight", Header = "Weight", Unit = "ton", Type = ReportColumnType.Number },
                new ReportColumn { Key = "CogX", Header = "COG X", Unit = "m", Type = ReportColumnType.Number },
                new ReportColumn { Key = "CogY", Header = "COG Y", Unit = "m", Type = ReportColumnType.Number },
                new ReportColumn { Key = "CogZ", Header = "COG Z", Unit = "m", Type = ReportColumnType.Number },
                new ReportColumn { Key = "Radius", Header = "Radius", Unit = "m", Type = ReportColumnType.Number },
                new ReportColumn { Key = "BoomLength", Header = "Boom Length", Unit = "m", Type = ReportColumnType.Number },
                new ReportColumn { Key = "RequiredCapacity", Header = "Required Capacity", Unit = "ton", Type = ReportColumnType.Number },
                new ReportColumn { Key = "AvailableCapacity", Header = "Available Capacity", Unit = "ton", Type = ReportColumnType.Number },
                new ReportColumn { Key = "SafetyMargin", Header = "Safety Margin", Unit = "ton", Type = ReportColumnType.Number },
                new ReportColumn { Key = "LiftStatus", Header = "Lift Status" },
                new ReportColumn { Key = "ReachStatus", Header = "Reach Status" },
                new ReportColumn { Key = "ChartStatus", Header = "Chart Status" },
                new ReportColumn { Key = "LookupMode", Header = "Lookup Mode" }
            }
        };

        var hasFinalAssignments = result.Positions
            .Any(position => position.ElementResults.Any(element => element.IsFinalAssignedToPoint));

        for (int setupIndex = 0; setupIndex < result.Positions.Count; setupIndex++)
        {
            var position = result.Positions[setupIndex];

            foreach (var element in position.ElementResults)
            {
                if (hasFinalAssignments && !element.IsFinalAssignedToPoint)
                    continue;

                table.Rows.Add(new ReportRow
                {
                    Values =
                    {
                        ["SetupIndex"] = setupIndex + 1,
                        ["ElementId"] = element.ElementId,
                        ["ElementMark"] = !string.IsNullOrWhiteSpace(element.CanonicalMark) ? element.CanonicalMark : (element.ElementMark ?? string.Empty),
                        ["ElementType"] = element.ElementType ?? string.Empty,
                        ["Weight"] = element.Weight,
                        ["CogX"] = element.CogX,
                        ["CogY"] = element.CogY,
                        ["CogZ"] = element.CogZ,
                        ["Radius"] = element.Radius,
                        ["BoomLength"] = element.BoomLength,
                        ["RequiredCapacity"] = element.RequiredCapacity,
                        ["AvailableCapacity"] = element.AvailableCapacity,
                        ["SafetyMargin"] = element.SafetyMargin,
                        ["LiftStatus"] = element.Status.ToString(),
                        ["ReachStatus"] = element.ReachStatus.ToString(),
                        ["ChartStatus"] = element.ChartStatus.ToString(),
                        ["LookupMode"] = element.LookupMode
                    }
                });
            }
        }

        return table;
    }
}

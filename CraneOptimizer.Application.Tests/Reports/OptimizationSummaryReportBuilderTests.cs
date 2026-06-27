using CraneOptimizer.Application.Optimization.GeneticAlgorithms.DTOs;
using CraneOptimizer.Application.Optimization.GeneticAlgorithms.Models;
using CraneOptimizer.Application.Reports.Builders;
using CraneOptimizer.Application.Reports.Inputs;
using CraneOptimizer.Application.Reports.Models;
using CraneOptimizer.Domain.Entities;

namespace CraneOptimizer.Application.Tests.Reports;

public class OptimizationSummaryReportBuilderTests
{
    [Fact]
    public void Build_ReturnsOptimizationSummaryDocument()
    {
        var builder = new OptimizationSummaryReportBuilder();

        var document = builder.Build(CreateInput());

        Assert.NotNull(document);
        Assert.Equal("optimization-summary", document.ReportId);
        Assert.Equal("Optimization Summary Report", document.Title);
        Assert.Equal("Project Alpha", document.Metadata.ProjectName);
        Assert.Equal("P-001", document.Metadata.ProjectId);
        Assert.Equal("model.ifc", document.Metadata.IfcFileName);
        Assert.Equal("LTM 1300", document.Metadata.CraneName);
        Assert.Equal("Engineer", document.Metadata.PreparedBy);
        Assert.Contains(document.Sections, section => section.Title == "Summary");
        Assert.Contains(document.Sections.SelectMany(section => section.Tables), table => table.Name == "crane_positions");
        Assert.Contains(document.Sections.SelectMany(section => section.Tables), table => table.Name == "failure_breakdown");
        Assert.Contains(document.Sections.SelectMany(section => section.Tables), table => table.Name == "element_lift_details");
        Assert.Contains(document.Sections.SelectMany(section => section.Metrics), metric => metric.Label == "Coverage");
    }

    [Fact]
    public void Build_CranePositionsRowsMatchPositionCount()
    {
        var document = new OptimizationSummaryReportBuilder().Build(CreateInput());

        var table = FindTable(document, "crane_positions");

        Assert.Equal(2, table.Rows.Count);
    }

    [Fact]
    public void Build_FailureBreakdownIncludesExpectedCategories()
    {
        var document = new OptimizationSummaryReportBuilder().Build(CreateInput());
        var table = FindTable(document, "failure_breakdown");
        var categories = table.Rows.Select(row => row.Values["Category"]).Cast<string>().ToHashSet(StringComparer.Ordinal);

        Assert.Contains("Liftable", categories);
        Assert.Contains("OutOfRadius", categories);
        Assert.Contains("Overweight", categories);
        Assert.Contains("HeightFail", categories);
        Assert.Contains("SpecialCase", categories);
        Assert.Contains("NotCharted", categories);
    }

    [Fact]
    public void Build_ElementDetailsIncludesOnlyFinalAssignments_WhenAnyFinalAssignmentExists()
    {
        var input = CreateInput();

        var document = new OptimizationSummaryReportBuilder().Build(input);
        var table = FindTable(document, "element_lift_details");

        Assert.Single(table.Rows);
        Assert.Equal("E-1", table.Rows[0].Values["ElementId"]);
    }

    [Fact]
    public void Build_ElementDetailsIncludesAllRows_WhenNoFinalAssignmentExists()
    {
        var input = CreateInput(hasFinalAssignment: false);

        var document = new OptimizationSummaryReportBuilder().Build(input);
        var table = FindTable(document, "element_lift_details");

        Assert.Equal(2, table.Rows.Count);
    }

    [Fact]
    public void Build_UsesCraneNameFallback_WhenCraneNavigationIsMissing()
    {
        var input = CreateInput();
        input.PlacementInput.CraneSpec = new CraneSpecification
        {
            Id = 42,
            Crane = null!
        };

        var document = new OptimizationSummaryReportBuilder().Build(input);

        Assert.Equal("Crane Specification #42", document.Metadata.CraneName);
    }

    private static ReportTable FindTable(ReportDocument document, string name)
    {
        return document.Sections
            .SelectMany(section => section.Tables)
            .Single(table => table.Name == name);
    }

    private static OptimizationReportInput CreateInput(bool hasFinalAssignment = true)
    {
        return new OptimizationReportInput
        {
            PreparedBy = "Engineer",
            PlacementInput = new PlacementInput
            {
                ProjectName = "Project Alpha",
                ProjectId = "P-001",
                IfcFileName = "model.ifc",
                ScenarioId = "scenario-1",
                ScopeDefinitionHash = "scope-hash",
                RequestedMaxCranePositions = 2,
                CraneSpec = new CraneSpecification
                {
                    Crane = new Crane
                    {
                        Name = "LTM 1300"
                    }
                }
            },
            PlacementResult = new PlacementResult
            {
                TotalElementCount = 4,
                LiftedElementCount = 3,
                CoverageRatio = 0.75,
                FitnessScore = 12.3456,
                Statistics = new LiftStatistics
                {
                    Liftable = 3,
                    OutOfRadius = 1,
                    Overweight = 2,
                    HeightFail = 3,
                    SpecialCase = 4,
                    NotCharted = 5
                },
                Positions =
                {
                    new CranePositionResult
                    {
                        X = 1,
                        Y = 2,
                        Z = 3,
                        NewLiftableCount = 1,
                        ElementResults =
                        {
                            CreateElement("E-1", hasFinalAssignment)
                        }
                    },
                    new CranePositionResult
                    {
                        X = 4,
                        Y = 5,
                        Z = 6,
                        NewLiftableCount = 1,
                        ElementResults =
                        {
                            CreateElement("E-2", false)
                        }
                    }
                }
            }
        };
    }

    private static ElementLiftResult CreateElement(string elementId, bool isFinalAssignedToPoint)
    {
        return new ElementLiftResult
        {
            ElementId = elementId,
            ElementMark = "M1",
            ElementType = "Column",
            Weight = 10.5,
            CogX = 1.1,
            CogY = 2.2,
            CogZ = 3.3,
            Radius = 12.5,
            BoomLength = 30,
            RequiredCapacity = 11,
            AvailableCapacity = 14,
            SafetyMargin = 3,
            Status = LiftStatus.Liftable,
            ReachStatus = ReachStatus.Reachable,
            ChartStatus = ChartStatus.ListedCapacityFound,
            LookupMode = "ConservativeStep",
            IsFinalAssignedToPoint = isFinalAssignedToPoint
        };
    }
}

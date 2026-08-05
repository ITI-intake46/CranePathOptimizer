using CraneOptimizer.Application.GAAnalysis;
using CraneOptimizer.Application.Optimization.GeneticAlgorithms.Engines.CraneConfiguration;
using CraneOptimizer.Application.Optimization.GeneticAlgorithms.Interfaces;
using CraneOptimizer.Application.Optimization.GeneticAlgorithms.Services;
using CraneOptimizer.Application.Optimization.GeneticAlgorithms.Snapshots;
using CraneOptimizer.Application.Services;
using CraneOptimizer.Domain.Entities;
using CraneOptimizer.Infrastructure.Contexts;
using CraneOptimizer.Infrastructure.GAAnalysis;
using CraneOptimizer.UI.Features.F8_GAOptimizer.Services;
using CraneOptimizer.UI.Features.F8_GAOptimizer.Views;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using System.Xml.Linq;

namespace CraneOptimizer.Application.Tests.GA;

public sealed class GaLiftAnalysisDependencyGraphTests
{
    [Fact]
    public void User_facing_name_is_lift_summary_in_analyze_only()
    {
        var analyze = File.ReadAllText(GetRepoFile(
            "CraneOptimizer.UI/Features/F8_GAOptimizer/Views/AnalyzeTabView.xaml"));
        var reports = File.ReadAllText(GetRepoFile(
            "CraneOptimizer.UI/Features/F12_Reports/Views/ReportsTabView.xaml"));
        var window = File.ReadAllText(GetRepoFile(
            "CraneOptimizer.UI/Features/F8_GAOptimizer/Views/GaLiftAnalysisWindow.xaml"));

        Assert.Contains("Content=\"Lift Summary\"", analyze, StringComparison.Ordinal);
        Assert.DoesNotContain("Lift Summary", reports, StringComparison.Ordinal);
        Assert.Contains(
            "Title=\"Lift Performance Summary\"",
            window,
            StringComparison.Ordinal);
        Assert.Contains(
            "Value=\"{Binding ProgressPercent, Mode=OneWay}\"",
            window,
            StringComparison.Ordinal);
        Assert.DoesNotContain("<DataGrid", window, StringComparison.Ordinal);
        Assert.DoesNotContain("Final lift outcomes", window, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("Technical evidence", window, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("GA performance", window, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("JSON", window, StringComparison.OrdinalIgnoreCase);
    }

    public static IEnumerable<object[]> SummaryUiContractCases()
    {
        var required = new[]
        {
            "Title=\"Lift Performance Summary\"",
            "Text=\"Lift Performance Summary\"",
            "Text=\"Executive Summary\"",
            "Text=\"Lift Result\"",
            "Text=\"Capacity Use\"",
            "Text=\"Failure Reasons\"",
            "Text=\"Engineering Inputs\"",
            "Text=\"Search &amp; Placement Summary\"",
            "Text=\"Recommended Action\"",
            "Text=\"Crane Alternatives\"",
            "Text=\"Limitations &amp; Safety Notes\"",
            "IsExpanded=\"False\"",
            "Text=\"Preparing Lift Summary…\"",
            "Text=\"Lift Summary could not be generated.\"",
            "Content=\"Try Again\"",
            "Content=\"Open Folder\"",
            "Content=\"Copy Summary\"",
            "Content=\"Close\"",
            "Open the folder containing this run’s saved Lift Summary.",
            "Copy the lifting result, utilization evidence, and recommended next action.",
            "Close Lift Summary.",
            "VerticalScrollBarVisibility=\"Auto\"",
            "HorizontalScrollBarVisibility=\"Disabled\"",
            "CanContentScroll=\"False\"",
            "IsDeferredScrollingEnabled=\"False\"",
            "PanningMode=\"VerticalOnly\"",
            "SmoothScrollBehavior.IsEnabled=\"True\"",
            "Summary.ExecutiveSummaryText",
            "Text=\"Total Lifts\"",
            "Text=\"Completed Lifts\"",
            "Text=\"Unresolved Lifts\"",
            "Text=\"Lift Coverage\"",
            "Text=\"Highest Capacity Use\"",
            "Text=\"Average Capacity Use\"",
            "Text=\"Smallest Capacity Reserve\"",
            "Text=\"Lifts Near Capacity Limit\"",
            "Summary.HighestCapacityUseText",
            "Summary.AverageCapacityUseText",
            "Summary.SmallestCapacityReserveText",
            "Summary.NearCapacityLimitText",
            "Text=\"Capacity Failures\"",
            "Text=\"Radius Failures\"",
            "Text=\"Boom or Height Failures\"",
            "Text=\"Load Chart Failures\"",
            "Text=\"Input Data Issues\"",
            "Text=\"Other Unresolved Issues\"",
            "Summary.CapacityFailuresText",
            "Summary.RadiusFailuresText",
            "Summary.BoomHeightFailuresText",
            "Summary.LoadChartFailuresText",
            "Summary.DominantFailureTitleText",
            "Summary.DominantFailureExplanationText",
            "Text=\"Safety Factor\"",
            "Text=\"Derating Ratio\"",
            "Text=\"Wire Length\"",
            "Text=\"Lifting Accessories Weight\"",
            "Text=\"Operating Mode\"",
            "Summary.SafetyFactorText",
            "Summary.DeratingRatioText",
            "Summary.WireLengthText",
            "Summary.LiftingAccessoriesWeightText",
            "Text=\"Candidate Points Generated\"",
            "Text=\"Valid Candidate Points\"",
            "Text=\"Rejected Candidate Points\"",
            "Text=\"Selected Crane Positions\"",
            "Text=\"Optimization Time\"",
            "Summary.PrimaryRecommendationText",
            "Summary.SecondaryRecommendations",
            "Summary.AlternativeRows",
            "SuccessfulPlanPreserved",
            "FailedLiftsRecovered",
            "Summary.Limitations",
            "Summary.SafetyNotice",
            "Review the assumptions and safety limitations used by this Lift Summary.",
            "FontFamily=\"{DynamicResource FontPrimary}\"",
            "Background=\"{DynamicResource SurfaceBrush}\"",
            "CornerRadius=\"8\""
        };
        foreach (var token in required)
            yield return new object[] { token, true };

        var forbidden = new[]
        {
            "Current Crane Adequacy",
            "Right-Sizing Assessment",
            "Alternative Screening",
            "Capacity P95",
            "Radius P95",
            "Boom P95",
            "95th percentile",
            "PercentileExplanation",
            "High-End Capacity Use",
            "High-End Radius Use",
            "High-End Boom Use",
            "LIFTS EVALUATED",
            "MAX CAPACITY USE",
            "MIN CAPACITY MARGIN",
            "NEAR-LIMIT SUCCESS",
            "RELIABLE CAPACITY FAILURES",
            "CHART EVIDENCE FAILURES",
            "Final lift outcomes",
            "LiftUnitId",
            "SourceElementGlobalId",
            "IFC ID",
            "Technical evidence",
            "GA performance",
            "JSON / provenance",
            "SchemaVersion",
            "AnalysisEngine",
            "Fingerprint",
            "Checksum",
            "Content=\"Cancel\"",
            "Content=\"Refresh\"",
            "Content=\"Rebuild\"",
            "Content=\"Save\"",
            "Open Analysis Folder",
            "Copy Executive Summary",
            "Text=\"Hook Weight\"",
            "Summary.HookWeightText",
            "Summary.AccessoriesWeightText",
            "<DataGrid"
        };
        foreach (var token in forbidden)
            yield return new object[] { token, false };
    }

    [Theory]
    [MemberData(nameof(SummaryUiContractCases))]
    public void Lift_summary_page_obeys_the_presentation_contract(string token, bool shouldExist)
    {
        var window = File.ReadAllText(GetRepoFile(
            "CraneOptimizer.UI/Features/F8_GAOptimizer/Views/GaLiftAnalysisWindow.xaml"));

        if (shouldExist)
            Assert.Contains(token, window, StringComparison.OrdinalIgnoreCase);
        else
            Assert.DoesNotContain(token, window, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void Safety_disclaimer_is_normal_text_without_a_nested_container()
    {
        string path = GetRepoFile(
            "CraneOptimizer.UI/Features/F8_GAOptimizer/Views/GaLiftAnalysisWindow.xaml");
        XDocument document = XDocument.Load(path);
        XElement safetyText = Assert.Single(
            document.Descendants()
                .Where(element =>
                    element.Name.LocalName == "TextBlock" &&
                    string.Equals(
                        (string?)element.Attribute("Text"),
                        "{Binding Summary.SafetyNotice}",
                        StringComparison.Ordinal)));

        Assert.Equal("StackPanel", safetyText.Parent?.Name.LocalName);
        Assert.Null(safetyText.Attribute("Background"));
        Assert.Null(safetyText.Attribute("BorderBrush"));
        Assert.Null(safetyText.Attribute("CornerRadius"));
        Assert.Equal("Wrap", (string?)safetyText.Attribute("TextWrapping"));
        Assert.Equal(
            "{DynamicResource TextSecondaryBrush}",
            (string?)safetyText.Attribute("Foreground"));
    }

    [Fact]
    public void Lift_summary_view_model_has_no_per_lift_or_synchronous_wait_surface()
    {
        var viewModel = File.ReadAllText(GetRepoFile(
            "CraneOptimizer.UI/Features/F8_GAOptimizer/ViewModels/GaLiftAnalysisViewModel.cs"));

        Assert.DoesNotContain("LiftResults", viewModel, StringComparison.Ordinal);
        Assert.DoesNotContain("SetupSummaries", viewModel, StringComparison.Ordinal);
        Assert.DoesNotContain(".Completion.Result", viewModel, StringComparison.Ordinal);
        Assert.DoesNotContain("Task.Result", viewModel, StringComparison.Ordinal);
        Assert.DoesNotContain(".Wait(", viewModel, StringComparison.Ordinal);
        Assert.DoesNotContain("GetAwaiter().GetResult", viewModel, StringComparison.Ordinal);
        Assert.DoesNotContain("Dispatcher.Invoke(", viewModel, StringComparison.Ordinal);
        Assert.DoesNotContain("CancelCommand", viewModel, StringComparison.Ordinal);
        Assert.DoesNotContain("RefreshCommand", viewModel, StringComparison.Ordinal);
        Assert.DoesNotContain("RebuildCommand", viewModel, StringComparison.Ordinal);
        Assert.Contains(".MapAsync(analysis, cancellationToken)", viewModel, StringComparison.Ordinal);
        Assert.DoesNotContain("_mapper.Map(", viewModel, StringComparison.Ordinal);
    }

    [Fact]
    public void Smooth_scroll_is_short_bounded_and_updates_the_existing_target()
    {
        var behavior = File.ReadAllText(GetRepoFile(
            "CraneOptimizer.UI/Shared/Behaviors/SmoothScrollBehavior.cs"));

        Assert.Contains("TimeSpan.FromMilliseconds(150)", behavior, StringComparison.Ordinal);
        Assert.Contains("state.IsAnimating ? state.TargetOffset : currentOffset", behavior, StringComparison.Ordinal);
        Assert.Contains("Math.Clamp", behavior, StringComparison.Ordinal);
        Assert.Contains("HandoffBehavior.SnapshotAndReplace", behavior, StringComparison.Ordinal);
        Assert.DoesNotContain("DispatcherTimer", behavior, StringComparison.Ordinal);
        Assert.DoesNotContain("Task.Delay", behavior, StringComparison.Ordinal);
    }

    [Fact]
    public void Folder_and_copy_actions_are_nonblocking_and_resolver_driven()
    {
        var actions = File.ReadAllText(GetRepoFile(
            "CraneOptimizer.UI/Features/F8_GAOptimizer/Services/GaLiftAnalysisPresentationActions.cs"));

        Assert.Contains("_pathResolver.Resolve(analysis.ProjectGuid, analysis.OptimizationRunId)", actions, StringComparison.Ordinal);
        Assert.Contains("Directory.Exists(paths.RunDirectory)", actions, StringComparison.Ordinal);
        Assert.Contains("Task.Run", actions, StringComparison.Ordinal);
        Assert.Contains("Clipboard.SetText(summary)", actions, StringComparison.Ordinal);
        Assert.DoesNotContain("MessageBox", actions, StringComparison.Ordinal);
        Assert.DoesNotContain("Path.Combine", actions, StringComparison.Ordinal);
        Assert.DoesNotContain("throw;", actions, StringComparison.Ordinal);
    }

    [Fact]
    public void Historical_summary_routing_requires_the_exact_selected_run()
    {
        var viewModel = File.ReadAllText(GetRepoFile(
            "CraneOptimizer.UI/Features/F8_GAOptimizer/ViewModels/CranePlacementViewModel.cs"));

        Assert.Contains("var selectedRunId = selected.OptimizationRunId;", viewModel, StringComparison.Ordinal);
        Assert.Contains("current.Run.OptimizationRunId", viewModel, StringComparison.Ordinal);
        Assert.Contains("resolved.Snapshot.Run.OptimizationRunId", viewModel, StringComparison.Ordinal);
        Assert.Contains(".LoadAsync(context.ProjectGuid, selectedRunId, token)", viewModel, StringComparison.Ordinal);
        Assert.Contains("saved.OptimizationRunId", viewModel, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Production_feature_graph_validates_runs_a_supervised_scope_and_creates_transient_window()
    {
        var root = Path.Combine(Path.GetTempPath(), "CraneOptimizer.Tests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(root);
        var services = CreateServices(root);
        using var provider = services.BuildServiceProvider(new ServiceProviderOptions
        {
            ValidateOnBuild = true,
            ValidateScopes = true
        });
        await EnsureDatabaseAsync(provider);

        Assert.NotNull(provider.GetRequiredService<IGaLiftAnalysisStore>());
        Assert.NotNull(provider.GetRequiredService<IGaCraneCatalogProvider>());
        Assert.NotNull(provider.GetRequiredService<IGaLiftAnalysisWindowService>());
        using (var scope = provider.CreateScope())
            Assert.NotNull(scope.ServiceProvider.GetRequiredService<IGaLiftAnalysisService>());

        var coordinator = provider.GetRequiredService<IGaLiftAnalysisJobCoordinator>();
        var snapshot = CreateMinimalSnapshot();
        var jobs = await Task.WhenAll(Enumerable.Range(0, 24)
            .Select(_ => Task.Run(() => coordinator.StartOrGet(snapshot))));
        var job = jobs[0];
        Assert.All(jobs, duplicate => Assert.Same(job, duplicate));
        await job.Completion;
        Assert.Equal(GaLiftAnalysisJobStatus.Completed, job.Status);
        Assert.NotNull(job.Result);

        var completion = new TaskCompletionSource<Exception?>(
            TaskCreationOptions.RunContinuationsAsynchronously);
        var thread = new Thread(() =>
        {
            try
            {
                var factory = provider.GetRequiredService<IGaLiftAnalysisWindowFactory>();
                var window = factory.CreateSaved(new GaLiftAnalysisResult
                {
                    ProjectGuid = "project-1",
                    OptimizationRunId = "run-1"
                });
                window.Close();
                completion.SetResult(null);
            }
            catch (Exception ex)
            {
                completion.SetResult(ex);
            }
        });
        thread.SetApartmentState(ApartmentState.STA);
        thread.Start();

        Assert.Null(await completion.Task);
    }

    [Fact]
    public async Task Catalog_provider_projects_load_chart_into_application_dtos()
    {
        var root = Path.Combine(Path.GetTempPath(), "CraneOptimizer.Tests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(root);
        using var provider = CreateServices(root).BuildServiceProvider(
            new ServiceProviderOptions { ValidateScopes = true });
        await EnsureDatabaseAsync(provider);
        await using (var scope = provider.CreateAsyncScope())
        {
            var context = scope.ServiceProvider.GetRequiredService<CraneDbContext>();
            var crane = new Crane { Name = "Test crane", Manufacturer = "Test" };
            var specification = new CraneSpecification
            {
                Crane = crane,
                MaxCapacity = 100,
                MaxRadius = 40,
                MinRadius = 2,
                MaxBoomLength = 50,
                MinBoomLength = 10
            };
            specification.LoadChart.Add(new TargetPoint
            {
                Radius = 10,
                BoomLength = 20,
                Capacity = 25,
                CraneSpecification = specification
            });
            context.CraneSpecifications.Add(specification);
            await context.SaveChangesAsync();
        }

        var candidates = await provider.GetRequiredService<IGaCraneCatalogProvider>()
            .GetCandidatesAsync(currentCraneSpecificationId: null);

        var candidate = Assert.Single(candidates);
        Assert.Equal("Test crane", candidate.Specification.CraneName);
        Assert.Equal(25, Assert.Single(candidate.Specification.LoadChart).Capacity);
    }

    private static ServiceCollection CreateServices(string root)
    {
        var services = new ServiceCollection();
        services.AddSingleton<IActiveProjectPathProvider>(new FixedProjectPathProvider(root));
        services.AddDbContextFactory<CraneDbContext>(options =>
            options.UseSqlite($"Data Source={Path.Combine(root, "catalog.db")}"));
        services.AddScoped<ILoadChartCapacityResolver, LoadChartCapacityResolver>();
        services.AddScoped<ICraneConfigurationEngine, CraneConfigurationEngine>();
        services.AddGaLiftAnalysisFeature();
        return services;
    }

    private static async Task EnsureDatabaseAsync(IServiceProvider provider)
    {
        await using var scope = provider.CreateAsyncScope();
        var context = scope.ServiceProvider.GetRequiredService<CraneDbContext>();
        await context.Database.EnsureCreatedAsync();
    }

    private static string GetRepoFile(string relativePath)
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory != null)
        {
            var candidate = Path.Combine(
                directory.FullName,
                relativePath.Replace('/', Path.DirectorySeparatorChar));
            if (File.Exists(candidate))
                return candidate;
            directory = directory.Parent;
        }

        throw new FileNotFoundException($"Could not locate repository file '{relativePath}'.");
    }

    private static GaResultSnapshotDto CreateMinimalSnapshot() => new()
    {
        SchemaVersion = 3,
        ApplicationVersion = "test",
        Run = new GaRunIdentitySnapshotDto
        {
            OptimizationRunId = "run-1",
            Status = "Completed",
            SnapshotSchemaVersion = 3
        },
        Project = new GaProjectSnapshotDto
        {
            ProjectGuid = "project-1",
            ProjectName = "Test project",
            ProjectRelativeSnapshotPath = "Reports/GA/run-1/ga_result.json"
        },
        Model = new GaModelSnapshotDto
        {
            ModelFingerprint = new string('a', 64),
            OptimizationInputFingerprint = new string('b', 64),
            OptimizationInputFingerprintVersion = 3,
            OptimizationInputFingerprintComponents =
                new Dictionary<string, string>(StringComparer.Ordinal)
        },
        Crane = new GaCraneSnapshotDto { CraneName = "Current crane" },
        Summary = new GaRunSummarySnapshotDto(),
        Capabilities = new GaSnapshotCapabilitiesDto
        {
            SourceKind = "ProductionJson"
        }
    };

    private sealed class FixedProjectPathProvider : IActiveProjectPathProvider
    {
        private readonly string _root;

        public FixedProjectPathProvider(string root)
        {
            _root = root;
        }

        public string? GetActiveProjectPath() => _root;
    }
}

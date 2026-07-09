using CraneOptimizer.AI.Host;
using CraneOptimizer.AI.Services;
using CraneOptimizer.AI.Workflow;
using Xunit;

namespace CraneOptimizer.Application.Tests.AI
{
    public class AiMissionControlFeatureTests
    {
        [Fact]
        public void MissionControl_BuildsVisibleDiagnosisRecoveryPlanAndWhatIfAdvice()
        {
            var readinessService = new WorkflowReadinessService();
            var context = BuildBlockedContext();
            var readiness = readinessService.Evaluate(context, new UiInteractionContextService());
            var mission = new AiMissionControlService().Build(context, readiness, "ANALYZE");

            Assert.Contains("optimizer blocked", mission.DiagnosisTitle, StringComparison.OrdinalIgnoreCase);
            Assert.Equal("Blocked", mission.DiagnosisStatus);
            Assert.Contains("zero trusted", mission.RootCause, StringComparison.OrdinalIgnoreCase);
            Assert.Contains("Export Element List", mission.RecommendedAction, StringComparison.OrdinalIgnoreCase);
            Assert.Contains("AI Recovery Plan", mission.RecoveryPlan, StringComparison.OrdinalIgnoreCase);
            Assert.Contains("data readiness", mission.WhatIfAdvisor, StringComparison.OrdinalIgnoreCase);
            Assert.Contains("report language", mission.ReportNarrative, StringComparison.OrdinalIgnoreCase);
            Assert.Contains(mission.Insights, card => card.Title.Contains("Weight Trust", StringComparison.OrdinalIgnoreCase));
            Assert.Contains(mission.Insights, card => card.Title.Contains("GA Feasibility", StringComparison.OrdinalIgnoreCase));
        }

        [Fact]
        public void EvidenceTextBuilder_ProducesAllMissionControlArtifactsWithoutRelaxingSafetyGates()
        {
            var readinessService = new WorkflowReadinessService();
            var context = BuildBlockedContext();
            var readiness = readinessService.Evaluate(context, new UiInteractionContextService());

            var recovery = AiWorkflowEvidenceTextBuilder.BuildRecoveryPlan(context, readiness, "IFC MODEL");
            var whatIf = AiWorkflowEvidenceTextBuilder.BuildWhatIfAdvisorBrief(context, readiness);
            var report = AiWorkflowEvidenceTextBuilder.BuildAiReportNarrativeDraft(context, readiness);
            var qaPack = AiWorkflowEvidenceTextBuilder.BuildQaEvidencePack(context, readiness, "IFC MODEL");

            Assert.Contains("BIM/export owner", recovery, StringComparison.OrdinalIgnoreCase);
            Assert.Contains("not crane reach or capacity", whatIf, StringComparison.OrdinalIgnoreCase);
            Assert.Contains("zero trusted GA-ready grouped lift candidates", report, StringComparison.OrdinalIgnoreCase);
            Assert.Contains("Readiness checklist", qaPack, StringComparison.OrdinalIgnoreCase);
            Assert.Contains("What-if advisor", qaPack, StringComparison.OrdinalIgnoreCase);
            Assert.DoesNotContain("bypass", recovery.Replace("do not bypass", string.Empty, StringComparison.OrdinalIgnoreCase), StringComparison.OrdinalIgnoreCase);
            Assert.Contains("does not certify", report, StringComparison.OrdinalIgnoreCase);
        }



        [Fact]
        public void MissionControl_NoIfcModelPrioritizesLoadIfcBeforeGaCandidateAdvice()
        {
            var readinessService = new WorkflowReadinessService();
            var context = new AiWorkflowContextSnapshot
            {
                HasProject = true,
                CurrentProjectName = "Empty project",
                ElementCount = 0,
                ZoneCount = 3,
                CraneCount = 0,
                OptimizationResult = new AiOptimizationResultSnapshot { HasResult = false }
            };

            var readiness = readinessService.Evaluate(context, new UiInteractionContextService());
            var mission = new AiMissionControlService().Build(context, readiness, "CRANES");

            Assert.Contains("IFC model not loaded", mission.DiagnosisTitle, StringComparison.OrdinalIgnoreCase);
            Assert.Contains("Load IFC", mission.RecommendedAction, StringComparison.OrdinalIgnoreCase);
            Assert.DoesNotContain("Export Element List", mission.RecommendedAction, StringComparison.OrdinalIgnoreCase);
            Assert.DoesNotContain("The IFC is loaded", mission.RootCause, StringComparison.OrdinalIgnoreCase);
        }

        [Fact]
        public void MissionControl_IncludesIfcMetadataIntelligenceCard()
        {
            var readinessService = new WorkflowReadinessService();
            var context = BuildBlockedContext();
            var readiness = readinessService.Evaluate(context, new UiInteractionContextService());
            var mission = new AiMissionControlService().Build(context, readiness, "IFC MODEL");

            Assert.Contains(mission.Insights, card => card.Title.Contains("IFC Metadata", StringComparison.OrdinalIgnoreCase));
        }

        [Fact]
        public void AiShowcaseService_BuildsVisibleAiMakeoverArtifacts()
        {
            var readinessService = new WorkflowReadinessService();
            var context = BuildBlockedContext();
            var readiness = readinessService.Evaluate(context, new UiInteractionContextService());
            var mission = new AiMissionControlService().Build(context, readiness, "IFC MODEL");
            var showcase = new AiShowcaseService().Build(context, readiness, mission, "IFC MODEL");

            Assert.Contains("AI Studio", showcase.HeroHeadline, StringComparison.OrdinalIgnoreCase);
            Assert.Contains("live capabilities", showcase.PowerScoreLabel, StringComparison.OrdinalIgnoreCase);
            Assert.Contains("Readiness Sentinel", showcase.AgentStackSummary, StringComparison.OrdinalIgnoreCase);
            Assert.Contains(showcase.Capabilities, capability => capability.Title.Contains("Lift-Unit Trust", StringComparison.OrdinalIgnoreCase));
            Assert.Contains(showcase.Capabilities, capability => capability.Title.Contains("ModelOps", StringComparison.OrdinalIgnoreCase));
            Assert.Contains(showcase.DemoPrompts, prompt => prompt.Label.Contains("What-if", StringComparison.OrdinalIgnoreCase));
            Assert.Contains(showcase.DecisionTrace, item => item.Contains("Classified workflow", StringComparison.OrdinalIgnoreCase));
            Assert.Contains("AI Makeover Showcase Brief", showcase.PresentationBrief, StringComparison.OrdinalIgnoreCase);
            Assert.DoesNotContain("bypass", showcase.PresentationBrief.Replace("never suggests bypassing", string.Empty, StringComparison.OrdinalIgnoreCase), StringComparison.OrdinalIgnoreCase);
        }

        [Fact]
        public void CompanionPanel_ExposesMissionControlSurfacesAndActionButtons()
        {
            var root = FindRepositoryRoot();
            Assert.NotNull(root);

            var companionXaml = File.ReadAllText(Path.Combine(root!, "CraneOptimizer.AI", "Companion", "Views", "AiCompanionPanel.xaml"));
            var companionVm = File.ReadAllText(Path.Combine(root!, "CraneOptimizer.AI", "Companion", "ViewModels", "AiCompanionViewModel.cs"));
            var softwareContext = File.ReadAllText(Path.Combine(root!, "CraneOptimizer.AI", "Services", "StaticAiSoftwareContextService.cs"));

            Assert.Contains("AI Assist", companionXaml);
            Assert.Contains("PRIMARY RECOMMENDATION", companionXaml);
            Assert.Contains("AiAssistRailButton", companionXaml);
            Assert.Contains("<ColumnDefinition Width=\"34\" />", companionXaml);
            Assert.Contains("RotateTransform Angle=\"-90\"", companionXaml);
            Assert.Contains("PreviewMouseWheel=\"SectionRail_PreviewMouseWheel\"", companionXaml);
            Assert.Contains("VerticalScrollBarVisibility=\"Hidden\"", companionXaml);
            Assert.DoesNotContain("Text=\"SECTION\"", companionXaml);
            Assert.DoesNotContain("Text=\"Talk\"", companionXaml);
            Assert.DoesNotContain("PINNED QUICK ACTIONS", companionXaml);
            Assert.DoesNotContain("TOOL TABS", companionXaml);
            Assert.Contains("CommandCenterStateLabel", companionXaml);
            Assert.Contains("CommandParameter=\"Chat\"", companionXaml);
            Assert.Contains("CommandParameter=\"Mission\"", companionXaml);
            Assert.Contains("CommandParameter=\"BIM\"", companionXaml);
            Assert.Contains("CommandParameter=\"Optimize\"", companionXaml);
            Assert.Contains("CommandParameter=\"Report\"", companionXaml);
            Assert.Contains("CommandParameter=\"AI Studio\"", companionXaml);
            Assert.Contains("AI MISSION CONTROL", companionXaml);
            Assert.Contains("AI STUDIO", companionXaml);
            Assert.Contains("Capabilities", companionXaml);
            Assert.Contains("Decision trace", companionXaml);
            Assert.Contains("Count slabs", companionXaml);
            Assert.Contains("Category summary", companionXaml);
            Assert.Contains("ASK CURRENT MODEL", companionXaml);
            Assert.Contains("SelectCommandCenterTabCommand", companionVm);
            Assert.Contains("FocusChatForSubmittedPrompt", companionVm);
            Assert.Contains("SelectedCommandCenterTab = \"Chat\"", companionVm);
            Assert.DoesNotContain("SelectedCommandCenterTab = intentDecision.PreferredSection", companionVm);
            Assert.DoesNotContain("SelectedCommandCenterTab = routeDecision.PreferredSection", companionVm);
            var companionCodeBehind = File.ReadAllText(Path.Combine(root!, "CraneOptimizer.AI", "Companion", "Views", "AiCompanionPanel.xaml.cs"));
            Assert.Contains("SectionRail_PreviewMouseWheel", companionCodeBehind);
            Assert.Contains("AI Studio", companionCodeBehind);
            Assert.Contains("DispatcherPriority.ContextIdle", companionCodeBehind);
            Assert.Contains("SelectedCommandCenterTab, \"Chat\"", companionCodeBehind);
            Assert.Contains("ScrollBodyToEnd();", companionCodeBehind);
            Assert.Contains("ChatTabVisibility", companionVm);
            Assert.Contains("IsChatSectionSelected", companionVm);
            Assert.Contains("IsAiStudioSectionSelected", companionVm);
            Assert.Contains("BimTabVisibility", companionVm);
            Assert.Contains("OptimizeTabVisibility", companionVm);
            Assert.Contains("CopyRecoveryPlanCommand", companionVm);
            Assert.Contains("CopyWhatIfAdvisorCommand", companionVm);
            Assert.Contains("CopyQaEvidencePackCommand", companionVm);
            Assert.Contains("CopyAiShowcaseBriefCommand", companionVm);
            Assert.Contains("AiShowcaseService", companionVm);
            Assert.Contains("Generate BIM exporter request", companionVm);
            Assert.Contains("AI Assist / Mission Control", softwareContext);
            Assert.Contains("slim section-rail navigator", softwareContext);
            Assert.Contains("mouse-wheel section switching", softwareContext);
            Assert.Contains("scrollbar-sized", softwareContext);
        }

        private static AiWorkflowContextSnapshot BuildBlockedContext()
        {
            return new AiWorkflowContextSnapshot
            {
                HasProject = true,
                CurrentProjectName = "Blocked IFC Test",
                ElementCount = 362,
                ZoneCount = 1,
                CraneCount = 1,
                HasLoadChartEvidence = true,
                OptimizationResult = new AiOptimizationResultSnapshot { HasResult = false },
                ScreenContexts = new[]
                {
                    new AiScreenContextSnapshot
                    {
                        ScreenName = "IFC import readiness",
                        Stage = "GA blocked — no trusted lift units",
                        Summary = "IFC is loaded but no trusted GA-ready lift units exist.",
                        Issues = new[]
                        {
                            new AiContextIssueSnapshot
                            {
                                Code = "GaCannotStartNoCandidates",
                                Severity = "Error",
                                Area = "GA pre-run",
                                Evidence = "IFC is loaded; zones=1; cranes=1; 0 trusted GA-ready grouped lift units.",
                                Recommendation = "Do not bypass the optimizer gate. Review/export explicit BIM mass or weight quantities.",
                                NextClick = "IFC MODEL → Export Element List / trusted weight-source review",
                                AffectedCount = 362
                            },
                            new AiContextIssueSnapshot
                            {
                                Code = "EstimatedWeightBlockedFromGA",
                                Severity = "Error",
                                Area = "Weight",
                                Evidence = "Estimated fallback weight is used and strictly blocked from GA.",
                                Recommendation = "Provide explicit quantities so elements are not blocked from GA.",
                                NextClick = "IFC MODEL → Export Element List / trusted weight-source review",
                                AffectedCount = 362
                            }
                        }
                    }
                }
            };
        }

        private static string? FindRepositoryRoot()
        {
            var current = new DirectoryInfo(AppContext.BaseDirectory);
            while (current != null)
            {
                if (File.Exists(Path.Combine(current.FullName, "CraneOptimizer.sln")))
                    return current.FullName;
                current = current.Parent;
            }

            current = new DirectoryInfo(Directory.GetCurrentDirectory());
            while (current != null)
            {
                if (File.Exists(Path.Combine(current.FullName, "CraneOptimizer.sln")))
                    return current.FullName;
                current = current.Parent;
            }

            return null;
        }
    }
}

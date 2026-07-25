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
        public void CompanionPanel_ExposesSlimVerticalMissionControlSurfacesAndGovernedActions()
        {
            var root = FindRepositoryRoot();
            Assert.NotNull(root);

            var companionXaml = File.ReadAllText(Path.Combine(root!, "CraneOptimizer.AI", "Companion", "Views", "AiCompanionPanel.xaml"));
            var companionCodeBehind = File.ReadAllText(Path.Combine(root!, "CraneOptimizer.AI", "Companion", "Views", "AiCompanionPanel.xaml.cs"));
            var companionVm = File.ReadAllText(Path.Combine(root!, "CraneOptimizer.AI", "Companion", "ViewModels", "AiCompanionViewModel.cs"));
            var mainWindowXaml = File.ReadAllText(Path.Combine(root!, "CraneOptimizer.UI", "Shell", "Views", "MainWindow.xaml"));
            var mainWindowCode = File.ReadAllText(Path.Combine(root!, "CraneOptimizer.UI", "Shell", "Views", "MainWindow.xaml.cs"));
            var softwareContext = File.ReadAllText(Path.Combine(root!, "CraneOptimizer.AI", "Services", "StaticAiSoftwareContextService.cs"));

            Assert.Contains("MinWidth=\"330\"", companionXaml);
            Assert.Contains("AiAssistRailButton", companionXaml);
            Assert.Contains("x:Name=\"SectionRail\"", companionXaml);
            Assert.Contains("x:Name=\"SectionRailNavigation\"", companionXaml);
            Assert.Contains("Rows=\"6\"", companionXaml);
            Assert.Contains("PreviewKeyDown=\"SectionRail_PreviewKeyDown\"", companionXaml);
            Assert.Contains("PreviewMouseWheel=\"SectionRail_PreviewMouseWheel\"", companionXaml);
            Assert.Contains("RotateTransform Angle=\"90\"", companionXaml);
            Assert.Contains("Width=\"30\"", companionXaml);
            Assert.Contains("VerticalScrollBarVisibility=\"Auto\"", companionXaml);
            Assert.DoesNotContain("AiAssistNavButton", companionXaml);
            Assert.DoesNotContain("x:Name=\"SectionNavigation\"", companionXaml);
            Assert.DoesNotContain("PRIMARY RECOMMENDATION", companionXaml);
            Assert.Contains("Text=\"NEXT STEP\"", companionXaml);
            Assert.Contains("Text=\"VERIFIED\"", companionXaml);
            Assert.Contains("x:Name=\"ContextToggle\"", companionXaml);
            Assert.Contains("PopupAnimation=\"Fade\"", companionXaml);
            Assert.Contains("Read-only tools · approval gated", companionXaml);
            Assert.Contains("CommandCenterStatusTitle", companionXaml);
            Assert.Contains("CommandCenterProjectLabel", companionXaml);
            Assert.Contains("NextActionTitle", companionXaml);
            Assert.Contains("x:Name=\"ConversationItemsControl\"", companionXaml);
            Assert.Contains("x:Name=\"JumpToLatestButton\"", companionXaml);
            Assert.Contains("ScrollChanged=\"BodyScrollViewer_ScrollChanged\"", companionXaml);
            Assert.Contains("ToggleDetailsCommand", companionXaml);
            Assert.Contains("DisplayBody", companionXaml);
            Assert.Contains("Content=\"Latest ↓\"", companionXaml);
            Assert.Contains("<Setter Property=\"LineHeight\" Value=\"19\" />", companionXaml);
            Assert.Contains("<Setter Property=\"TextAlignment\" Value=\"Left\" />", companionXaml);
            Assert.DoesNotContain("TextAlignment=\"Justify\"", companionXaml);
            Assert.Contains("CommandParameter=\"Chat\"", companionXaml);
            Assert.Contains("CommandParameter=\"Mission\"", companionXaml);
            Assert.Contains("CommandParameter=\"BIM\"", companionXaml);
            Assert.Contains("CommandParameter=\"Optimize\"", companionXaml);
            Assert.Contains("CommandParameter=\"Report\"", companionXaml);
            Assert.Contains("CommandParameter=\"AI Studio\"", companionXaml);
            Assert.Contains("Content=\"Open Full Studio\"", companionXaml);
            Assert.Contains("Text=\"Analyze result\"", companionXaml);
            Assert.Contains("Text=\"Ask local model\"", companionXaml);
            Assert.Contains("Capabilities", companionXaml);
            Assert.Contains("Decision trace", companionXaml);
            Assert.Contains("Count slabs", companionXaml);
            Assert.Contains("Content=\"Categories\"", companionXaml);
            Assert.Contains("AutomationProperties.Name=\"Copy latest AI answer\"", companionXaml);

            Assert.Contains("SelectCommandCenterTabCommand", companionVm);
            Assert.Contains("OpenFullStudioCommand", companionVm);
            Assert.Contains("PrepareLocalOllamaQuestionCommand", companionVm);
            Assert.Contains("FocusChatForSubmittedPrompt", companionVm);
            Assert.Contains("SelectedCommandCenterTab = \"Chat\"", companionVm);
            Assert.DoesNotContain("SelectedCommandCenterTab = intentDecision.PreferredSection", companionVm);
            Assert.DoesNotContain("SelectedCommandCenterTab = routeDecision.PreferredSection", companionVm);
            Assert.Contains("CopyRecoveryPlanCommand", companionVm);
            Assert.Contains("CopyWhatIfAdvisorCommand", companionVm);
            Assert.Contains("CopyQaEvidencePackCommand", companionVm);
            Assert.Contains("CopyAiShowcaseBriefCommand", companionVm);
            Assert.Contains("AiShowcaseService", companionVm);
            Assert.Contains("Generate BIM exporter request", companionVm);
            Assert.Contains("BuildCompactPreview", companionVm);
            Assert.Contains("LimitAtReadingBoundary", companionVm);
            Assert.Contains("DetailsToggleLabel", companionVm);
            Assert.Contains("BuildCompactNextAction", companionVm);

            Assert.Contains("SectionRail_PreviewKeyDown", companionCodeBehind);
            Assert.Contains("SectionRail_PreviewMouseWheel", companionCodeBehind);
            Assert.Contains("IsSectionRailExpanded", companionCodeBehind);
            Assert.Contains("DoubleAnimation", companionCodeBehind);
            Assert.Contains("DispatcherPriority.ContextIdle", companionCodeBehind);
            Assert.Contains("_activeSectionName, \"Chat\"", companionCodeBehind);
            Assert.Contains("_pendingPromptAnchor = newestUserMessage;", companionCodeBehind);
            Assert.Contains("var anchor = _pendingPromptAnchor ?? newestAssistantMessage;", companionCodeBehind);
            Assert.Contains("JumpToLatestButton_Click", companionCodeBehind);
            Assert.Contains("RestoreSectionScrollPosition", companionCodeBehind);
            Assert.Contains("LatestMessageThreshold", companionCodeBehind);
            Assert.Contains("_followLatestContent", companionCodeBehind);
            Assert.Contains("IsNearLatestMessage", companionCodeBehind);
            Assert.DoesNotContain("if (string.Equals(_viewModel?.SelectedCommandCenterTab, \"Chat\", StringComparison.OrdinalIgnoreCase))", companionCodeBehind);

            Assert.Contains("AutomationProperties.Name=\"Resize right workspace panel\"", mainWindowXaml);
            Assert.Contains("x:Name=\"RightWorkspaceColumn\" Width=\"360\" MinWidth=\"330\" MaxWidth=\"380\"", mainWindowXaml);
            Assert.DoesNotContain("SelectionChanged=\"RightWorkspaceTabs_SelectionChanged\"", mainWindowXaml);
            Assert.DoesNotContain("AiAssistPreferredWidth", mainWindowCode);
            Assert.DoesNotContain("ApplyAiAssistWorkspaceWidth", mainWindowCode);
            Assert.DoesNotContain("RestoreCranePropertiesWorkspaceWidth", mainWindowCode);

            Assert.Contains("AI Assist / Mission Control", softwareContext);
            Assert.Contains("slim, evidence-first sidecar", softwareContext);
            Assert.Contains("rotated vertical section rail", softwareContext);
            Assert.Contains("same 330 to 380 px right dock", softwareContext);
            Assert.Contains("context inspector", softwareContext);
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

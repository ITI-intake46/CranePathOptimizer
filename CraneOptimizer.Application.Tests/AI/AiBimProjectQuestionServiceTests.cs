using CraneOptimizer.AI.Companion.ViewModels;
using CraneOptimizer.AI.Host;
using CraneOptimizer.AI.Services;
using Xunit;

namespace CraneOptimizer.Application.Tests.AI
{
    public class AiBimProjectQuestionServiceTests
    {
        [Theory]
        [InlineData("How many slabs are there?", "97", "Slab")]
        [InlineData("How many beams are there?", "80", "Beam")]
        [InlineData("count columns", "72", "Column")]
        [InlineData("number of walls", "179", "Wall")]
        [InlineData("how many unknown elements?", "1,950", "Unknown")]
        public void AnswersCategoryCountsFromResolvedCategorySummary(string question, string expectedCount, string expectedLabel)
        {
            var context = BuildModelContext();

            var handled = AiBimProjectQuestionService.TryBuildAnswer(context, question, "IFC MODEL", out var answer, out var source);

            Assert.True(handled);
            Assert.Contains(expectedCount, answer);
            Assert.Contains(expectedLabel, answer, StringComparison.OrdinalIgnoreCase);
            Assert.Contains("deterministic live project facts", answer, StringComparison.OrdinalIgnoreCase);
            Assert.Contains("Model revision", answer, StringComparison.OrdinalIgnoreCase);
            Assert.Contains("Element table items", answer, StringComparison.OrdinalIgnoreCase);
            Assert.Contains("BIM category count", source, StringComparison.OrdinalIgnoreCase);
            Assert.DoesNotContain("detailed documentation", answer, StringComparison.OrdinalIgnoreCase);
            Assert.DoesNotContain("workflow for a construction management software", answer, StringComparison.OrdinalIgnoreCase);
        }

        [Fact]
        public void AnswersCombinedBimObjectAndZoneCountsFromOneSnapshot()
        {
            var context = BuildModelContext(
                elementCount: 362,
                modelRevision: 7,
                zoneCount: 5,
                allowedZoneCount: 3,
                forbiddenZoneCount: 2);

            var handled = AiBimProjectQuestionService.TryBuildAnswer(
                context,
                "How many BIM objects and zones are currently loaded?",
                "HOME",
                out var answer,
                out var source);

            Assert.True(handled);
            Assert.Contains("362 BIM/model object", answer, StringComparison.OrdinalIgnoreCase);
            Assert.Contains("5 zone", answer, StringComparison.OrdinalIgnoreCase);
            Assert.Contains("3 allowed/green", answer, StringComparison.OrdinalIgnoreCase);
            Assert.Contains("2 forbidden", answer, StringComparison.OrdinalIgnoreCase);
            Assert.Contains("same captured live project snapshot", answer, StringComparison.OrdinalIgnoreCase);
            Assert.Contains("BIM object and zone count", source, StringComparison.OrdinalIgnoreCase);
            Assert.DoesNotContain("cannot find a deterministic count", answer, StringComparison.OrdinalIgnoreCase);
        }

        [Fact]
        public void AnswersBimObjectCountAliasAsTotalSceneCount()
        {
            var context = BuildModelContext(elementCount: 362, modelRevision: 7);

            var handled = AiBimProjectQuestionService.TryBuildAnswer(
                context,
                "How many BIM objects are currently loaded?",
                "HOME",
                out var answer,
                out _);

            Assert.True(handled);
            Assert.Contains("362 scene BIM/model object", answer, StringComparison.OrdinalIgnoreCase);
            Assert.DoesNotContain("cannot find a deterministic count", answer, StringComparison.OrdinalIgnoreCase);
        }

        [Fact]
        public void AnswersBimCategoryBreakdownFromLiveFacts()
        {
            var context = BuildModelContext();

            var handled = AiBimProjectQuestionService.TryBuildAnswer(context, "Show the BIM category breakdown.", "IFC MODEL", out var answer, out _);

            Assert.True(handled);
            Assert.Contains("Slab: 97", answer);
            Assert.Contains("Wall: 179", answer);
            Assert.Contains("Resolved categories", answer);
        }

        [Fact]
        public void AnswersNoIfcCountQuestionWithLoadIfcGuidance()
        {
            var context = new AiWorkflowContextSnapshot
            {
                HasProject = true,
                CurrentProjectName = "Empty",
                ElementCount = 0,
                ZoneCount = 0,
                CraneCount = 0,
                OptimizationResult = new AiOptimizationResultSnapshot { HasResult = false }
            };

            var handled = AiBimProjectQuestionService.TryBuildAnswer(context, "How many slabs are there?", "CRANES", out var answer, out _);

            Assert.True(handled);
            Assert.Contains("no IFC/model elements are loaded", answer, StringComparison.OrdinalIgnoreCase);
            Assert.Contains("IFC MODEL → Load IFC", answer);
            Assert.DoesNotContain("97", answer);
        }

        [Fact]
        public void UnavailableCategoryDoesNotInventCount()
        {
            var context = BuildModelContext();

            var handled = AiBimProjectQuestionService.TryBuildAnswer(context, "How many windows are there?", "IFC MODEL", out var answer, out _);

            Assert.True(handled);
            Assert.Contains("cannot find a deterministic count", answer, StringComparison.OrdinalIgnoreCase);
            Assert.Contains("do not invent", answer, StringComparison.OrdinalIgnoreCase);
        }


        [Fact]
        public void AnswersUseCurrentModelSnapshotAfterModelChange()
        {
            var largeModel = BuildModelContext();
            var smallModel = BuildModelContext(new Dictionary<string, string>
            {
                [AiWorkflowFactKeys.ResolvedCategoryCounts] = "Column=205; Slab=60; Beam=34; Wall=26; Unknown=25; Stair=12",
                [AiWorkflowFactKeys.TopResolvedCategories] = "Column (205), Slab (60), Beam (34), Wall (26), Unknown (25), Stair (12)",
                [AiWorkflowFactKeys.RawIfcTypeCounts] = "IfcColumn=205; IfcSlab=60; IfcBeam=34; IfcWallStandardCase=26; IfcReinforcingBar=24; IfcStairFlight=8; IfcStair=4; IfcGrid=1",
                [AiWorkflowFactKeys.TopRawIfcTypes] = "IfcColumn (205), IfcSlab (60), IfcBeam (34), IfcWallStandardCase (26), IfcReinforcingBar (24), IfcStairFlight (8), IfcStair (4), IfcGrid (1)",
                [AiWorkflowFactKeys.TotalElements] = "362"
            }, elementCount: 362, modelRevision: 9, tableTotalCount: 337, visibleTableCount: 331);

            Assert.True(AiBimProjectQuestionService.TryBuildAnswer(largeModel, "How many slabs are there?", "IFC MODEL", out var oldAnswer, out _));
            Assert.Contains("97", oldAnswer);

            Assert.True(AiBimProjectQuestionService.TryBuildAnswer(smallModel, "How many slabs are there?", "IFC MODEL", out var newAnswer, out _));
            Assert.Contains("60", newAnswer);
            Assert.DoesNotContain("97 elements categorized as Slab", newAnswer);
            Assert.Contains("362", newAnswer);
            Assert.Contains("337", newAnswer);
            Assert.Contains("331", newAnswer);
        }

        [Fact]
        public void DoesNotBlockCategoryCountsWhenReadinessCountDiffersFromSceneCount()
        {
            var context = BuildModelContext(new Dictionary<string, string>
            {
                [AiWorkflowFactKeys.ResolvedCategoryCounts] = "Slab=97; Beam=80",
                [AiWorkflowFactKeys.TopResolvedCategories] = "Slab (97), Beam (80)",
                [AiWorkflowFactKeys.TotalElements] = "362",
                [AiWorkflowFactKeys.TotalBimObjects] = "362",
                [AiWorkflowFactKeys.SceneBimObjectCount] = "362",
                [AiWorkflowFactKeys.ReadinessElementCount] = "361",
                [AiWorkflowFactKeys.PlanningElementCount] = "361",
                [AiWorkflowFactKeys.ReferenceGeometryExcludedCount] = "1",
                [AiWorkflowFactKeys.ElementCountScopeNote] = "Scene BIM/model objects include 1 IFC reference-geometry object excluded from readiness/GA planning counts."
            }, elementCount: 362, modelRevision: 10, tableTotalCount: 337, visibleTableCount: 331);

            var handled = AiBimProjectQuestionService.TryBuildAnswer(context, "How many slabs are there?", "IFC MODEL", out var answer, out var source);

            Assert.True(handled);
            Assert.Contains("There are 97", answer);
            Assert.Contains("Planning/readiness element count: 361", answer);
            Assert.Contains("IFC reference geometry excluded from readiness: 1", answer);
            Assert.Contains("BIM category count", source, StringComparison.OrdinalIgnoreCase);
            Assert.DoesNotContain("will not answer", answer, StringComparison.OrdinalIgnoreCase);
            Assert.DoesNotContain("mismatched snapshot", answer, StringComparison.OrdinalIgnoreCase);
        }

        [Fact]
        public void BlocksCountAnswerWhenExplicitHostRevisionFactMismatchesSnapshot()
        {
            var context = BuildModelContext(new Dictionary<string, string>
            {
                [AiWorkflowFactKeys.ResolvedCategoryCounts] = "Slab=97; Beam=80",
                [AiWorkflowFactKeys.TopResolvedCategories] = "Slab (97), Beam (80)",
                [AiWorkflowFactKeys.SceneBimObjectCount] = "362",
                [AiWorkflowFactKeys.HostModelRevision] = "9"
            }, elementCount: 362, modelRevision: 10);

            var handled = AiBimProjectQuestionService.TryBuildAnswer(context, "How many slabs are there?", "IFC MODEL", out var answer, out var source);

            Assert.True(handled);
            Assert.Contains("will not answer", answer, StringComparison.OrdinalIgnoreCase);
            Assert.Contains("explicit host snapshot mismatch", answer, StringComparison.OrdinalIgnoreCase);
            Assert.Contains("blocked", source, StringComparison.OrdinalIgnoreCase);
            Assert.DoesNotContain("There are 97", answer);
        }


        [Fact]
        public void HostPlanningFactsMakeDirectCategoryAnswerPlanningFirstAndSceneAware()
        {
            var context = BuildEndToEndCountContext();

            var handled = AiBimProjectQuestionService.TryBuildAnswer(
                context,
                "How many slabs are there?",
                "HOME",
                out var answer,
                out var source);

            Assert.True(handled);
            Assert.Contains("50 filterable planning elements", answer, StringComparison.OrdinalIgnoreCase);
            Assert.Contains("116 objects classified as Slab", answer, StringComparison.OrdinalIgnoreCase);
            Assert.Contains("66 additional complete-scene objects", answer, StringComparison.OrdinalIgnoreCase);
            Assert.Contains("Filterable planning elements: 357", answer, StringComparison.OrdinalIgnoreCase);
            Assert.Contains("Currently visible filterable planning elements: 357", answer, StringComparison.OrdinalIgnoreCase);
            Assert.Contains("both planning/filterable and complete-scene values came from explicit deterministic host facts", answer, StringComparison.OrdinalIgnoreCase);
            Assert.DoesNotContain("not available to AI", answer, StringComparison.OrdinalIgnoreCase);
            Assert.Contains("BIM category count", source, StringComparison.OrdinalIgnoreCase);
        }

        [Fact]
        public void HostPlanningFactsMakeDiscrepancyComparisonFullyHostConfirmed()
        {
            var context = BuildEndToEndCountContext();

            var handled = AiBimProjectQuestionService.TryBuildAnswer(
                context,
                "Why does the IFC Element Filter show 50 slabs while the BIM context contains 116?",
                "HOME",
                out var answer,
                out _);

            Assert.True(handled);
            Assert.Contains("Planning/filterable Slab elements: **50**", answer);
            Assert.Contains("Complete IFC scene Slab elements: **116**", answer);
            Assert.Contains("Difference: **66 more complete-scene object(s)**", answer);
            Assert.Contains("Both values are host-confirmed", answer, StringComparison.OrdinalIgnoreCase);
            Assert.Contains("user-reported value agrees", answer, StringComparison.OrdinalIgnoreCase);
            Assert.DoesNotContain("not independently confirmed", answer, StringComparison.OrdinalIgnoreCase);
            Assert.DoesNotContain("host context adapter is required", answer, StringComparison.OrdinalIgnoreCase);
        }

        [Fact]
        public void HostPlanningFactsMakeCategoryBreakdownShowBothAuthoritativeScopes()
        {
            var context = BuildEndToEndCountContext();

            var handled = AiBimProjectQuestionService.TryBuildAnswer(
                context,
                "Show the BIM category counts for the planning/filter scope and the complete IFC scene.",
                "HOME",
                out var answer,
                out _);

            Assert.True(handled);
            Assert.Contains("Planning/filterable categories", answer);
            Assert.Contains("Slab: 50", answer);
            Assert.Contains("Beam: 40", answer);
            Assert.Contains("Resolved categories — complete IFC scene", answer);
            Assert.Contains("Slab: 116", answer);
            Assert.Contains("Beam: 80", answer);
            Assert.Contains("Planning/filterable counts are the primary engineering scope", answer, StringComparison.OrdinalIgnoreCase);
        }

        [Fact]
        public void AmbiguousAlternativeCategoryCountsStayDeterministicAndExplainBothScopes()
        {
            var context = BuildEndToEndCountContext();

            var handled = AiBimProjectQuestionService.TryBuildAnswer(
                context,
                "Are there 50 or 116 slabs?",
                "IFC MODEL",
                out var answer,
                out var source);

            Assert.True(handled);
            Assert.Contains("Both numbers are correct for different scopes", answer, StringComparison.OrdinalIgnoreCase);
            Assert.Contains("Planning/filterable Slab elements: **50**", answer);
            Assert.Contains("Complete IFC scene Slab elements: **116**", answer);
            Assert.DoesNotContain("2,383 slabs", answer, StringComparison.OrdinalIgnoreCase);
            Assert.Contains("count-scope comparison", source, StringComparison.OrdinalIgnoreCase);
        }

        [Fact]
        public void ConflictingUserReportedCountIsExplicitlyCorrectedByHostFacts()
        {
            var context = BuildEndToEndCountContext();

            var handled = AiBimProjectQuestionService.TryBuildAnswer(
                context,
                "Why does the Filter show 999 slabs while BIM contains 116?",
                "IFC MODEL",
                out var answer,
                out _);

            Assert.True(handled);
            Assert.Contains("planning/filter value of 999 conflicts", answer, StringComparison.OrdinalIgnoreCase);
            Assert.Contains("host-confirmed value of 50", answer, StringComparison.OrdinalIgnoreCase);
            Assert.Contains("host fact is authoritative", answer, StringComparison.OrdinalIgnoreCase);
            Assert.Contains("Planning/filterable Slab elements: **50**", answer);
        }

        [Fact]
        public void SingleReportedFilterCountIsCorrectedWithoutProviderFallback()
        {
            var context = BuildEndToEndCountContext();

            var handled = AiBimProjectQuestionService.TryBuildAnswer(
                context,
                "The Filter says 999 slabs.",
                "IFC MODEL",
                out var answer,
                out var source);

            Assert.True(handled);
            Assert.Contains("planning/filter value of 999 conflicts", answer, StringComparison.OrdinalIgnoreCase);
            Assert.Contains("host-confirmed value of 50", answer, StringComparison.OrdinalIgnoreCase);
            Assert.Contains("Planning/filterable Slab elements: **50**", answer);
            Assert.Contains("Complete IFC scene Slab elements: **116**", answer);
            Assert.Contains("count-scope comparison", source, StringComparison.OrdinalIgnoreCase);
        }

        [Fact]
        public void WhichCategoryCountIsCorrectRecommendsPlanningAndExplainsBothScopes()
        {
            var context = BuildEndToEndCountContext();

            var handled = AiBimProjectQuestionService.TryBuildAnswer(
                context,
                "Which slab count is correct?",
                "IFC MODEL",
                out var answer,
                out _);

            Assert.True(handled);
            Assert.Contains("Use the planning/filterable count for lift planning: **50 Slab elements**", answer);
            Assert.Contains("Use the same planning/filterable scope for engineering filtering", answer, StringComparison.OrdinalIgnoreCase);
            Assert.Contains("Planning/filterable Slab elements: **50**", answer);
            Assert.Contains("Complete IFC scene Slab elements: **116**", answer);
            Assert.Contains("no model provider was invoked", answer, StringComparison.OrdinalIgnoreCase);
        }

        [Fact]
        public void LiftPlanningQuestionDirectlyRecommendsPlanningScope()
        {
            var context = BuildEndToEndCountContext();

            var handled = AiBimProjectQuestionService.TryBuildAnswer(
                context,
                "Which slab count should I use for lift planning?",
                "IFC MODEL",
                out var answer,
                out _);

            Assert.True(handled);
            Assert.Contains("Use the planning/filterable count for lift planning: **50 Slab elements**", answer);
            Assert.Contains("not as the lift-planning quantity", answer, StringComparison.OrdinalIgnoreCase);
            Assert.Contains("no model provider was invoked", answer, StringComparison.OrdinalIgnoreCase);
        }

        [Fact]
        public void MultiCategoryComparisonIncludesEveryRequestedCategory()
        {
            var context = BuildEndToEndCountContext();

            var handled = AiBimProjectQuestionService.TryBuildAnswer(
                context,
                "Compare the slab and beam counts in both scopes.",
                "IFC MODEL",
                out var answer,
                out _);

            Assert.True(handled);
            Assert.Contains("Slab: **50 planning/filterable**; **116 complete scene**", answer);
            Assert.Contains("Beam: **40 planning/filterable**; **80 complete scene**", answer);
            Assert.Contains("Use planning/filterable counts for engineering filtering and lift-planning quantities", answer, StringComparison.OrdinalIgnoreCase);
            Assert.Contains("no model provider was invoked", answer, StringComparison.OrdinalIgnoreCase);
        }

        [Fact]
        public void ChatMessageMarksPreviousModelAnswersAsStale()
        {
            var oldContext = BuildModelContext(elementCount: 2483, modelRevision: 7);
            var newContext = BuildModelContext(new Dictionary<string, string>
            {
                [AiWorkflowFactKeys.ResolvedCategoryCounts] = "Slab=60",
                [AiWorkflowFactKeys.TopResolvedCategories] = "Slab (60)",
                [AiWorkflowFactKeys.TotalElements] = "362"
            }, elementCount: 362, modelRevision: 8);

            var message = AiCompanionChatMessageViewModel.Assistant("There are 97 slabs.", oldContext);

            message.MarkStaleAgainst(newContext.ModelFingerprint);

            Assert.True(message.IsStale);
            Assert.Contains("Stale captured context", message.StaleContextLabel);
            Assert.Contains("Bahy", message.StaleContextLabel);
            Assert.Contains("BIM object", message.StaleContextLabel);
        }

        [Fact]
        public void CandidateQuestionSeparatesRawCandidateLikeElementsFromTrustedLiftUnits()
        {
            var context = BuildModelContext(new Dictionary<string, string>
            {
                [AiWorkflowFactKeys.ResolvedCategoryCounts] = "Slab=97; Beam=80",
                [AiWorkflowFactKeys.TopResolvedCategories] = "Slab (97), Beam (80)",
                [AiWorkflowFactKeys.RawCandidateLikeElements] = "232",
                [AiWorkflowFactKeys.TrustedGroupedLiftUnits] = "0",
                [AiWorkflowFactKeys.GaBlockedElements] = "2125",
                [AiWorkflowFactKeys.EstimatedWeightBlockedCount] = "2016",
                [AiWorkflowFactKeys.MissingTrustedWeightCount] = "2125"
            });

            var handled = AiBimProjectQuestionService.TryBuildAnswer(context, "How many trusted lift units are there?", "IFC MODEL", out var answer, out _);

            Assert.True(handled);
            Assert.Contains("0 trusted GA-ready lift unit", answer, StringComparison.OrdinalIgnoreCase);
            Assert.Contains("Raw/import-screen candidate-like elements: 232", answer);
            Assert.Contains("Estimated/fallback-weight elements blocked from GA: 2,016", answer);
            Assert.Contains("Raw candidate-like element counts are not the same", answer);
        }

        [Fact]
        public void LiveBimCountQuestionsSuppressDocumentationRoutingButMetadataQuestionsDoNot()
        {
            Assert.True(AiBimProjectQuestionService.IsLiveBimDataQuestion("How many slabs are there?"));
            Assert.True(AiBimProjectQuestionService.IsLiveBimDataQuestion("Show the BIM category breakdown."));
            Assert.True(AiBimProjectQuestionService.IsLiveBimDataQuestion("Are there 50 or 116 slabs?"));
            Assert.True(AiBimProjectQuestionService.IsLiveBimDataQuestion("Which slab count should I use for lift planning?"));
            Assert.True(AiBimProjectQuestionService.IsLiveBimDataQuestion("Compare the slab and beam counts in both scopes."));
            Assert.True(AiBimProjectQuestionService.IsLiveBimDataQuestion("The Filter says 999 slabs."));
            Assert.True(AiBimProjectQuestionService.IsLiveBimDataQuestion("Which slab count is correct?"));
            Assert.False(AiBimProjectQuestionService.IsLiveBimDataQuestion("Are you able to extract the type of building or usage of the building?"));
        }

        private static AiWorkflowContextSnapshot BuildEndToEndCountContext()
        {
            return BuildModelContext(new Dictionary<string, string>
            {
                [AiWorkflowFactKeys.ResolvedCategoryCounts] = "Unknown=1950; Wall=214; Slab=116; Beam=80; Column=72; Roof=51",
                [AiWorkflowFactKeys.TopResolvedCategories] = "Unknown (1,950), Wall (214), Slab (116), Beam (80), Column (72), Roof (51)",
                [AiWorkflowFactKeys.PlanningCategoryCounts] = "Wall=214; Slab=50; Beam=40; Column=36; Roof=17",
                [AiWorkflowFactKeys.TopPlanningCategories] = "Wall (214), Slab (50), Beam (40), Column (36), Roof (17)",
                [AiWorkflowFactKeys.FilterableCategoryCounts] = "Wall=214; Slab=50; Beam=40; Column=36; Roof=17",
                [AiWorkflowFactKeys.TopFilterableCategories] = "Wall (214), Slab (50), Beam (40), Column (36), Roof (17)",
                [AiWorkflowFactKeys.FilterableElementCount] = "357",
                [AiWorkflowFactKeys.VisibleFilterableElementCount] = "357",
                [AiWorkflowFactKeys.CategoryCountScopeNote] = "Planning/filterable categories use the same scope as HOME → Filter: top-level LiftAssembly elements with IncludeInGa=true. Complete-scene categories can additionally include categorized child/component objects.",
                [AiWorkflowFactKeys.RawIfcTypeCounts] = "IfcBuildingElementPart=2025; IfcElementAssembly=357; IfcReinforcingBar=100; IfcGrid=1",
                [AiWorkflowFactKeys.TopRawIfcTypes] = "IfcBuildingElementPart (2,025), IfcElementAssembly (357), IfcReinforcingBar (100), IfcGrid (1)",
                [AiWorkflowFactKeys.SceneBimObjectCount] = "2483",
                [AiWorkflowFactKeys.TotalElements] = "2483"
            }, elementCount: 2483, modelRevision: 9, tableTotalCount: 2383, visibleTableCount: 2383);
        }

        private static AiWorkflowContextSnapshot BuildModelContext(
            IReadOnlyDictionary<string, string>? facts = null,
            int elementCount = 2483,
            long modelRevision = 7,
            int tableTotalCount = 2382,
            int visibleTableCount = 2351,
            int zoneCount = 0,
            int allowedZoneCount = 0,
            int forbiddenZoneCount = 0)
        {
            var modelFacts = facts ?? new Dictionary<string, string>
            {
                [AiWorkflowFactKeys.ResolvedCategoryCounts] = "Unknown=1950; Wall=179; Slab=97; Beam=80; Column=72",
                [AiWorkflowFactKeys.TopResolvedCategories] = "Unknown (1,950), Wall (179), Slab (97), Beam (80), Column (72)",
                [AiWorkflowFactKeys.RawIfcTypeCounts] = "IfcBuildingElementPart=2025; IfcElementAssembly=357; IfcReinforcingBar=100; IfcGrid=1",
                [AiWorkflowFactKeys.TopRawIfcTypes] = "IfcBuildingElementPart (2,025), IfcElementAssembly (357), IfcReinforcingBar (100), IfcGrid (1)",
                [AiWorkflowFactKeys.WeightOver3000KgCount] = "14",
                [AiWorkflowFactKeys.TotalElements] = "2483"
            };

            return new AiWorkflowContextSnapshot
            {
                HasProject = true,
                CurrentProjectName = "Bahy",
                ModelDisplayName = "Bahy",
                ModelRevision = modelRevision,
                ModelFingerprint = $"test-model-rev-{modelRevision}-elements-{elementCount}",
                ElementCount = elementCount,
                ElementTableTotalCount = tableTotalCount,
                ElementTableVisibleCount = visibleTableCount,
                ZoneCount = zoneCount,
                AllowedZoneCount = allowedZoneCount,
                ForbiddenZoneCount = forbiddenZoneCount,
                CraneCount = 0,
                OptimizationResult = new AiOptimizationResultSnapshot { HasResult = false },
                ScreenContexts = new[]
                {
                    new AiScreenContextSnapshot
                    {
                        ScreenName = "IFC import readiness",
                        Stage = "Review",
                        Summary = "IFC model loaded with resolved category facts.",
                        Facts = modelFacts
                    }
                }
            };
        }
    }
}

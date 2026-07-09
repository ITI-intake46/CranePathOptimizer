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
            Assert.False(AiBimProjectQuestionService.IsLiveBimDataQuestion("Are you able to extract the type of building or usage of the building?"));
        }

        private static AiWorkflowContextSnapshot BuildModelContext(IReadOnlyDictionary<string, string>? facts = null, int elementCount = 2483, long modelRevision = 7, int tableTotalCount = 2382, int visibleTableCount = 2351)
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
                ZoneCount = 0,
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

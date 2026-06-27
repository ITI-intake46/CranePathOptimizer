using System.Collections.Generic;
using CraneOptimizer.UI.Features.F4_LoadChart.Services;
using Xunit;

namespace CraneOptimizer.Application.Tests.LoadChart
{
    public sealed class LoadChartPdfMetadataServiceTests
    {
        [Theory]
        [MemberData(nameof(RealCorpusMetadataCases))]
        public void ExtractFromRealCorpusSnippets_DetectsStrongMetadataWithoutWeakGuesses(
            string fileName,
            string pageText,
            string expectedModel,
            string expectedManufacturer,
            double? expectedCounterweight,
            double? expectedLength,
            double? expectedHeight,
            double? expectedWidth,
            double? expectedOutriggerWidth,
            double? expectedOutriggerLength)
        {
            var metadata = LoadChartPdfMetadataService.ExtractFromFileNameAndPageTextsForTesting(
                fileName,
                new[] { pageText },
                selectedPageNumber: 1);

            if (!string.IsNullOrWhiteSpace(expectedModel))
                Assert.Equal(expectedModel, metadata.Model);

            if (!string.IsNullOrWhiteSpace(expectedManufacturer))
                Assert.Equal(expectedManufacturer, metadata.Manufacturer);

            AssertNear(expectedCounterweight, metadata.CounterweightTons, "counterweight");
            AssertNear(expectedLength, metadata.VehicleLengthMeters, "vehicle length");
            AssertNear(expectedHeight, metadata.VehicleHeightMeters, "vehicle height");
            AssertNear(expectedWidth, metadata.VehicleWidthMeters, "vehicle width");
            AssertNear(expectedOutriggerWidth, metadata.OutriggerWidthMeters, "outrigger width");
            AssertNear(expectedOutriggerLength, metadata.OutriggerLengthMeters, "outrigger length");
        }

        [Fact]
        public void ExtractFromPageTexts_RejectsTirePairAsOutriggerSpan()
        {
            var metadata = LoadChartPdfMetadataService.ExtractFromTextForTesting(
                "STC800 Truck Crane Technical parameter Dimensions Overall length mm 141000 Overall width mm 2750 Overall height mm 3850 " +
                "Outriggers Max. span up to 6.1mx7.6m. Tyres 14*12.00R24 20PR Counterweight is 4T");

            Assert.Equal(7.6d, metadata.OutriggerWidthMeters.GetValueOrDefault(), precision: 3);
            Assert.Equal(6.1d, metadata.OutriggerLengthMeters.GetValueOrDefault(), precision: 3);
            Assert.NotEqual(12d, metadata.OutriggerWidthMeters.GetValueOrDefault(), precision: 3);
        }

        [Fact]
        public void ExtractFromPageTexts_PreservesLiebherrPrivateDashModelSuffix()
        {
            var metadata = LoadChartPdfMetadataService.ExtractFromTextForTesting(
                "Technische Daten Technical Data Mobilkran Mobile Crane LTM 1200\uE02D5.1 200 t max " +
                "Ma\u00DFe Dimensions A A B C D E F H I K mm* 150 385/95 R 25 (14.00 R 25) 3950 3800 3000 2563");

            Assert.Equal("LTM 1200-5.1", metadata.Model);
        }

        [Fact]
        public void ExtractFromPageTexts_AddsMissingAndSourceReviewNotes()
        {
            var metadata = LoadChartPdfMetadataService.ExtractFromTextForTesting(
                "SANY SRC550C Technical parameter Weight and dimensions Overall length14300 mm Outrigger span7.2 x 7.2 m");

            Assert.Contains(metadata.Notes, note => note.Contains("still needs manual review"));
            Assert.Contains(metadata.Notes, note => note.Contains("PDF metadata sources"));
        }

        [Fact]
        public void AutoFillState_ReplacesPreviousAutoValuesButPreservesUserValues()
        {
            var state = new LoadChartMetadataAutoFillState();

            state.RememberModel("STC1000C");
            state.RememberManufacturer("SANY");
            state.RememberCounterweight("32.5");
            state.RememberVehicleLength(18.5);
            state.RememberVehicleWidth(3.0);
            state.RememberVehicleHeight(4.0);
            state.RememberOutriggerWidth(7.6);
            state.RememberOutriggerLength(7.6);

            Assert.True(state.ShouldReplaceModel("STC1000C", "100Tons"));
            Assert.True(state.ShouldReplaceManufacturer("SANY"));
            Assert.True(state.ShouldReplaceCounterweight("32.5"));
            Assert.True(state.ShouldReplaceVehicleLength(18.5));
            Assert.True(state.ShouldReplaceVehicleWidth(3.0));
            Assert.True(state.ShouldReplaceVehicleHeight(4.0));
            Assert.True(state.ShouldReplaceOutriggerWidth(7.6));
            Assert.True(state.ShouldReplaceOutriggerLength(7.6));

            Assert.False(state.ShouldReplaceModel("ManualModel", "100Tons"));
            Assert.False(state.ShouldReplaceManufacturer("ManualManufacturer"));
            Assert.False(state.ShouldReplaceCounterweight("99"));
            Assert.False(state.ShouldReplaceVehicleLength(19.0));
            Assert.False(state.ShouldReplaceVehicleWidth(3.5));
            Assert.False(state.ShouldReplaceVehicleHeight(4.5));
            Assert.False(state.ShouldReplaceOutriggerWidth(8.0));
            Assert.False(state.ShouldReplaceOutriggerLength(8.0));
        }

        [Fact]
        public void AutoFillState_UserOwnershipClearPreventsFutureReplacement()
        {
            var state = new LoadChartMetadataAutoFillState();

            state.RememberModel("STC1000C");
            state.RememberManufacturer("SANY");
            state.RememberCounterweight("32.5");
            state.RememberVehicleLength(18.5);
            state.RememberVehicleWidth(3.0);
            state.RememberVehicleHeight(4.0);
            state.RememberOutriggerWidth(7.6);
            state.RememberOutriggerLength(7.6);

            state.ClearModelOwnership();
            state.ClearManufacturerOwnership();
            state.ClearCounterweightOwnership();
            state.ClearDimensionOwnership();

            Assert.False(state.ShouldReplaceModel("STC1000C", "100Tons"));
            Assert.False(state.ShouldReplaceManufacturer("SANY"));
            Assert.False(state.ShouldReplaceCounterweight("32.5"));
            Assert.False(state.ShouldReplaceVehicleLength(18.5));
            Assert.False(state.ShouldReplaceVehicleWidth(3.0));
            Assert.False(state.ShouldReplaceVehicleHeight(4.0));
            Assert.False(state.ShouldReplaceOutriggerWidth(7.6));
            Assert.False(state.ShouldReplaceOutriggerLength(7.6));
        }


        [Theory]
        [InlineData("LTM 1200-5.1 Liebherr Masse Dimensions C G A A B D E F H I K mm* 150 385/95 (14.00 25) R R 25 3950 3800 3000 2563 3704 3139 2000 Ballast 72 t")]
        [InlineData("LTM 1200-5.1 Liebherr Masse Dimensions AABCDEFGHIK150 mm*385/95 R 25 (14.00 R 25)395038003000256337043139200010deg15deg37715deg Ballast 72 t")]
        public void ExtractFromPageTexts_DetectsLiebherrDimensionTableWhenPdfTextOrderIsCompact(string pageText)
        {
            var metadata = LoadChartPdfMetadataService.ExtractFromTextForTesting(pageText);

            Assert.Equal("LTM 1200-5.1", metadata.Model);
            Assert.Equal("Liebherr", metadata.Manufacturer);
            Assert.Equal(3.95d, metadata.VehicleHeightMeters.GetValueOrDefault(), precision: 3);
            Assert.Equal(3.0d, metadata.VehicleWidthMeters.GetValueOrDefault(), precision: 3);
        }

        [Fact]
        public void AutoFillState_PdfSourceIdentitySequenceReplacesAutoFilledDimensions()
        {
            var state = new LoadChartMetadataAutoFillState();
            var dimensions = new TestDimensions();

            var src550c = LoadChartPdfMetadataService.ExtractFromFileNameAndPageTextsForTesting(
                "12_Seiter_Prospekt_SRC550C_eng.pdf",
                new[] { "SANY SRC550C Technical parameter Weight and dimensionsOverall length14300 mm Overall width3300 mmOverall height3760 mm Main Performance Data Outrigger span7.2 x 7.2 m Max rated lifting capacity55 t" },
                selectedPageNumber: 1);

            dimensions = ApplyDetectedDimensionsForTest(state, "sha256:SRC550C|page:1", src550c, dimensions);

            Assert.Equal(14.3d, dimensions.VehicleLengthMeters, precision: 3);
            Assert.Equal(3.76d, dimensions.VehicleHeightMeters, precision: 3);
            Assert.Equal(3.3d, dimensions.VehicleWidthMeters, precision: 3);
            Assert.Equal(7.2d, dimensions.OutriggerWidthMeters.GetValueOrDefault(), precision: 3);
            Assert.Equal(7.2d, dimensions.OutriggerLengthMeters.GetValueOrDefault(), precision: 3);

            var stc800 = LoadChartPdfMetadataService.ExtractFromFileNameAndPageTextsForTesting(
                "80Tons.pdf",
                new[] { "SANY STC800 Truck Crane TECHNICAL PARAMETER Type Item Parameter Capacity Max. lifting capacity 80 t Dimensions Overall length mm 141000 Overall width mm 2750 Overall height mm 3850 Main Performance Data Outrigger span (LongitudinalxTransversal) 6.1 x 7.6 m Counterweight is 4T Tyres 14*12.00R24" },
                selectedPageNumber: 1);

            dimensions = ApplyDetectedDimensionsForTest(state, "sha256:STC800|page:7", stc800, dimensions);

            Assert.Equal(14.1d, dimensions.VehicleLengthMeters, precision: 3);
            Assert.Equal(3.85d, dimensions.VehicleHeightMeters, precision: 3);
            Assert.Equal(2.75d, dimensions.VehicleWidthMeters, precision: 3);
            Assert.Equal(7.6d, dimensions.OutriggerWidthMeters.GetValueOrDefault(), precision: 3);
            Assert.Equal(6.1d, dimensions.OutriggerLengthMeters.GetValueOrDefault(), precision: 3);

            var liebherr70 = LoadChartPdfMetadataService.ExtractFromFileNameAndPageTextsForTesting(
                "70Tons-Liebherr.pdf",
                new[] { "LTM 1070-4.2 Liebherr Ma\u00DFe Dimensions 13030 12515 11000 3460 2200 I L H I K G 1979 1650 2050 1650 10114 2785 2295 3005 12409 7991 5326 2665 B 4500 6300 2320-6300 A A B C D E F G H I K L 100 mm* 385/95 R 25 (14.00 R 25) 3900 3800 2550 2160 3574 2844 1816 378 Counterweight 14.5 t" },
                selectedPageNumber: 1);

            dimensions = ApplyDetectedDimensionsForTest(state, "sha256:LTM1070|page:11", liebherr70, dimensions);

            Assert.Equal(13.03d, dimensions.VehicleLengthMeters, precision: 3);
            Assert.Equal(3.9d, dimensions.VehicleHeightMeters, precision: 3);
            Assert.Equal(2.55d, dimensions.VehicleWidthMeters, precision: 3);
            Assert.Equal(6.3d, dimensions.OutriggerWidthMeters.GetValueOrDefault(), precision: 3);
            Assert.Equal(6.3d, dimensions.OutriggerLengthMeters.GetValueOrDefault(), precision: 3);
        }

        [Fact]
        public void AutoFillState_UserEditedCurrentPdfDimensionIsPreservedWhileOtherAutoDimensionsRefresh()
        {
            var state = new LoadChartMetadataAutoFillState();
            var dimensions = new TestDimensions();

            var stc800 = LoadChartPdfMetadataService.ExtractFromFileNameAndPageTextsForTesting(
                "80Tons.pdf",
                new[] { "SANY STC800 Truck Crane TECHNICAL PARAMETER Type Item Parameter Capacity Max. lifting capacity 80 t Dimensions Overall length mm 141000 Overall width mm 2750 Overall height mm 3850 Main Performance Data Outrigger span (LongitudinalxTransversal) 6.1 x 7.6 m Counterweight is 4T Tyres 14*12.00R24" },
                selectedPageNumber: 1);

            dimensions = ApplyDetectedDimensionsForTest(state, "sha256:STC800|page:7", stc800, dimensions);

            dimensions.VehicleLengthMeters = 99d;
            state.ClearVehicleLengthOwnership();

            var liebherr70 = LoadChartPdfMetadataService.ExtractFromFileNameAndPageTextsForTesting(
                "70Tons-Liebherr.pdf",
                new[] { "LTM 1070-4.2 Liebherr Ma\u00DFe Dimensions 13030 12515 11000 3460 2200 I L H I K G 1979 1650 2050 1650 10114 2785 2295 3005 12409 7991 5326 2665 B 4500 6300 2320-6300 A A B C D E F G H I K L 100 mm* 385/95 R 25 (14.00 R 25) 3900 3800 2550 2160 3574 2844 1816 378 Counterweight 14.5 t" },
                selectedPageNumber: 1);

            dimensions = ApplyDetectedDimensionsForTest(state, "sha256:LTM1070|page:11", liebherr70, dimensions);

            Assert.Equal(99d, dimensions.VehicleLengthMeters, precision: 3);
            Assert.Equal(3.9d, dimensions.VehicleHeightMeters, precision: 3);
            Assert.Equal(2.55d, dimensions.VehicleWidthMeters, precision: 3);
            Assert.Equal(6.3d, dimensions.OutriggerWidthMeters.GetValueOrDefault(), precision: 3);
            Assert.Equal(6.3d, dimensions.OutriggerLengthMeters.GetValueOrDefault(), precision: 3);
        }



        [Fact]
        public void ExtractFromPageTexts_DetectsConcatenatedLiebherr200SupportBaseClusterFromRealPdfTextOrder()
        {
            string pageText = "LTM 1200-5.1 Liebherr MaÃŸe Dimensions A E D F C348388795396R = 6870R1 = 5450R = 13320R = 12550R1 = 11000R = 11150R1 = 9580R1 = 118504321550062308300175R = 4850B 503438658899 MaÃŸe Dimensions Encombrement Dimensioni Dimensiones mm AABCDEFGHIK 385/95 R 25 (14.00 R 25)3950380030002563370431392000 Counterweight 72 t";

            var metadata = LoadChartPdfMetadataService.ExtractFromTextForTesting(pageText);

            Assert.Equal("LTM 1200-5.1", metadata.Model);
            Assert.Equal(8.3d, metadata.OutriggerWidthMeters.GetValueOrDefault(), precision: 3);
            Assert.Equal(8.899d, metadata.OutriggerLengthMeters.GetValueOrDefault(), precision: 3);
            Assert.Contains("transverse support-width", metadata.OutriggerWidthSource, System.StringComparison.OrdinalIgnoreCase);
            Assert.Contains("longitudinal front-to-rear support base", metadata.OutriggerLengthSource, System.StringComparison.OrdinalIgnoreCase);
        }

        private static TestDimensions ApplyDetectedDimensionsForTest(
            LoadChartMetadataAutoFillState state,
            string sourceIdentity,
            LoadChartPdfMetadataDto metadata,
            TestDimensions current)
        {
            if (!state.IsSameSource(sourceIdentity))
            {
                if (state.ShouldReplaceVehicleLength(current.VehicleLengthMeters))
                    current.VehicleLengthMeters = 0d;

                if (state.ShouldReplaceVehicleHeight(current.VehicleHeightMeters))
                    current.VehicleHeightMeters = 0d;

                if (state.ShouldReplaceVehicleWidth(current.VehicleWidthMeters))
                    current.VehicleWidthMeters = 0d;

                if (state.ShouldReplaceOutriggerWidth(current.OutriggerWidthMeters))
                    current.OutriggerWidthMeters = null;

                if (state.ShouldReplaceOutriggerLength(current.OutriggerLengthMeters))
                    current.OutriggerLengthMeters = null;

                state.Clear();
                state.BeginSource(sourceIdentity);
            }

            if (metadata.VehicleLengthMeters.HasValue && state.ShouldReplaceVehicleLength(current.VehicleLengthMeters))
            {
                current.VehicleLengthMeters = metadata.VehicleLengthMeters.Value;
                state.RememberVehicleLength(current.VehicleLengthMeters);
            }

            if (metadata.VehicleHeightMeters.HasValue && state.ShouldReplaceVehicleHeight(current.VehicleHeightMeters))
            {
                current.VehicleHeightMeters = metadata.VehicleHeightMeters.Value;
                state.RememberVehicleHeight(current.VehicleHeightMeters);
            }

            if (metadata.VehicleWidthMeters.HasValue && state.ShouldReplaceVehicleWidth(current.VehicleWidthMeters))
            {
                current.VehicleWidthMeters = metadata.VehicleWidthMeters.Value;
                state.RememberVehicleWidth(current.VehicleWidthMeters);
            }

            if (metadata.OutriggerWidthMeters.HasValue && state.ShouldReplaceOutriggerWidth(current.OutriggerWidthMeters))
            {
                current.OutriggerWidthMeters = metadata.OutriggerWidthMeters.Value;
                state.RememberOutriggerWidth(current.OutriggerWidthMeters);
            }

            if (metadata.OutriggerLengthMeters.HasValue && state.ShouldReplaceOutriggerLength(current.OutriggerLengthMeters))
            {
                current.OutriggerLengthMeters = metadata.OutriggerLengthMeters.Value;
                state.RememberOutriggerLength(current.OutriggerLengthMeters);
            }

            return current;
        }

        private sealed class TestDimensions
        {
            public double VehicleLengthMeters { get; set; }
            public double VehicleHeightMeters { get; set; }
            public double VehicleWidthMeters { get; set; }
            public double? OutriggerWidthMeters { get; set; }
            public double? OutriggerLengthMeters { get; set; }
        }
        [Fact]
        public void ExtractFromPageTexts_DetectsCounterweightOnePiecePhrase()
        {
            var metadata = LoadChartPdfMetadataService.ExtractFromTextForTesting(
                "SANY SRC550C Technical parameter Weight and dimensions Overall length14300 mm Overall width3300 mm Overall height3760 mm " +
                "Counterweight One piece of 4.5 ton, fixed on the slewing platform. Outrigger span7.2 x 7.2 m");

            Assert.Equal(4.5d, metadata.CounterweightTons.GetValueOrDefault(), precision: 3);
            Assert.Contains("confidence", metadata.CounterweightSource, System.StringComparison.OrdinalIgnoreCase);
        }

        [Fact]
        public void ExtractFromPageTexts_InferSingleLiebherrSupportRangeForBothOutriggerDirections()
        {
            var metadata = LoadChartPdfMetadataService.ExtractFromTextForTesting(
                "LTM 1070-4.2 Liebherr MaÃƒÅ¸e Dimensions 13030 12515 11000 3460 2200 I L H I K G " +
                "1979 1650 2050 1650 10114 2785 2295 3005 12409 7991 5326 2665 B 4500 6300 2320-6300 " +
                "A A B C D E F G H I K L 100 mm* 385/95 R 25 (14.00 R 25) 3900 3800 2550 2160 Counterweight 14.5 t");

            Assert.Equal(6.3d, metadata.OutriggerWidthMeters.GetValueOrDefault(), precision: 3);
            Assert.Equal(6.3d, metadata.OutriggerLengthMeters.GetValueOrDefault(), precision: 3);
            Assert.Contains(metadata.Notes, note => note.Contains("Inferred Liebherr front-to-rear outrigger span", System.StringComparison.OrdinalIgnoreCase));
        }

        [Fact]
        public void ExtractFromPageTexts_RoundsLiebherrSupportRangesForUserFacingPrecision()
        {
            var metadata = LoadChartPdfMetadataService.ExtractFromTextForTesting(
                "LTM 1160-5.2 Liebherr MaÃƒÅ¸e/Dimensions Encombrement/Dimensioni 8314 E C B 4270 5376 6840 8306 D A " +
                "2446-8306 1) 2454-8314 1) A A B C D E I H F G 100 mm* 385/95 R 25 (14.00 R 25) 3950 3850 2750 2360 " +
                "Additional counterweight20 t for a total counterweight of 54 t");

            Assert.Equal(8.31d, metadata.OutriggerWidthMeters.GetValueOrDefault(), precision: 3);
            Assert.Equal(8.31d, metadata.OutriggerLengthMeters.GetValueOrDefault(), precision: 3);
        }

        [Fact]
        public void ExtractFromPageTexts_DoesNotGuessLiebherrOutriggersWithoutSupportRange()
        {
            var metadata = LoadChartPdfMetadataService.ExtractFromTextForTesting(
                "LTM 1200-5.1 Liebherr MaÃƒÅ¸e Dimensions 15810 15115 13230 4066 925 925 2450 A D E F K H G I " +
                "A A B C D E F H I K 150 mm* 385/95 R 25 (14.00 R 25) 3950 3800 3000 2563 3704 3139 2000 Ballast 72 t");

            Assert.False(metadata.OutriggerWidthMeters.HasValue);
            Assert.False(metadata.OutriggerLengthMeters.HasValue);
            Assert.Contains(metadata.Notes, note => note.Contains("outrigger", System.StringComparison.OrdinalIgnoreCase));
        }
        [Fact]
        public void ExtractFromPageTexts_RejectsRatedCapacityValueBeforeCounterweightKeyword()
        {
            var metadata = LoadChartPdfMetadataService.ExtractFromTextForTesting(
                "SANY SRC550C Technical parameter Weight and dimensions Overall length14300 mm Overall width3300 mm Overall height3760 mm " +
                "Main Performance Data Outrigger span7.2 x 7.2 m Max rated lifting capacity55 t " +
                "Counterweight One piece of 4.5 ton, fixed on the slewing platform.");

            Assert.Equal(4.5d, metadata.CounterweightTons.GetValueOrDefault(), precision: 3);
        }
        [Fact]
        public void ExtractFromPageTexts_DetectsLiebherr200OutriggerDiagramCluster()
        {
            var metadata = LoadChartPdfMetadataService.ExtractFromTextForTesting(
                "LTM 1200-5.1 Liebherr Ma\u00DFe Dimensions Encombrement Dimensioni Dimensiones " +
                "A E D F C 5500 6230 8300 B R = 6870 R1 = 5450 R = 13320 R = 12550 R1 = 11000 " +
                "A A B C D E F G H I K 150 mm* 385/95 R 25 (14.00 R 25) 3950 3800 3000 2563 Ballast 72 t");

            Assert.Equal(8.3d, metadata.OutriggerWidthMeters.GetValueOrDefault(), precision: 3);
            Assert.False(metadata.OutriggerLengthMeters.HasValue);
            Assert.Contains("transverse support-width", metadata.OutriggerWidthSource, System.StringComparison.OrdinalIgnoreCase);
            Assert.Contains(metadata.Notes, note => note.Contains("front-to-rear outrigger span still needs manual review", System.StringComparison.OrdinalIgnoreCase));
        }

        [Fact]
        public void ExtractFromPageTexts_DoesNotUseLiebherrSteeringRadiusValuesAsOutriggers()
        {
            var metadata = LoadChartPdfMetadataService.ExtractFromTextForTesting(
                "LTM 1200-5.1 Liebherr Ma\u00DFe Dimensions Encombrement Dimensioni Dimensiones " +
                "R = 6870 R1 = 5450 R = 13320 R = 12550 R1 = 11000 R = 11150 R1 = 9580 R1 = 11850 " +
                "A A B C D E F G H I K 150 mm* 385/95 R 25 (14.00 R 25) 3950 3800 3000 2563 Ballast 72 t");

            Assert.False(metadata.OutriggerWidthMeters.HasValue);
            Assert.False(metadata.OutriggerLengthMeters.HasValue);
        }
        [Fact]
        public void ExtractFromPositionedWords_DetectsLiebherr200OutriggerVerticalDigitColumnsWhenTextOrderIsBroken()
        {
            var positionedWords = new (string Text, double Left, double Right, double Top, double Bottom)[]
            {
                ("B", 503.505, 509.074, 309.167, 314.158),
                ("C", 492.499, 498.334, 308.979, 314.298),
                ("0", 514.267, 519.890, 316.166, 320.175),
                ("0", 514.267, 519.890, 311.588, 315.597),
                ("5", 514.360, 519.890, 307.182, 311.151),
                ("5", 514.360, 519.890, 302.744, 306.714),
                ("0", 525.084, 530.708, 316.071, 320.080),
                ("3", 525.084, 530.708, 311.602, 315.579),
                ("2", 525.178, 530.708, 307.257, 311.110),
                ("6", 525.084, 530.708, 302.835, 306.883),
                ("0", 535.901, 541.525, 316.082, 320.091),
                ("0", 535.901, 541.525, 311.504, 315.513),
                ("3", 535.901, 541.525, 307.035, 311.013),
                ("8", 535.901, 541.525, 302.558, 306.582),
            };

            var metadata = LoadChartPdfMetadataService.ExtractFromPositionedWordsForTesting(
                "LTM 1200-5.1 Liebherr MaÃŸe Dimensions Encombrement Dimensioni Dimensiones AABCDEFGHIK " +
                "steering radius R = 6870 R1 = 5450 R = 13320 R = 12550 no readable support range in text Ballast 72 t",
                positionedWords,
                selectedPageNumber: 2);

            Assert.Equal(8.3d, metadata.OutriggerWidthMeters.GetValueOrDefault(), precision: 3);
            Assert.False(metadata.OutriggerLengthMeters.HasValue);
            Assert.Contains("positioned Liebherr transverse support-width", metadata.OutriggerWidthSource, System.StringComparison.OrdinalIgnoreCase);
            Assert.Contains(metadata.Notes, note => note.Contains("front-to-rear outrigger span still needs manual review", System.StringComparison.OrdinalIgnoreCase));
        }

        [Fact]
        public void VisualChartHeaderNormalization_CorrectsDroppedDecimalInUniversalGeometryPath()
        {
            var serviceType = typeof(LoadChartPdfMetadataService).Assembly.GetType(
                "CraneOptimizer.UI.Features.F4_LoadChart.Services.LoadChartPdfService",
                throwOnError: true)!;
            var method = serviceType.GetMethod("CorrectBoomHeaderDroppedDecimalsForTesting")
                ?? throw new System.InvalidOperationException("Missing boom-header normalization test hook.");

            var corrected = (IReadOnlyList<double>)method.Invoke(null, new object[] { new[] { 13.2d, 13.2d, 177d, 22.2d, 26.7d, 31.3d } })!;

            Assert.Equal(13.2d, corrected[0], precision: 3);
            Assert.Equal(13.2d, corrected[1], precision: 3);
            Assert.Equal(17.7d, corrected[2], precision: 3);
            Assert.Equal(22.2d, corrected[3], precision: 3);
        }

        [Fact]
        public void ExtractFromPositionedWords_DetectsLiebherr200OutriggerColumnsWhenBcLabelsAreOffset()
        {
            var positionedWords = new (string Text, double Left, double Right, double Top, double Bottom)[]
            {
                ("C", 488.368, 494.309, 354.480, 359.896),
                ("B", 499.573, 505.244, 368.672, 373.754),
                ("0", 509.659, 515.385, 361.932, 366.014),
                ("0", 509.659, 515.385, 357.516, 361.598),
                ("5", 509.754, 515.385, 353.100, 357.143),
                ("5", 509.754, 515.385, 348.685, 352.727),
                ("0", 520.674, 526.400, 361.932, 366.014),
                ("3", 520.674, 526.400, 357.516, 361.566),
                ("2", 520.769, 526.400, 353.100, 357.024),
                ("6", 520.674, 526.400, 348.685, 352.807),
                ("0", 531.688, 537.414, 361.932, 366.014),
                ("0", 531.688, 537.414, 357.516, 361.598),
                ("3", 531.688, 537.414, 353.100, 357.151),
                ("8", 531.688, 537.414, 348.685, 352.783),
            };

            var metadata = LoadChartPdfMetadataService.ExtractFromPositionedWordsForTesting(
                "LTM 1200-5.1 Liebherr MaÃŸe Dimensions Encombrement Dimensioni Dimensiones AABCDEFGHIK " +
                "R = 6870 R1 = 5450 R = 13320 R = 12550 text order does not expose readable support range Ballast 72 t",
                positionedWords,
                selectedPageNumber: 39);

            Assert.Equal(8.3d, metadata.OutriggerWidthMeters.GetValueOrDefault(), precision: 3);
            Assert.False(metadata.OutriggerLengthMeters.HasValue);
            Assert.Contains("positioned Liebherr transverse support-width", metadata.OutriggerWidthSource, System.StringComparison.OrdinalIgnoreCase);
        }

        [Fact]
        public void ExtractFromPageTexts_DetectsLiebherr200CompactContentOrderSupportCluster()
        {
            var metadata = LoadChartPdfMetadataService.ExtractFromTextForTesting(
                "LTM 1200-5.1 Liebherr MaÃŸe Dimensions Encombrement Dimensioni Dimensiones " +
                "C348388795396R = 6870R1 = 5450R = 13320R = 12550R1 = 11000R = 11150R1 = 9580R1 = 118504321550062308300175R = 4850B " +
                "AABCDEFGHIK150 mm*385/95 R 25 (14.00 R 25)3950380030002563370431392000 Ballast 72 t");

            Assert.Equal(8.3d, metadata.OutriggerWidthMeters.GetValueOrDefault(), precision: 3);
            Assert.Equal(8.879d, metadata.OutriggerLengthMeters.GetValueOrDefault(), precision: 3);
            Assert.Contains("transverse support-width", metadata.OutriggerWidthSource, System.StringComparison.OrdinalIgnoreCase);
            Assert.Contains("longitudinal front-to-rear support base", metadata.OutriggerLengthSource, System.StringComparison.OrdinalIgnoreCase);
        }



        [Fact]
        public void ExtractFromPageTexts_DetectsLiebherr200TCapRuntimeCompressedKbWidthCluster()
        {
            var metadata = LoadChartPdfMetadataService.ExtractFromTextForTesting(
                "LTM 1200-5.1 Liebherr Dimensions Encombrement Dimensioni Dimensiones " +
                "9252450AE274024961329815282S2026HGDF13230I265016501984244016502412600386550348899" +
                "C34835396R = 6870R1 = 5450R = 13320R = 12550R1 = 11010R = 11150R1 = 9560R1 = 11850" +
                "4237550062308300175R = 4850KB3935 " +
                "MaÃŸe Â· Dimensions Â· Encombrement Â· Dimensioni Â· Dimensiones mm AABCDEFGHIK150 mm* " +
                "385/95 R 25 (14.00 R 25)3950380030002563370431392000 Ballast 72 t");

            Assert.Equal("LTM 1200-5.1", metadata.Model);
            Assert.Equal(8.3d, metadata.OutriggerWidthMeters.GetValueOrDefault(), precision: 3);
            Assert.Equal(8.899d, metadata.OutriggerLengthMeters.GetValueOrDefault(), precision: 3);
            Assert.Contains("transverse support-width", metadata.OutriggerWidthSource, System.StringComparison.OrdinalIgnoreCase);
            Assert.Contains("longitudinal front-to-rear support base", metadata.OutriggerLengthSource, System.StringComparison.OrdinalIgnoreCase);
        }
        [Fact]
        public void ExtractFromPageTexts_RejectsLiebherrR1SteeringRadius9560AsOutriggerSupport()
        {
            var metadata = LoadChartPdfMetadataService.ExtractFromTextForTesting(
                "LTM 1200-5.1 Liebherr MaÃŸe Dimensions Encombrement Dimensioni Dimensiones " +
                "R1 = 9560 R = 6870 R1 = 5450 R = 13320 R = 12550 R1 = 11010 R = 11150 R1 = 11850 " +
                "R1 = All-wheel steering A A B C D E F G H I K 150 mm* 385/95 R 25 (14.00 R 25) 3950 3800 3000 2563 Ballast 72 t");

            Assert.False(metadata.OutriggerWidthMeters.HasValue);
            Assert.False(metadata.OutriggerLengthMeters.HasValue);
            Assert.DoesNotContain("9.56", metadata.OutriggerWidthSource);
            Assert.DoesNotContain("9.56", metadata.OutriggerLengthSource);
        }

        [Fact]
        public void ExtractFromPageTexts_Liebherr8300And8899ReturnSeparateOutriggerDirections()
        {
            var metadata = LoadChartPdfMetadataService.ExtractFromTextForTesting(
                "LTM 1200-5.1 Liebherr MaÃŸe Dimensions Encombrement Dimensioni Dimensiones " +
                "A E D F C 5500 6230 8300 B 5396 3483 5034 3865 8899 R1 = All-wheel steering " +
                "A A B C D E F H I K 150 mm* 385/95 R 25 (14.00 R 25) 3950 3800 3000 2563 Ballast 72 t");

            Assert.Equal(8.3d, metadata.OutriggerWidthMeters.GetValueOrDefault(), precision: 3);
            Assert.Equal(8.899d, metadata.OutriggerLengthMeters.GetValueOrDefault(), precision: 3);
            Assert.Contains("transverse support-width", metadata.OutriggerWidthSource, System.StringComparison.OrdinalIgnoreCase);
            Assert.Contains("longitudinal front-to-rear support base", metadata.OutriggerLengthSource, System.StringComparison.OrdinalIgnoreCase);
        }

        [Fact]
        public void ExtractFromPageTexts_PreservesSanyLongitudinalTransversalOutriggerOrientation()
        {
            var metadata = LoadChartPdfMetadataService.ExtractFromTextForTesting(
                "SANY STC800 Truck Crane Technical parameter Main Performance Data " +
                "Outrigger span (Longitudinal x Transversal) 6.1 x 7.6 m Counterweight is 4T");

            Assert.Equal(7.6d, metadata.OutriggerWidthMeters.GetValueOrDefault(), precision: 3);
            Assert.Equal(6.1d, metadata.OutriggerLengthMeters.GetValueOrDefault(), precision: 3);
        }

        [Fact]
        public void ExtractFromPageTexts_PreservesSrc550cExplicitSquareOutriggerSpan()
        {
            var metadata = LoadChartPdfMetadataService.ExtractFromTextForTesting(
                "SANY SRC550C Technical parameter Main Performance Data Outrigger span 7.2 m x 7.2 m Counterweight One piece of 4.5 ton");

            Assert.Equal(7.2d, metadata.OutriggerWidthMeters.GetValueOrDefault(), precision: 3);
            Assert.Equal(7.2d, metadata.OutriggerLengthMeters.GetValueOrDefault(), precision: 3);
        }

        public static IEnumerable<object[]> RealCorpusMetadataCases()
        {
            yield return new object[]
            {
                "80Tons.pdf",
                "SANY STC800 Truck Crane TECHNICAL PARAMETER Type Item Parameter Capacity Max. lifting capacity 80 t Dimensions Overall length mm 141000 Overall width mm 2750 Overall height mm 3850 Main Performance Data Outrigger span (LongitudinalxTransversal) 6.1 x 7.6 m Counterweight is 4T Tyres 14*12.00R24",
                "STC800", "SANY", 4d, 14.1d, 3.85d, 2.75d, 7.6d, 6.1d
            };

            yield return new object[]
            {
                "100Tons.pdf",
                "SANY STC1000C Truck Crane TECHNICAL PARAMETER Capacity Max. lifting capacity 100t Dimensions Overall length mm 15400 Overall width mm 3000 Overall height mm 4000 Main Performance Data Outrigger span (LongitudinalxTransversal) 7.6 x 7.6 m Creative counterweight pushing technology With full-counterweight weight of 32.5T",
                "STC1000C", "SANY", 32.5d, 15.4d, 4.0d, 3.0d, 7.6d, 7.6d
            };

            yield return new object[]
            {
                "130Tons.pdf",
                "SANY STC1300C Truck Crane TECHNICAL PARAMETER Capacity Max. lifting capacity 130t Dimensions Overall length mm 15485 Overall width mm 3000 Overall height mm 4000 Main Performance Data Outrigger span (LongitudinalxTransversal) 7.6 x 7.6 m Creative counterweight pushing technology With full-counterweight weight of 40T",
                "STC1300C", "SANY", 40d, 15.485d, 4.0d, 3.0d, 7.6d, 7.6d
            };

            yield return new object[]
            {
                "12_Seiter_Prospekt_SRC550C_eng.pdf",
                "SANY SRC550C Technical parameter Weight and dimensionsOverall length14300 mm Overall width3300 mmOverall height3760 mm Main Performance Data Outrigger span7.2 x 7.2 m Max rated lifting capacity55 t Counterweight One piece of 4.5 ton, fixed on the slewing platform.",
                "SRC550C", "SANY", 4.5d, 14.3d, 3.76d, 3.3d, 7.2d, 7.2d
            };

            yield return new object[]
            {
                "100Tons-Liebherr.pdf",
                "LTM 1100-4.2 Liebherr Ma\u00DFe Dimensions 2515-7009 S3110 13513 14008 1399 4084 929 11460 D A E 2189 3529 1650 2440 1650 2799 2954 2230 10728 12958 8531 5755 2776 K H I F 310 B C 4830 5007 7009 G H A A B C D E F G H I K 100 mm* 385/95 R 25 (14.00 R 25) 3950 3850 2750 2360 3667 1890 378 17 11 10 16 Counterweight 17.2 t basic counterweight Additional counterweight 11 t for a total counterweight of 28.2 t",
                "LTM 1100-4.2", "Liebherr", 28.2d, 14.008d, 3.95d, 2.75d, 7.01d, 7.01d
            };

            yield return new object[]
            {
                "180Tons.pdf",
                "LTM 1160-5.2 Liebherr Ma\u00DFe Dimensions 4715 1245 4420 15866 13100 1920 1130 A D E H I G G F 2446-8306 2454-8314 A A B C D E I H F G 100 mm* 385/95 R 25 (14.00 R 25) 3950 3850 2750 2360 3708 1996 17 10 378 14 Additional counterweight20 t for a total counterweight of 54 t",
                "LTM 1160-5.2", "Liebherr", 54d, 15.866d, 3.95d, 2.75d, 8.31d, 8.31d
            };

            yield return new object[]
            {
                "200Tons.pdf",
                "LTM 1200\uE02D5.1 Liebherr Ma\u00DFe Dimensions A E D F C 5500 6230 8300 B 15810 15115 13230 4066 925 925 2450 A D E F K H G I 4321 13382 15370 8879 5396 3483 5034 3865 8899 A A B C D E F H I K 150 mm* 385/95 R 25 (14.00 R 25) 3950 3800 3000 2563 3704 3139 2000 Ballast 72 t",
                "LTM 1200-5.1", "Liebherr", 72d, 15.81d, 3.95d, 3.0d, 8.3d, 8.899d
            };

            yield return new object[]
            {
                "200 t cap.pdf",
                "LTM 1200\u00025.1 Liebherr Ma\u00DFe Dimensions A E D F C 5500 6230 8300 B 13230 3935 2450 925 A D E F I G K H 175 2740 2496 1984 2650 1650 2440 1650 2412 4237 13298 15282 5396 3483 5034 3865 8899 A A B D E F H I K 150 mm* 385/95 R 25 (14.00 R 25) 3950 3800 3000 2563 3704 3139 2000 Ballast 72 t",
                "LTM 1200-5.1", "Liebherr", 72d, 15.282d, 3.95d, 3.0d, 8.3d, 8.899d
            };

            yield return new object[]
            {
                "70Tons-Liebherr.pdf",
                "LTM 1070-4.2 Liebherr Ma\u00DFe Dimensions 13030 12515 11000 3460 2200 I L H I K G 1979 1650 2050 1650 10114 2785 2295 3005 12409 7991 5326 2665 B 4500 6300 2320-6300 A A B C D E F G H I K L 100 mm* 385/95 R 25 (14.00 R 25) 3900 3800 2550 2160 3574 2844 1816 378 Counterweight 14.5 t",
                "LTM 1070-4.2", "Liebherr", 14.5d, 13.03d, 3.9d, 2.55d, 6.3d, 6.3d
            };
        }

        private static void AssertNear(double? expected, double? actual, string fieldName)
        {
            if (!expected.HasValue)
                return;

            Assert.True(actual.HasValue, $"Expected {fieldName} to be detected as {expected.Value}, but it was missing.");
            Assert.Equal(expected.Value, actual.Value, precision: 3);
        }
    }
}

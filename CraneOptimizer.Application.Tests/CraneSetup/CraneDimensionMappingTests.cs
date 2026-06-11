using CraneOptimizer.Domain.Common;
using CraneOptimizer.Domain.Entities;
using CraneOptimizer.UI.Core.Models;
using CraneOptimizer.UI.Features.F3_CraneSetup.Parameters;
using CraneOptimizer.UI.Features.F3_CraneSetup.Visual;
using HelixToolkit.Wpf;
using System.Windows.Media.Media3D;

namespace CraneOptimizer.Application.Tests.CraneSetup
{
    public sealed class CraneDimensionMappingTests
    {
        [Fact]
        public void FromSceneCrane_PreservesImportedDimensionsAndMapsOutriggerAxes()
        {
            SceneCrane scene = CreateScene("100%");

            MobileCraneParameters parameters = CraneParametersFactory.FromSceneCrane(scene);

            Assert.Equal(17.2d, parameters.CarrierLength, 3);
            Assert.Equal(3.1d, parameters.TravelWidthM, 3);
            Assert.Equal(4.25d, parameters.CarrierHeight, 3);
            Assert.Equal(9.6d, parameters.OutriggerSpanM, 3);
            Assert.Equal(8.8d, parameters.SpanToSpanM, 3);
            Assert.False(parameters.IsOnRubber);
        }

        [Fact]
        public void FromSceneCrane_OnRubberDisablesOutriggerRendering()
        {
            SceneCrane scene = CreateScene("On Rubber");

            MobileCraneParameters parameters = CraneParametersFactory.FromSceneCrane(scene);

            Assert.True(parameters.IsOnRubber);
            Assert.False(parameters.ShowOutriggerPads);
        }

        [Fact]
        public void MobileCraneParameters_DoNotSilentlyClampImportedTransportDimensions()
        {
            var parameters = new MobileCraneParameters
            {
                CarrierLength = 36d,
                CarrierWidth = 7.5d,
                CarrierHeight = 4.6d,
                TravelWidthM = 7.5d,
                OutriggerSpanM = 24d,
                SpanToSpanM = 22d
            };

            Assert.Equal(36d, parameters.CarrierLength);
            Assert.Equal(7.5d, parameters.CarrierWidth);
            Assert.Equal(4.6d, parameters.CarrierHeight);
            Assert.Equal(7.5d, parameters.TravelWidthM);
            Assert.Equal(24d, parameters.OutriggerSpanM);
            Assert.Equal(22d, parameters.SpanToSpanM);
        }

        [Fact]
        public void MobileCraneVisual_OnRubberOmitsPadsAndLegs()
        {
            var outriggerParameters = new MobileCraneParameters
            {
                ShowSwingRadius = false,
                ShowOutriggerPads = true,
                IsOnRubber = false
            };
            var rubberParameters = new MobileCraneParameters
            {
                ShowSwingRadius = false,
                ShowOutriggerPads = true,
                IsOnRubber = true
            };

            using var outriggerVisual = new MobileCraneVisual3D(outriggerParameters);
            using var rubberVisual = new MobileCraneVisual3D(rubberParameters);

            Model3DGroup outriggerRoot = Assert.IsType<Model3DGroup>(outriggerVisual.Content);
            Model3DGroup rubberRoot = Assert.IsType<Model3DGroup>(rubberVisual.Content);

            Assert.Equal(rubberRoot.Children.Count + 2, outriggerRoot.Children.Count);
        }

        [Fact]
        public void MobileCraneVisual_DimensionGuideAddsOneOverlayGroup()
        {
            RunOnStaThread(() =>
            {
                var parameters = new MobileCraneParameters
                {
                    ShowSwingRadius = false,
                    IsOnRubber = true,
                    ShowDimensionGuide = false
                };
                using var visual = new MobileCraneVisual3D(parameters);
                Model3DGroup withoutGuide = Assert.IsType<Model3DGroup>(visual.Content);
                int baseChildCount = withoutGuide.Children.Count;

                parameters.ShowDimensionGuide = true;

                Model3DGroup withGuide = Assert.IsType<Model3DGroup>(visual.Content);
                Assert.Equal(baseChildCount + 1, withGuide.Children.Count);
                ModelVisual3D labelContainer = Assert.IsType<ModelVisual3D>(Assert.Single(visual.Children));
                Assert.Equal(3, labelContainer.Children.Count);
            });
        }

        [Fact]
        public void MobileCraneVisual_OutriggerDimensionGuideShowsFiveNumericLabels()
        {
            RunOnStaThread(() =>
            {
                var parameters = new MobileCraneParameters
                {
                    ShowSwingRadius = false,
                    IsOnRubber = false,
                    ShowDimensionGuide = true
                };
                using var visual = new MobileCraneVisual3D(parameters);

                ModelVisual3D labelContainer = Assert.IsType<ModelVisual3D>(Assert.Single(visual.Children));
                Assert.Equal(5, labelContainer.Children.Count);
                Assert.All(
                    labelContainer.Children.Cast<BillboardTextVisual3D>(),
                    label => Assert.Contains("m", label.Text));
            });
        }

        private static void RunOnStaThread(Action action)
        {
            Exception? exception = null;
            var thread = new Thread(() =>
            {
                try
                {
                    action();
                }
                catch (Exception ex)
                {
                    exception = ex;
                }
            });

            thread.SetApartmentState(ApartmentState.STA);
            thread.Start();
            thread.Join();

            if (exception != null)
                throw exception;
        }

        private static SceneCrane CreateScene(string configuration)
        {
            var specification = new CraneSpecification
            {
                OutriggerConfig = configuration,
                MinRadius = 3d,
                MaxRadius = 50d,
                MinBoomLength = 15d,
                MaxBoomLength = 60d,
                MaxCapacity = 500d
            };
            var crane = new Crane
            {
                Name = "Mapped Crane",
                Manufacturer = "Example",
                VehicleLength = 17.2d,
                VehicleWidth = 3.1d,
                VehicleHeight = 4.25d,
                OutriggerWidth = 9.6d,
                OutriggerLength = 8.8d,
                Specifications = new List<CraneSpecification> { specification }
            };

            return new SceneCrane
            {
                CraneDefinition = crane,
                ActiveSpecification = specification,
                Position = new DomainPoint3D(),
                BoomLength = 20d,
                BoomAngle = 45d
            };
        }
    }
}

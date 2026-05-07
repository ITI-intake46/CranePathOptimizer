using CraneOptimizer.UI.Core.Models;
using CraneOptimizer.UI.Features.F3_CraneSetup.Parameters;
using CraneOptimizer.UI.Features.F3_CraneSetup.Visual;
using System.Windows;
using System.Windows.Media.Media3D;

namespace CraneOptimizer.UI.Features.F3_CraneSetup.Management
{
    internal sealed class CraneInstance
    {
        public SceneCrane Scene { get; private set; }
        public CraneParametersBase Parameters { get; }
        public ModelVisual3D Visual { get; }
        public ICraneVisual CraneVisual { get; }

        public CraneInstance(SceneCrane scene, CraneParametersBase parameters, ModelVisual3D visual)
        {
            Scene = scene;
            Parameters = parameters;
            Visual = visual;
            CraneVisual = (ICraneVisual)visual;
            Visual.SetValue(FrameworkElement.TagProperty, scene);
        }

        public void UpdateFromScene(SceneCrane scene)
        {
            Scene = scene;
            if (Parameters is MobileCraneParameters mobile)
                CraneParametersFactory.UpdateFromScene(mobile, scene);

            Visual.SetValue(FrameworkElement.TagProperty, scene);
        }

        public void Dispose() => CraneVisual.Dispose();
    }
}

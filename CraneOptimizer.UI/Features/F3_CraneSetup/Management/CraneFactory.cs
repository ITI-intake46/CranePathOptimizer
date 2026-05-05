using CraneOptimizer.Domain.Entities.Enums;
using CraneOptimizer.UI.Core.Models;
using CraneOptimizer.UI.Features.F3_CraneSetup.Parameters;
using CraneOptimizer.UI.Features.F3_CraneSetup.Visual;
using System.Windows.Media.Media3D;

namespace CraneOptimizer.UI.Features.F3_CraneSetup.Management
{
    internal static class CraneFactory
    {
        public static (ModelVisual3D Visual, CraneParametersBase Parameters) Create(SceneCrane scene)
        {
            // Domain enum remains unchanged (Mobile/Crawler).
            // Both map to the mobile-crane renderer for now.
            if (scene.CraneDefinition.CraneType is CraneType.Mobile or CraneType.Crawler)
            {
                var parameters = CraneParametersFactory.FromSceneCrane(scene);
                return (new MobileCraneVisual3D(parameters), parameters);
            }

            var fallback = CraneParametersFactory.FromSceneCrane(scene);
            return (new MobileCraneVisual3D(fallback), fallback);
        }
    }
}

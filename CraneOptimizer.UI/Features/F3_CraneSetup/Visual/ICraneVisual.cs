using CraneOptimizer.UI.Features.F3_CraneSetup.Parameters;

namespace CraneOptimizer.UI.Features.F3_CraneSetup.Visual
{
    internal interface ICraneVisual
    {
        CraneParametersBase Parameters { get; }
        void Rebuild();
        void Dispose();
    }
}

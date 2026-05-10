using CraneOptimizer.Domain.Schemas;
using System.Collections.Generic;
using System.Linq;

namespace CraneOptimizer.UI.Features.F5_ElementManagement.Services
{
    public interface IIfcLoadingService
    {
        string GetRevitId(object product);
        string GetMark(object product);
        double EstimateWeight(double? volumeM3, string? material);
    }

    public class IfcLoadingService : IIfcLoadingService
    {
        private const double ConcreteDensity = 2500;

        public string GetRevitId(object product)
        {
            // Implementation logic moved to IfcReaderRepository for tight integration with Xbim
            return string.Empty;
        }

        public string GetMark(object product)
        {
            return string.Empty;
        }

        public double EstimateWeight(double? volumeM3, string? material)
        {
            if (!volumeM3.HasValue || volumeM3 <= 0) return 0;
            return volumeM3.Value * ConcreteDensity;
        }
    }
}

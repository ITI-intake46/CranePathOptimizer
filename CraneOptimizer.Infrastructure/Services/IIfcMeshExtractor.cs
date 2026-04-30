using CraneOptimizer.Domain.Schemas;
using Xbim.Common.Geometry;

namespace CraneOptimizer.Infrastructure.Services
{
    public interface IIfcMeshExtractor
    {
        IfcElementMesh? Extract(
            byte[] shapeData,
            XbimMatrix3D transform,
            double lengthToMillimetersFactor,
            IfcMeshQuality quality = IfcMeshQuality.Smooth);
    }
}

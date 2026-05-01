using Xbim.Common.Geometry;
using Xbim.Ifc;
using Xbim.ModelGeometry.Scene;
using System.Collections.Generic;

namespace CraneOptimizer.UI.Core.Interfaces
{
    public interface IIfcGeometryProvider
    {
        IfcStore CurrentModel { get; }
        Xbim3DModelContext GeometryContext { get; }
        IEnumerable<XbimShapeInstance> GetElementShapes(string globalId);
    }
}

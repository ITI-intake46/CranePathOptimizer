using CraneOptimizer.Domain.Common;
using CraneOptimizer.Domain.CreatedProject;

namespace CraneOptimizer.Domain.Interfaces
{
    public interface IProjectCoordinateTransformService
    {
        event System.EventHandler? ReferenceChanged;

        bool IsEnabled { get; }
        ProjectCoordinateReference CurrentReference { get; }

        DomainPoint3D LocalToSurvey(DomainPoint3D local);
        DomainPoint3D SurveyToLocal(DomainPoint3D survey);

        string FormatCoordinate(double value);
        string FormatLocalPoint(DomainPoint3D point);
        string FormatSurveyPoint(DomainPoint3D point);

        bool ApplyReference(ProjectCoordinateReference reference, out string? errorMessage);
        void ResetReference();
    }
}

using System;
using CraneOptimizer.Domain.Common;
using CraneOptimizer.Domain.CreatedProject;
using CraneOptimizer.Domain.Interfaces;

namespace CraneOptimizer.Application.Services
{
    public sealed class ProjectCoordinateTransformService : IProjectCoordinateTransformService
    {
        public event EventHandler? ReferenceChanged;

        private ProjectCoordinateReference _reference = new() { IsEnabled = false };

        public bool IsEnabled => _reference.IsEnabled;

        public ProjectCoordinateReference CurrentReference => _reference;

        public DomainPoint3D LocalToSurvey(DomainPoint3D local)
        {
            if (!_reference.IsEnabled)
            {
                return local;
            }

            double dx = local.X - _reference.LocalReferenceX;
            double dy = local.Y - _reference.LocalReferenceY;
            double dz = local.Z - _reference.LocalReferenceZ;

            double theta = _reference.RotationToNorthDegrees * Math.PI / 180.0;
            double cosTheta = Math.Cos(theta);
            double sinTheta = Math.Sin(theta);

            double easting = _reference.SurveyEasting + (cosTheta * dx - sinTheta * dy) * _reference.Scale;
            double northing = _reference.SurveyNorthing + (sinTheta * dx + cosTheta * dy) * _reference.Scale;
            double elevation = _reference.SurveyElevation + dz * _reference.Scale;

            return new DomainPoint3D(easting, northing, elevation);
        }

        public DomainPoint3D SurveyToLocal(DomainPoint3D survey)
        {
            if (!_reference.IsEnabled)
            {
                return survey;
            }

            double dE = survey.X - _reference.SurveyEasting;
            double dN = survey.Y - _reference.SurveyNorthing;
            double dH = survey.Z - _reference.SurveyElevation;

            double theta = _reference.RotationToNorthDegrees * Math.PI / 180.0;
            double cosTheta = Math.Cos(theta);
            double sinTheta = Math.Sin(theta);

            // Matrix inverse for 2D rotation:
            // dx = cos(theta) * dE + sin(theta) * dN
            // dy = -sin(theta) * dE + cos(theta) * dN
            double scaleFactor = _reference.Scale != 0.0 ? _reference.Scale : 1.0;
            double dx = (cosTheta * dE + sinTheta * dN) / scaleFactor;
            double dy = (-sinTheta * dE + cosTheta * dN) / scaleFactor;
            double dz = dH / scaleFactor;

            double x = _reference.LocalReferenceX + dx;
            double y = _reference.LocalReferenceY + dy;
            double z = _reference.LocalReferenceZ + dz;

            return new DomainPoint3D(x, y, z);
        }

        public string FormatCoordinate(double value)
        {
            return value.ToString("F3");
        }

        public string FormatLocalPoint(DomainPoint3D point)
        {
            return $"X: {point.X:F3}, Y: {point.Y:F3}, Z: {point.Z:F3}";
        }

        public string FormatSurveyPoint(DomainPoint3D point)
        {
            return $"E: {point.X:F3}, N: {point.Y:F3}, H: {point.Z:F3}";
        }

        public bool ApplyReference(ProjectCoordinateReference reference, out string? errorMessage)
        {
            if (reference == null)
            {
                errorMessage = "Reference settings cannot be null.";
                return false;
            }

            if (reference.Scale <= 0.0)
            {
                errorMessage = "Scale factor must be greater than zero.";
                return false;
            }

            if (reference.RotationToNorthDegrees < -360.0 || reference.RotationToNorthDegrees > 360.0)
            {
                errorMessage = "Rotation to north must be between -360 and 360 degrees.";
                return false;
            }

            _reference = reference;
            errorMessage = null;
            ReferenceChanged?.Invoke(this, EventArgs.Empty);
            return true;
        }

        public void ResetReference()
        {
            _reference = new ProjectCoordinateReference
            {
                IsEnabled = false,
                Scale = 1.0,
                RotationToNorthDegrees = 0.0
            };
            ReferenceChanged?.Invoke(this, EventArgs.Empty);
        }
    }
}

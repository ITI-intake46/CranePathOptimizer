using CraneOptimizer.UI.Core.Models;

namespace CraneOptimizer.UI.Features.F3_CraneSetup.Parameters
{
    internal static class CraneParametersFactory
    {
        public static MobileCraneParameters FromSceneCrane(SceneCrane scene)
        {
            var crane = scene.CraneDefinition;
            var spec = scene.ActiveSpecification;
            var loadChart = spec?.LoadChart;

            var minBoom = spec?.MinBoomLength ?? 15.0;
            var maxBoom = spec?.MaxBoomLength ?? 40.0;
            if (maxBoom < minBoom)
                (minBoom, maxBoom) = (maxBoom, minBoom);

            var minReachFromPoints = loadChart != null && loadChart.Count > 0
                ? loadChart.Where(point => point.Radius > 0.0).Select(point => point.Radius).DefaultIfEmpty(0.0).Min()
                : 0.0;
            var maxReachFromPoints = loadChart != null && loadChart.Count > 0
                ? loadChart.Max(point => point.Radius)
                : 0.0;
            var maxCapacityFromPoints = loadChart != null && loadChart.Count > 0
                ? loadChart.Where(point => point.Capacity > 0.0).Select(point => point.Capacity).DefaultIfEmpty(0.0).Max()
                : 0.0;
            bool isOnRubber = IsOnRubber(spec?.OutriggerConfig);

            var minReach = spec is { MinRadius: > 0.0 }
                ? spec.MinRadius
                : minReachFromPoints;
            var maxReach = spec is { MaxRadius: > 0.0 }
                ? spec.MaxRadius
                : maxReachFromPoints;

            if (maxReach <= 0.0)
                maxReach = maxBoom;
            if (minReach < 0.0)
                minReach = 0.0;
            if (minReach > maxReach)
                (minReach, maxReach) = (maxReach, minReach);

            return new MobileCraneParameters
            {
                PositionX = scene.Position.X,
                PositionY = scene.Position.Y,
                PositionZ = scene.Position.Z,
                SlewAngle = scene.SlewAngle,
                IsVisible = true,
                ShowSwingRadius = true,
                ExclusionRadiusM = 0.0,
                CableLength = 15.0,

                CarrierLength = ToMeters(crane.VehicleLength, 12.0),
                CarrierWidth = ToMeters(crane.VehicleWidth, 2.75),
                CarrierHeight = ToMeters(crane.VehicleHeight, 2.0),
                OutriggerSpanM = ToMeters(crane.OutriggerWidth, 7.0),
                SpanToSpanM = ToMeters(crane.OutriggerLength, 7.0),
                OutriggerPadSize = 2.0,
                ShowOutriggerPads = !isOnRubber,
                IsOnRubber = isOnRubber,
                TravelWidthM = ToMeters(crane.VehicleWidth, 3.0),
                BodyAngle = scene.Rotation,
                BoomSections = 4,
                BoomBaseWidth = 0.8,

                MinReach = minReach,
                MaxReach = maxReach,
                MinBoomLength = minBoom,
                MaxBoomLength = maxBoom,
                BoomLength = Math.Clamp(scene.BoomLength, minBoom, maxBoom),
                BoomAngle = scene.BoomAngle,
                MaxCapacity = maxCapacityFromPoints > 0.0
                    ? maxCapacityFromPoints
                    : (spec?.MaxCapacity ?? 0.0)
            };
        }

        public static void UpdateFromScene(MobileCraneParameters target, SceneCrane scene)
        {
            var fresh = FromSceneCrane(scene);
            target.PositionX = fresh.PositionX;
            target.PositionY = fresh.PositionY;
            target.PositionZ = fresh.PositionZ;
            target.SlewAngle = fresh.SlewAngle;
            target.IsVisible = fresh.IsVisible;
            target.ShowSwingRadius = fresh.ShowSwingRadius;
            target.ExclusionRadiusM = fresh.ExclusionRadiusM;
            target.CableLength = fresh.CableLength;

            target.CarrierLength = fresh.CarrierLength;
            target.CarrierWidth = fresh.CarrierWidth;
            target.CarrierHeight = fresh.CarrierHeight;
            target.OutriggerSpanM = fresh.OutriggerSpanM;
            target.SpanToSpanM = fresh.SpanToSpanM;
            target.OutriggerPadSize = fresh.OutriggerPadSize;
            target.ShowOutriggerPads = fresh.ShowOutriggerPads;
            target.IsOnRubber = fresh.IsOnRubber;
            target.TravelWidthM = fresh.TravelWidthM;
            target.BodyAngle = fresh.BodyAngle;
            target.BoomSections = fresh.BoomSections;
            target.BoomBaseWidth = fresh.BoomBaseWidth;

            target.MinReach = fresh.MinReach;
            target.MaxReach = fresh.MaxReach;
            target.MinBoomLength = fresh.MinBoomLength;
            target.MaxBoomLength = fresh.MaxBoomLength;
            target.BoomLength = fresh.BoomLength;
            target.BoomAngle = fresh.BoomAngle;
            target.MaxCapacity = fresh.MaxCapacity;
        }

        private static double ToMeters(double value, double fallback)
        {
            if (value <= 0.0)
                return fallback;

            return value;
        }

        private static double ToMeters(double? value, double fallback)
        {
            return value.HasValue ? ToMeters(value.Value, fallback) : fallback;
        }

        private static bool IsOnRubber(string? outriggerConfiguration)
        {
            return string.Equals(
                outriggerConfiguration?.Trim(),
                "On Rubber",
                StringComparison.OrdinalIgnoreCase);
        }
    }
}

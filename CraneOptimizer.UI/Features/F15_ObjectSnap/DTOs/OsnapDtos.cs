namespace CraneOptimizer.UI.Features.F15_ObjectSnap.DTOs
{
    /// <summary>
    /// Simple point data for object snap results.
    /// </summary>
    public sealed record OsnapPointDto(
        double X,
        double Y,
        double Z);

    /// <summary>
    /// Simple object snap result data for UI display.
    /// </summary>
    public sealed record OsnapResultDto(
        string SnapType,
        OsnapPointDto WorldPoint,
        string Source);

    /// <summary>
    /// Simple object snap settings data saved to JSON.
    /// </summary>
    public sealed record OsnapSettingsDto
    {
        public bool MasterEnabled { get; init; } = true;
        public bool SnapEndpoint { get; init; } = true;
        public bool SnapMidpoint { get; init; } = true;
        public bool SnapIntersection { get; init; }
        public bool SnapPerpendicular { get; init; }
        public bool SnapNearest { get; init; }
        public bool SnapGrid { get; init; } = true;
        public bool SnapCenter { get; init; } = true;
        public bool SnapQuadrant { get; init; }
        public double SnapRadiusPx { get; init; } = 25.0;
        public double GridSpacing { get; init; } = 1.0;
        public int MaxEdgesForIntersection { get; init; } = 200;
        public bool SnapToDxf { get; init; } = true;
        public bool SnapToIfc { get; init; } = true;
    }
}

namespace CraneOptimizer.Domain.Schemas.Enums
{
    /// <summary>
    /// Represents the status of a lifting operation.
    /// </summary>
    public enum LiftStatus
    {
        /// <summary>
        /// Element is within radius and capacity.
        /// </summary>
        Liftable,

        /// <summary>
        /// Element is outside the crane's maximum working radius.
        /// </summary>
        OutOfRadius,

        /// <summary>
        /// Element weight exceeds the crane's capacity at the given radius.
        /// </summary>
        Overweight,

        /// <summary>
        /// Crane cannot reach the required height for the lift.
        /// </summary>
        HeightFail,

        /// <summary>
        /// No load chart data available for this configuration.
        /// </summary>
        SpecialCase
    }
}

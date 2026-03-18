namespace WPF_IFC_test1.Analysis
{
    /// <summary>
    /// Per-element result of the 4-step lift feasibility check.
    /// Enum values intentionally match the PDF specification penalty table.
    /// </summary>
    public enum LiftStatus
    {
        /// <summary>All three checks passed. Element is within reach, within crane capacity, and
        /// the hook height is sufficient. Penalty = 0.</summary>
        Liftable = 0,

        /// <summary>Within horizontal reach, but either the crane is overloaded at that radius
        /// OR the hook cannot reach the required delivery height. Penalty = 20.</summary>
        NonLiftable = 2,

        /// <summary>Element centroid is beyond the crane's maximum boom radius, or the
        /// requested (radius, boom) combination is outside the load chart envelope. Penalty = 100.</summary>
        Unreachable = 3
    }
}

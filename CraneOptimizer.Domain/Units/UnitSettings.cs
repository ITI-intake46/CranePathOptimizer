namespace CraneOptimizer.Domain.Units
{
    public sealed class UnitSettings
    {
        public LengthUnit DisplayLengthUnit { get; set; } = LengthUnit.Meter;
        public MassUnit DisplayMassUnit { get; set; } = MassUnit.Ton;
        public AreaUnit DisplayAreaUnit { get; set; } = AreaUnit.SquareMeter;
        public int DecimalPlaces { get; set; } = 2;

        public static UnitSettings Default { get; } = new();

        public UnitSettings Clone() => new()
        {
            DisplayLengthUnit = DisplayLengthUnit,
            DisplayMassUnit = DisplayMassUnit,
            DisplayAreaUnit = DisplayAreaUnit,
            DecimalPlaces = DecimalPlaces
        };
    }
}

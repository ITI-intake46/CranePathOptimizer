namespace CraneOptimizer.UI.Features.F3_CraneSetup.Parameters
{
    public sealed class MobileCraneParameters : CraneParametersBase
    {
        private double _carrierLength = 12.0;
        private double _carrierWidth = 2.75;
        private double _carrierHeight = 2.0;
        private double _outtriggerSpanM = 7.0;
        private double _spanToSpanM = 7.0;
        private double _outtriggerPadSize = 2.0;
        private bool _showOutriggerPads = true;
        private double _boomLength = 20.0;
        private double _minBoomLength = 10.0;
        private double _maxBoomLength = 60.0;
        private double _boomAngle = 45.0;
        private double _travelWidthM = 3.0;
        private double _bodyAngle;
        private int _boomSections = 4;
        private double _boomBaseWidth = 0.8;
        private double _workingRadius = 14.14;
        private bool _isOnRubber;
        private bool _showDimensionGuide;

        public override UiCraneType Type => UiCraneType.MobileCrane;

        public double CarrierLength { get => _carrierLength; set => SetField(ref _carrierLength, Math.Max(0.1, value)); }
        public double CarrierWidth { get => _carrierWidth; set => SetField(ref _carrierWidth, Math.Max(0.1, value)); }
        public double CarrierHeight { get => _carrierHeight; set => SetField(ref _carrierHeight, Math.Max(0.1, value)); }
        public double OutriggerSpanM { get => _outtriggerSpanM; set => SetField(ref _outtriggerSpanM, Math.Max(0.1, value)); }
        public double SpanToSpanM { get => _spanToSpanM; set => SetField(ref _spanToSpanM, Math.Max(0.1, value)); }
        public double OutriggerPadSize { get => _outtriggerPadSize; set => SetField(ref _outtriggerPadSize, Math.Clamp(value, 1.0, 4.0)); }
        public bool ShowOutriggerPads { get => _showOutriggerPads; set => SetField(ref _showOutriggerPads, value); }
        public bool IsOnRubber { get => _isOnRubber; set => SetField(ref _isOnRubber, value); }
        public bool ShowDimensionGuide { get => _showDimensionGuide; set => SetField(ref _showDimensionGuide, value); }
        public double BoomLength
        {
            get => _boomLength;
            set
            {
                if (SetField(ref _boomLength, Math.Clamp(value, MinBoomLength, MaxBoomLength)))
                    RecalcWorkingRadius();
            }
        }
        public double MinBoomLength { get => _minBoomLength; set => SetField(ref _minBoomLength, Math.Max(0.1, value)); }
        public double MaxBoomLength { get => _maxBoomLength; set => SetField(ref _maxBoomLength, Math.Max(0.1, value)); }
        public double BoomAngle
        {
            get => _boomAngle;
            set
            {
                if (SetField(ref _boomAngle, Math.Clamp(value, 0.0, 85.0)))
                    RecalcWorkingRadius();
            }
        }
        public double TravelWidthM { get => _travelWidthM; set => SetField(ref _travelWidthM, Math.Max(0.1, value)); }
        public double BodyAngle
        {
            get => _bodyAngle;
            set
            {
                var normalized = value % 360.0;
                if (normalized < 0)
                    normalized += 360.0;

                SetField(ref _bodyAngle, normalized);
            }
        }

        public int BoomSections
        {
            get => _boomSections;
            set => SetField(ref _boomSections, Math.Clamp(value, 1, 6));
        }

        public double BoomBaseWidth { get => _boomBaseWidth; set => SetField(ref _boomBaseWidth, value); }
        public double HookRadius => BoomLength * Math.Cos(BoomAngle * Math.PI / 180.0);
        public double BoomPivotHeight => Math.Max(0.2, CarrierHeight * 0.8);
        public double HookHeight
        {
            get
            {
                var boomTipHeight = BoomPivotHeight + BoomLength * Math.Sin(BoomAngle * Math.PI / 180.0);
                return Math.Max(0, boomTipHeight - CableLength);
            }
        }

        /// <summary>
        /// Horizontal distance from crane center to boom tip = BoomLength × cos(BoomAngle).
        /// Updated automatically when BoomLength or BoomAngle changes.
        /// Can also be set directly (adjusts BoomAngle to match).
        /// </summary>
        public double WorkingRadius
        {
            get => _workingRadius;
            set
            {
                double clamped = ClampWorkingRadius(value);
                if (SetField(ref _workingRadius, clamped))
                {
                    // Back-calculate BoomAngle from working radius
                    double ratio = Math.Clamp(clamped / BoomLength, 0, 1);
                    double newAngle = Math.Acos(ratio) * 180.0 / Math.PI;
                    SetField(ref _boomAngle, newAngle, nameof(BoomAngle));
                }
            }
        }

                private double ClampWorkingRadius(double value)
        {
            double upperBound = BoomLength;

            if (MaxReach > 0.0)
                upperBound = Math.Min(upperBound, MaxReach);

            upperBound = Math.Max(0.0, upperBound);

            double lowerBound = Math.Max(0.0, MinReach);
            if (lowerBound > upperBound)
                lowerBound = upperBound;

            return Math.Clamp(value, lowerBound, upperBound);
        }
private void RecalcWorkingRadius()
        {
            double rad = BoomAngle * Math.PI / 180.0;
            double r = Math.Round(BoomLength * Math.Cos(rad), 2);
            SetField(ref _workingRadius, ClampWorkingRadius(r), nameof(WorkingRadius));
        }
    }
}

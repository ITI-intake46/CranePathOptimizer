using System;

namespace WPF_IFC_test1.Models
{
    public class MobileCraneParameters : CraneParametersBase
    {
        // Outrigger parameters
        private double _outriggerSpread = 7;
        private double _outriggerPadSize = 2.0;
        private bool _showOutriggerPads = true;

        // Chart-driven outrigger geometry (pad-to-pad W, front-to-rear B)
        private double _outriggerSpanM = 7.0;
        private double _spanToSpanM = 7.0;

        // Carrier parameters
        private double _carrierLength = 10;
        private double _carrierWidth = 2.8;
        private double _travelWidthM = 0.0;   // total transport width including tires (0 = not set)
        private double _bodyAngle = 0.0;      // whole-crane orientation offset (0°, 90°, 180°, 270°)
        private double _carrierHeight = 1.2;

        // Boom parameters
        private double _boomLength = 35;
        private double _boomAngle = 60;
        private int _boomSections = 4;
        private double _boomBaseWidth = 0.8;

        public override CraneType Type => CraneType.MobileCrane;
        public override string DisplayName => "Mobile Crane (Outrigger)";

        #region Outrigger Properties

        public double OutriggerSpread
        {
            get => _outriggerSpread;
            set { _outriggerSpread = Math.Max(4, Math.Min(12, value)); OnPropertyChanged(); }
        }

        public double OutriggerPadSize
        {
            get => _outriggerPadSize;
            set { _outriggerPadSize = Math.Max(1, Math.Min(4, value)); OnPropertyChanged(); }
        }

        public bool ShowOutriggerPads
        {
            get => _showOutriggerPads;
            set { _showOutriggerPads = value; OnPropertyChanged(); }
        }

        /// <summary>Pad-to-pad side spread (W) in metres — set from load chart.</summary>
        public double OutriggerSpanM
        {
            get => _outriggerSpanM;
            set { _outriggerSpanM = Math.Max(2.0, Math.Min(20.0, value)); OnPropertyChanged(); }
        }

        /// <summary>Front-to-rear span (B) in metres — set from load chart.</summary>
        public double SpanToSpanM
        {
            get => _spanToSpanM;
            set { _spanToSpanM = Math.Max(2.0, Math.Min(20.0, value)); OnPropertyChanged(); }
        }

        #endregion

        #region Carrier Properties

        public double CarrierLength
        {
            get => _carrierLength;
            set { _carrierLength = Math.Max(6, Math.Min(30, value)); OnPropertyChanged(); }
        }

        public double CarrierWidth
        {
            get => _carrierWidth;
            set { _carrierWidth = Math.Max(2, Math.Min(6, value)); OnPropertyChanged(); }
        }

        /// <summary>
        /// Total transport width including tires (metres).
        /// When set (&gt; 0), overrides CarrierWidth for 3D visual: the body width is
        /// back-calculated so that the wheel outer-tip matches this total width.
        /// 0 means use the default CarrierWidth.
        /// </summary>
        public double TravelWidthM
        {
            get => _travelWidthM;
            set { _travelWidthM = Math.Max(0, Math.Min(10.0, value)); OnPropertyChanged(); }
        }

        /// <summary>
        /// Whole-crane body orientation offset in degrees (0 / 90 / 180 / 270).
        /// Applied as a rotation of the entire 3D group around Z — used by the
        /// "Swap W ↔ L" button to physically rotate the crane 90°.
        /// SlewAngle continues to operate relative to the rotated body.
        /// </summary>
        public double BodyAngle
        {
            get => _bodyAngle;
            set { _bodyAngle = value % 360; if (_bodyAngle < 0) _bodyAngle += 360; OnPropertyChanged(); }
        }

        public double CarrierHeight
        {
            get => _carrierHeight;
            set { _carrierHeight = Math.Max(0.8, Math.Min(2, value)); OnPropertyChanged(); }
        }

        #endregion

        #region Boom Properties

        public double BoomLength
        {
            get => _boomLength;
            set { _boomLength = Math.Max(15, Math.Min(100, value)); OnPropertyChanged(); }
        }

        public double BoomAngle
        {
            get => _boomAngle;
            set { _boomAngle = Math.Max(0, Math.Min(85, value)); OnPropertyChanged(); }
        }

        public int BoomSections
        {
            get => _boomSections;
            set { _boomSections = Math.Max(1, Math.Min(6, value)); OnPropertyChanged(); }
        }

        public double BoomBaseWidth
        {
            get => _boomBaseWidth;
            set { _boomBaseWidth = value; OnPropertyChanged(); }
        }

        #endregion

        #region Calculated Properties

        public override double HookRadius => BoomLength * Math.Cos(BoomAngle * Math.PI / 180);

        public override double HookHeight
        {
            get
            {
                double turntableHeight = CarrierHeight + 1.5; // Carrier + turntable
                double boomTipHeight = turntableHeight + BoomLength * Math.Sin(BoomAngle * Math.PI / 180);
                return Math.Max(0, boomTipHeight - CableLength);
            }
        }

        public override double MaxReach => BoomLength;

        public double TurntableHeight => CarrierHeight + 1.5;

        public double OutriggerFootprint => OutriggerSpread * 2 * Math.Sqrt(2);

        #endregion
    }
}
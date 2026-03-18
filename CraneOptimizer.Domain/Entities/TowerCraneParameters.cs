using System;

namespace WPF_IFC_test1.Models
{
    public class TowerCraneParameters : CraneParametersBase
    {
        private double _towerHeight = 30;
        private double _boomLength = 50;
        private double _boomAngle = 5;
        private double _counterJibLength = 15;
        private double _baseRadius = 3;

        public override CraneType Type => CraneType.TowerCrane;
        public override string DisplayName => "Tower Crane";

        #region Tower Properties

        public double TowerHeight
        {
            get => _towerHeight;
            set { _towerHeight = Math.Max(10, Math.Min(100, value)); OnPropertyChanged(); }
        }

        public double BaseRadius
        {
            get => _baseRadius;
            set { _baseRadius = Math.Max(2, Math.Min(6, value)); OnPropertyChanged(); }
        }

        #endregion

        #region Boom Properties

        public double BoomLength
        {
            get => _boomLength;
            set { _boomLength = Math.Max(20, Math.Min(80, value)); OnPropertyChanged(); }
        }

        public double BoomAngle
        {
            get => _boomAngle;
            set { _boomAngle = Math.Max(-5, Math.Min(30, value)); OnPropertyChanged(); }
        }

        public double CounterJibLength
        {
            get => _counterJibLength;
            set { _counterJibLength = Math.Max(5, Math.Min(30, value)); OnPropertyChanged(); }
        }

        #endregion

        #region Calculated Properties

        public override double HookRadius => BoomLength * Math.Cos(BoomAngle * Math.PI / 180);

        public override double HookHeight
        {
            get
            {
                double boomBaseHeight = 2 + TowerHeight + 2; // Base + Tower + Slew platform
                double boomEndZ = BoomLength * Math.Tan(BoomAngle * Math.PI / 180);
                return Math.Max(0, boomBaseHeight + boomEndZ - CableLength);
            }
        }

        public override double MaxReach => BoomLength;

        public double TotalHeight => 2 + TowerHeight + 10; // Base + Tower + A-frame

        #endregion
    }
}
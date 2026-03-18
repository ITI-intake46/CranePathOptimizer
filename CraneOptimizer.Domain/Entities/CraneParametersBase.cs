using System;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Windows.Media.Media3D;

namespace WPF_IFC_test1.Models
{
    public enum CraneType
    {
        TowerCrane,
        MobileCrane
    }

    public abstract class CraneParametersBase : INotifyPropertyChanged
    {
        public event PropertyChangedEventHandler PropertyChanged;

        // Common properties for all crane types
        private double _slewAngle = 0;
        private double _cableLength = 15;
        private double _positionX = 0;
        private double _positionY = 0;
        private double _positionZ = 0;
        private bool _isVisible = true;
        private bool _showSwingRadius = true;
        private double _exclusionRadiusM = 0.0;
        private int    _assignedChartId  = -1;

        public abstract CraneType Type { get; }
        public abstract string DisplayName { get; }

        #region Slewing & Cable Properties

        public double SlewAngle
        {
            get => _slewAngle;
            set
            {
                _slewAngle = value % 360;
                if (_slewAngle < 0) _slewAngle += 360;
                OnPropertyChanged();
            }
        }

        public double CableLength
        {
            get => _cableLength;
            set { _cableLength = Math.Max(2, Math.Min(80, value)); OnPropertyChanged(); }
        }

        #endregion

        #region Position Properties

        public double PositionX
        {
            get => _positionX;
            set { _positionX = value; OnPropertyChanged(); }
        }

        public double PositionY
        {
            get => _positionY;
            set { _positionY = value; OnPropertyChanged(); }
        }

        public double PositionZ
        {
            get => _positionZ;
            set { _positionZ = value; OnPropertyChanged(); }
        }

        #endregion

        #region Visibility Properties

        public bool IsVisible
        {
            get => _isVisible;
            set { _isVisible = value; OnPropertyChanged(); }
        }

        public bool ShowSwingRadius
        {
            get => _showSwingRadius;
            set { _showSwingRadius = value; OnPropertyChanged(); }
        }

        /// <summary>Exclusion / keep-clear zone radius from the crane CG in metres (0–30 m).
        /// Shown as an orange ring in the 3D view.</summary>
        public double ExclusionRadiusM
        {
            get => _exclusionRadiusM;
            set { _exclusionRadiusM = Math.Max(0.0, Math.Min(30.0, value)); OnPropertyChanged(); }
        }

        /// <summary>
        /// Database ID of the load chart currently assigned to this crane.
        /// Set by CraneManagerPanel when the user picks a chart from the combo.
        /// -1 means no chart assigned.
        /// </summary>
        public int AssignedChartId
        {
            get => _assignedChartId;
            set { _assignedChartId = value; OnPropertyChanged(); }
        }

        #endregion

        #region Abstract Calculated Properties

        public abstract double HookRadius { get; }
        public abstract double HookHeight { get; }
        public abstract double MaxReach { get; }

        public Point3D HookPosition
        {
            get
            {
                double slewRad = SlewAngle * Math.PI / 180;
                double radius = HookRadius;

                return new Point3D(
                    PositionX + radius * Math.Cos(slewRad),
                    PositionY + radius * Math.Sin(slewRad),
                    PositionZ + HookHeight
                );
            }
        }

        #endregion

        protected void OnPropertyChanged([CallerMemberName] string name = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
        }
    }
}
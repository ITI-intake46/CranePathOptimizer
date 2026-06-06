using System.ComponentModel;
using System.Runtime.CompilerServices;

namespace CraneOptimizer.UI.Features.F3_CraneSetup.Parameters
{
    public abstract class CraneParametersBase : INotifyPropertyChanged
    {
        public event PropertyChangedEventHandler? PropertyChanged;

        private double _positionX;
        private double _positionY;
        private double _positionZ;
        private double _slewAngle;
        private double _cableLength = 15.0;
        private bool _isVisible = true;
        private bool _showSwingRadius = true;
        private double _exclusionRadiusM;
        private double _minReach;
        private double _maxReach;
        private double _maxCapacity;

        public abstract UiCraneType Type { get; }

        public double PositionX { get => _positionX; set => SetField(ref _positionX, value); }
        public double PositionY { get => _positionY; set => SetField(ref _positionY, value); }
        public double PositionZ { get => _positionZ; set => SetField(ref _positionZ, value); }
        public double SlewAngle { get => _slewAngle; set => SetField(ref _slewAngle, value); }
        public double CableLength { get => _cableLength; set => SetField(ref _cableLength, value); }
        public bool IsVisible { get => _isVisible; set => SetField(ref _isVisible, value); }
        public bool ShowSwingRadius { get => _showSwingRadius; set => SetField(ref _showSwingRadius, value); }
        public double ExclusionRadiusM { get => _exclusionRadiusM; set => SetField(ref _exclusionRadiusM, value); }
        public double MinReach { get => _minReach; set => SetField(ref _minReach, Math.Max(0.0, value)); }
        public double MaxReach { get => _maxReach; set => SetField(ref _maxReach, Math.Max(0.0, value)); }
        public double MaxCapacity { get => _maxCapacity; set => SetField(ref _maxCapacity, value); }

        protected bool SetField<T>(ref T field, T value, [CallerMemberName] string? name = null)
        {
            if (EqualityComparer<T>.Default.Equals(field, value))
                return false;

            field = value;
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
            return true;
        }
    }
}

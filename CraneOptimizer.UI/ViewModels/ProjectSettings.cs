using System;
using System.ComponentModel;
using System.Runtime.CompilerServices;

namespace WPF_IFC_test1.Models
{
    public class ProjectSettings : INotifyPropertyChanged
    {
        private string _projectName;
        private string _country = "Egypt";
        private double _constructionSiteSize = 200.00;
        private string _projectDescription;
        private string _location;
        private string _unitSystem = "Metric"; // Default
        private string _lengthUnit = "m";
        private string _massUnit = "kg";
        private string _areaUnit = "m²";
        private string _volumeUnit = "m³";
        private string _pressureUnit = "kPa";
        private string _forceUnit = "kN";
        private string _decimalSeparator = ".";
        private bool _showAdvancedParameters = false;

        public string ProjectName
        {
            get => _projectName;
            set { _projectName = value; OnPropertyChanged(); }
        }

        public string Country
        {
            get => _country;
            set { _country = value; OnPropertyChanged(); }
        }

        public double ConstructionSiteSize
        {
            get => _constructionSiteSize;
            set { _constructionSiteSize = value; OnPropertyChanged(); }
        }

        public string ProjectDescription
        {
            get => _projectDescription;
            set { _projectDescription = value; OnPropertyChanged(); }
        }

        public string Location
        {
            get => _location;
            set { _location = value; OnPropertyChanged(); }
        }

        public string UnitSystem
        {
            get => _unitSystem;
            set
            {
                if (_unitSystem != value)
                {
                    _unitSystem = value;
                    OnPropertyChanged();
                    UpdateUnitsForSystem();

                    System.Diagnostics.Debug.WriteLine($"✓ UnitSystem changed to: {_unitSystem}");
                }
            }
        }

        public string LengthUnit
        {
            get => _lengthUnit;
            set { _lengthUnit = value; OnPropertyChanged(); }
        }

        public string MassUnit
        {
            get => _massUnit;
            set { _massUnit = value; OnPropertyChanged(); }
        }

        public string AreaUnit
        {
            get => _areaUnit;
            set { _areaUnit = value; OnPropertyChanged(); }
        }

        public string VolumeUnit
        {
            get => _volumeUnit;
            set { _volumeUnit = value; OnPropertyChanged(); }
        }

        public string PressureUnit
        {
            get => _pressureUnit;
            set { _pressureUnit = value; OnPropertyChanged(); }
        }

        public string ForceUnit
        {
            get => _forceUnit;
            set { _forceUnit = value; OnPropertyChanged(); }
        }

        public string DecimalSeparator
        {
            get => _decimalSeparator;
            set { _decimalSeparator = value; OnPropertyChanged(); }
        }

        public bool ShowAdvancedParameters
        {
            get => _showAdvancedParameters;
            set { _showAdvancedParameters = value; OnPropertyChanged(); }
        }

        private void UpdateUnitsForSystem()
        {
            System.Diagnostics.Debug.WriteLine($"\n=== UpdateUnitsForSystem Called ===");
            System.Diagnostics.Debug.WriteLine($"UnitSystem: {_unitSystem}");

            if (_unitSystem == "Metric")
            {
                LengthUnit = "m";
                MassUnit = "kg";
                AreaUnit = "m²";
                VolumeUnit = "m³";
                PressureUnit = "kPa";
                ForceUnit = "kN";

                System.Diagnostics.Debug.WriteLine("✓ Set to METRIC units");
            }
            else if (_unitSystem == "Imperial")
            {
                LengthUnit = "ft";
                MassUnit = "lb";
                AreaUnit = "ft²";
                VolumeUnit = "ft³";
                PressureUnit = "psi";
                ForceUnit = "lbf";

                System.Diagnostics.Debug.WriteLine("✓ Set to IMPERIAL units");
            }

            System.Diagnostics.Debug.WriteLine($"LengthUnit: {LengthUnit}");
            System.Diagnostics.Debug.WriteLine($"MassUnit: {MassUnit}");
            System.Diagnostics.Debug.WriteLine($"AreaUnit: {AreaUnit}");
            System.Diagnostics.Debug.WriteLine("===========================\n");
        }

        public event PropertyChangedEventHandler PropertyChanged;
        protected virtual void OnPropertyChanged([CallerMemberName] string propertyName = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }
    }
}
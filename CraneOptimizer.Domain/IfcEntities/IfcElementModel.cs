using System;
using System.ComponentModel;
using System.Linq;
using System.Runtime.CompilerServices;

namespace WPF_IFC_test1.Models
{
    public class IfcElementModel : INotifyPropertyChanged
    {
        private string _name;
        private string _displayName;
        private string _globalId;
        private string _mark;
        private string _type;
        private double? _length;
        private double? _width;
        private double? _height;
        private double? _volume;
        private double? _weight;        // Always stored in kg (internal)
        private double? _displayWeight;  // Converted for display
        private string _weightUnit = "kg";
        private string _material;
        private double? _cogX;
        private double? _cogY;
        private double? _cogZ;
        private string _elementColor = "#FFFFFF";
        private bool _isSelected;

        public string Name
        {
            get => _name;
            set { _name = value; OnPropertyChanged(); }
        }

        public string DisplayName
        {
            get => _displayName;
            set { _displayName = value; OnPropertyChanged(); }
        }

        public string GlobalId
        {
            get => _globalId;
            set { _globalId = value; OnPropertyChanged(); }
        }

        public string Mark
        {
            get => _mark;
            set { _mark = value; OnPropertyChanged(); }
        }

        public string Type
        {
            get => _type;
            set { _type = value; OnPropertyChanged(); }
        }

        public double? Length
        {
            get => _length;
            set { _length = value; OnPropertyChanged(); }
        }

        public double? Width
        {
            get => _width;
            set { _width = value; OnPropertyChanged(); }
        }

        public double? Height
        {
            get => _height;
            set { _height = value; OnPropertyChanged(); }
        }

        public double? Volume
        {
            get => _volume;
            set { _volume = value; OnPropertyChanged(); }
        }

        /// <summary>
        /// Weight in kg (internal storage, always kg for weight category checks)
        /// </summary>
        public double? Weight
        {
            get => _weight;
            set { _weight = value; OnPropertyChanged(); }
        }

        /// <summary>
        /// Weight converted for display (kg or lb based on unit system)
        /// </summary>
        public double? DisplayWeight
        {
            get => _displayWeight;
            set { _displayWeight = value; OnPropertyChanged(); }
        }

        /// <summary>
        /// Current weight unit for display ("kg" or "lb")
        /// </summary>
        public string WeightUnit
        {
            get => _weightUnit;
            set { _weightUnit = value; OnPropertyChanged(); }
        }

        public string Material
        {
            get => _material;
            set { _material = value; OnPropertyChanged(); }
        }

        public double? COG_X
        {
            get => _cogX;
            set { _cogX = value; OnPropertyChanged(); }
        }

        public double? COG_Y
        {
            get => _cogY;
            set { _cogY = value; OnPropertyChanged(); }
        }

        public double? COG_Z
        {
            get => _cogZ;
            set { _cogZ = value; OnPropertyChanged(); }
        }

        public string ElementColor
        {
            get => _elementColor;
            set { _elementColor = value; OnPropertyChanged(); }
        }

        public bool IsSelected
        {
            get => _isSelected;
            set { _isSelected = value; OnPropertyChanged(); }
        }

        public static bool LooksLikeGuid(string text)
        {
            if (string.IsNullOrWhiteSpace(text))
                return true;

            if (text.Length == 22 && text.All(c => char.IsLetterOrDigit(c) || c == '_' || c == '$'))
                return true;

            if (Guid.TryParse(text, out _))
                return true;

            if (text.Length > 20 && text.Count(c => c == '-') >= 3)
                return true;

            if (text.Length > 16)
            {
                int hexCount = text.Count(c => "0123456789abcdefABCDEF-".Contains(c));
                if ((double)hexCount / text.Length > 0.8)
                    return true;
            }

            return false;
        }

        public static string GenerateDisplayName(string name, string type, string mark, int entityLabel)
        {
            string cleanType = type?.Replace("Ifc", "") ?? "Element";

            if (!string.IsNullOrWhiteSpace(name) && !LooksLikeGuid(name))
                return name;

            if (!string.IsNullOrWhiteSpace(mark))
                return $"{cleanType} - {mark}";

            return $"{cleanType} #{entityLabel}";
        }

        public event PropertyChangedEventHandler PropertyChanged;

        protected virtual void OnPropertyChanged([CallerMemberName] string propertyName = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }
    }
}
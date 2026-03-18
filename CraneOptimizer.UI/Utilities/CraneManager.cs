using System;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Windows.Media.Media3D;
using WPF_IFC_test1.Models;

namespace WPF_IFC_test1.Utilities
{
    public class CraneInstance : INotifyPropertyChanged
    {
        public event PropertyChangedEventHandler PropertyChanged;

        private string _name;
        private bool _isSelected;

        public string Id { get; }
        public CraneType Type { get; }
        public CraneParametersBase Parameters { get; }
        public ModelVisual3D Visual { get; }
        public ICraneVisual CraneVisual { get; }

        public string Name
        {
            get => _name;
            set { _name = value; OnPropertyChanged(); }
        }

        public bool IsSelected
        {
            get => _isSelected;
            set { _isSelected = value; OnPropertyChanged(); }
        }

        public string DisplayInfo => $"{Name} ({Type})";

        public CraneInstance(string name, CraneType type, CraneParametersBase parameters, ModelVisual3D visual)
        {
            Id = Guid.NewGuid().ToString("N").Substring(0, 8);
            Name = name;
            Type = type;
            Parameters = parameters;
            Visual = visual;
            CraneVisual = visual as ICraneVisual;
        }

        protected void OnPropertyChanged([CallerMemberName] string name = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
        }
    }

    public class CraneManager : INotifyPropertyChanged
    {
        public event PropertyChangedEventHandler PropertyChanged;
        public event Action<CraneInstance> CraneSelected;
        public event Action<string> StatusChanged;

        private CraneInstance _selectedCrane;
        private int _towerCraneCount = 0;
        private int _mobileCraneCount = 0;

        public ObservableCollection<CraneInstance> Cranes { get; } = new ObservableCollection<CraneInstance>();

        public CraneInstance SelectedCrane
        {
            get => _selectedCrane;
            set
            {
                if (_selectedCrane != value)
                {
                    // Deselect previous
                    if (_selectedCrane != null)
                        _selectedCrane.IsSelected = false;

                    _selectedCrane = value;

                    // Select new
                    if (_selectedCrane != null)
                        _selectedCrane.IsSelected = true;

                    OnPropertyChanged();
                    OnPropertyChanged(nameof(HasSelectedCrane));
                    OnPropertyChanged(nameof(SelectedCraneParameters));

                    CraneSelected?.Invoke(_selectedCrane);
                }
            }
        }

        public bool HasSelectedCrane => _selectedCrane != null;

        public CraneParametersBase SelectedCraneParameters => _selectedCrane?.Parameters;

        public int TotalCranes => Cranes.Count;

        public CraneInstance AddCrane(CraneType type)
        {
            // Generate name
            string name;
            if (type == CraneType.TowerCrane)
            {
                _towerCraneCount++;
                name = $"Tower Crane {_towerCraneCount}";
            }
            else
            {
                _mobileCraneCount++;
                name = $"Mobile Crane {_mobileCraneCount}";
            }

            // Create crane using factory
            var (visual, parameters) = CraneFactory.CreateCrane(type);

            if (visual == null || parameters == null)
            {
                return null;
            }

            // Offset position based on existing cranes
            double offsetX = 25 + (Cranes.Count * 15);
            double offsetY = 25 + (Cranes.Count * 10);
            parameters.PositionX = offsetX;
            parameters.PositionY = offsetY;

            var instance = new CraneInstance(name, type, parameters, visual);
            Cranes.Add(instance);

            // Auto-select the new crane
            SelectedCrane = instance;

            OnPropertyChanged(nameof(TotalCranes));
            StatusChanged?.Invoke($"✓ {name} added at ({offsetX:F0}, {offsetY:F0})");

            return instance;
        }

        public void RemoveCrane(CraneInstance crane)
        {
            if (crane == null) return;

            crane.CraneVisual?.Dispose();
            Cranes.Remove(crane);

            // Select another crane if available
            if (_selectedCrane == crane)
            {
                SelectedCrane = Cranes.Count > 0 ? Cranes[Cranes.Count - 1] : null;
            }

            OnPropertyChanged(nameof(TotalCranes));
            StatusChanged?.Invoke($"✓ {crane.Name} removed");
        }

        public void RemoveSelectedCrane()
        {
            RemoveCrane(_selectedCrane);
        }

        public void RemoveAllCranes()
        {
            foreach (var crane in Cranes)
            {
                crane.CraneVisual?.Dispose();
            }

            Cranes.Clear();
            _selectedCrane = null;
            _towerCraneCount = 0;
            _mobileCraneCount = 0;

            OnPropertyChanged(nameof(SelectedCrane));
            OnPropertyChanged(nameof(HasSelectedCrane));
            OnPropertyChanged(nameof(TotalCranes));
            StatusChanged?.Invoke("✓ All cranes removed");
        }

        public void SelectCraneAt(Point3D clickPoint)
        {
            // Find closest crane to click point
            CraneInstance closest = null;
            double minDistance = double.MaxValue;

            foreach (var crane in Cranes)
            {
                double dx = crane.Parameters.PositionX - clickPoint.X;
                double dy = crane.Parameters.PositionY - clickPoint.Y;
                double distance = Math.Sqrt(dx * dx + dy * dy);

                // Check if within crane's reach
                if (distance < crane.Parameters.MaxReach + 10 && distance < minDistance)
                {
                    minDistance = distance;
                    closest = crane;
                }
            }

            if (closest != null)
            {
                SelectedCrane = closest;
            }
        }

        protected void OnPropertyChanged([CallerMemberName] string name = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
        }
    }
}
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Diagnostics;
using System.Linq;
using System.Runtime.CompilerServices;
using WPF_IFC_test1.Models;
using WPF_IFC_test1.Utilities;
using Xbim.Common.Geometry;
using Xbim.Ifc;
using Xbim.Ifc4.Interfaces;
using Xbim.ModelGeometry.Scene;

namespace WPF_IFC_test1.ViewModels
{
    public class MainViewModel : INotifyPropertyChanged
    {
        public ObservableCollection<IfcElementModel> Elements { get; set; }

        /// <summary>
        /// The filtered/sorted view of Elements — bound by the Building Elements panel.
        /// </summary>
        public ObservableCollection<IfcElementModel> FilteredElements { get; private set; }

        private ElementFilter _activeFilter = new ElementFilter();
        public ElementFilter ActiveFilter => _activeFilter;

        private int _filteredCount;
        public int FilteredCount
        {
            get => _filteredCount;
            private set { _filteredCount = value; OnPropertyChanged(); OnPropertyChanged(nameof(CountLabel)); }
        }

        private int _totalCount;
        public int TotalCount
        {
            get => _totalCount;
            private set { _totalCount = value; OnPropertyChanged(); OnPropertyChanged(nameof(CountLabel)); }
        }

        public string CountLabel
        {
            get
            {
                if (TotalCount == 0) return "No elements";
                if (!_activeFilter.IsEmpty && FilteredCount != TotalCount)
                    return $"{FilteredCount} / {TotalCount} elements";
                return $"{TotalCount} elements";
            }
        }

        public bool IsFiltered => !_activeFilter.IsEmpty;

        // ─── IMPORTANT: kept alive after LoadIfc so MainWindow can reuse it ───
        private Xbim3DModelContext _geometryContext;
        private IfcStore _currentModel;

        /// <summary>
        /// Exposes the open IFC store so MainWindow can pass it directly to
        /// IfcHelixLoader — avoids opening the same file a second/third time.
        /// </summary>
        public IfcStore CurrentModel => _currentModel;

        private string _buildingName = "";
        private double _buildingArea;
        private double _buildingHeight;
        private int _numberOfStoreys;
        private string _selectedUnit = "m";
        private string _unitSystem = "Metric";
        private string _areaUnit = "m²";

        public string UnitSystem
        {
            get => _unitSystem;
            set { _unitSystem = value; OnPropertyChanged(); }
        }

        public string SelectedUnit
        {
            get => _selectedUnit;
            set
            {
                if (_selectedUnit != value)
                {
                    _selectedUnit = value;
                    OnPropertyChanged();
                    if (_currentModel != null)
                        ReloadElementsWithNewUnit();
                }
            }
        }

        public string AreaUnit
        {
            get => _areaUnit;
            set { _areaUnit = value; OnPropertyChanged(); }
        }

        public System.Collections.Generic.List<string> AvailableUnits { get; } =
            new System.Collections.Generic.List<string> { "m", "mm", "cm", "ft", "in" };

        public string BuildingName
        {
            get => _buildingName;
            set { _buildingName = value; OnPropertyChanged(); }
        }

        public double BuildingArea
        {
            get => _buildingArea;
            set { _buildingArea = value; OnPropertyChanged(); }
        }

        public double BuildingHeight
        {
            get => _buildingHeight;
            set { _buildingHeight = value; OnPropertyChanged(); }
        }

        public int NumberOfStoreys
        {
            get => _numberOfStoreys;
            set { _numberOfStoreys = value; OnPropertyChanged(); }
        }

        public event PropertyChangedEventHandler PropertyChanged;

        protected virtual void OnPropertyChanged([CallerMemberName] string propertyName = null)
            => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));

        public MainViewModel()
        {
            Elements = new ObservableCollection<IfcElementModel>();
            FilteredElements = new ObservableCollection<IfcElementModel>();
            Elements.CollectionChanged += (s, e) => RefreshFilteredElements();
        }

        // ─────────────────────────────────────────────────────────────────────
        //  Filter management
        // ─────────────────────────────────────────────────────────────────────

        public void ApplyFilter(ElementFilter filter)
        {
            _activeFilter = filter?.Clone() ?? new ElementFilter();
            RefreshFilteredElements();
            OnPropertyChanged(nameof(IsFiltered));
            OnPropertyChanged(nameof(ActiveFilter));
        }

        public void ClearFilter()
        {
            _activeFilter = new ElementFilter();
            RefreshFilteredElements();
            OnPropertyChanged(nameof(IsFiltered));
            OnPropertyChanged(nameof(ActiveFilter));
        }

        private void RefreshFilteredElements()
        {
            var src = Elements.AsEnumerable();
            var f = _activeFilter;

            // ── Search text ──────────────────────────────────────────────────
            if (!string.IsNullOrWhiteSpace(f.SearchText))
            {
                var q = f.SearchText.Trim().ToLowerInvariant();
                src = src.Where(e =>
                    (e.DisplayName?.ToLowerInvariant().Contains(q) == true) ||
                    (e.Type?.ToLowerInvariant().Contains(q) == true) ||
                    (e.Mark?.ToLowerInvariant().Contains(q) == true) ||
                    (e.Material?.ToLowerInvariant().Contains(q) == true));
            }

            // ── Type filter ──────────────────────────────────────────────────
            if (f.AllowedTypes != null && f.AllowedTypes.Count > 0)
                src = src.Where(e => e.Type != null && f.AllowedTypes.Contains(e.Type));

            // ── Material filter ──────────────────────────────────────────────
            if (f.AllowedMaterials != null && f.AllowedMaterials.Count > 0)
                src = src.Where(e => e.Material != null && f.AllowedMaterials.Contains(e.Material));

            // ── Weight range ─────────────────────────────────────────────────
            if (f.MinWeightKg.HasValue)
                src = src.Where(e => e.Weight >= f.MinWeightKg.Value);
            if (f.MaxWeightKg.HasValue)
                src = src.Where(e => e.Weight <= f.MaxWeightKg.Value);

            // ── Height range ─────────────────────────────────────────────────
            if (f.MinHeightM.HasValue)
                src = src.Where(e => e.Height >= f.MinHeightM.Value);
            if (f.MaxHeightM.HasValue)
                src = src.Where(e => e.Height <= f.MaxHeightM.Value);

            // ── Only selected ────────────────────────────────────────────────
            if (f.OnlySelected)
                src = src.Where(e => e.IsSelected);

            // ── Sort ─────────────────────────────────────────────────────────
            src = f.SortField switch
            {
                ElementSortField.Type => f.SortDescending ? src.OrderByDescending(e => e.Type) : src.OrderBy(e => e.Type),
                ElementSortField.WeightAsc => src.OrderBy(e => e.Weight ?? 0),
                ElementSortField.WeightDesc => src.OrderByDescending(e => e.Weight ?? 0),
                ElementSortField.HeightAsc => src.OrderBy(e => e.Height ?? 0),
                ElementSortField.HeightDesc => src.OrderByDescending(e => e.Height ?? 0),
                ElementSortField.Mark => f.SortDescending ? src.OrderByDescending(e => e.Mark) : src.OrderBy(e => e.Mark),
                _ => f.SortDescending ? src.OrderByDescending(e => e.DisplayName) : src.OrderBy(e => e.DisplayName)
            };

            var result = src.ToList();

            FilteredElements.Clear();
            foreach (var el in result)
                FilteredElements.Add(el);

            TotalCount = Elements.Count;
            FilteredCount = FilteredElements.Count;
        }

        /// <summary>Returns distinct element types in the current model.</summary>
        public List<string> GetAvailableTypes()
            => Elements.Select(e => e.Type).Where(t => !string.IsNullOrEmpty(t)).Distinct().OrderBy(t => t).ToList();

        /// <summary>Returns distinct materials in the current model.</summary>
        public List<string> GetAvailableMaterials()
            => Elements.Select(e => e.Material).Where(m => !string.IsNullOrEmpty(m)).Distinct().OrderBy(m => m).ToList();

        /// <summary>Returns the max weight (kg) across all elements.</summary>
        public double GetMaxWeightKg()
            => Elements.Where(e => e.Weight.HasValue).Select(e => e.Weight!.Value).DefaultIfEmpty(0).Max();

        /// <summary>Returns the max height (m) across all elements.</summary>
        public double GetMaxHeightM()
            => Elements.Where(e => e.Height.HasValue).Select(e => e.Height!.Value).DefaultIfEmpty(0).Max();

        public void ClearData()
        {
            BuildingName = "";
            BuildingArea = 0;
            BuildingHeight = 0;
            NumberOfStoreys = 0;
            AreaUnit = "m²";
            Elements.Clear();

            // Dispose geometry context & model cleanly
            _geometryContext = null;
            if (_currentModel != null)
            {
                try { _currentModel.Close(); _currentModel.Dispose(); }
                catch { }
                _currentModel = null;
            }
        }

        /// <summary>
        /// Opens the IFC file, builds a geometry context and populates Elements.
        /// The IfcStore is intentionally kept open in _currentModel so that
        /// MainWindow can hand it to IfcHelixLoader without reopening the file.
        /// Call ClearData() or the overload below when done with the model.
        /// </summary>
        public void LoadIfc(string filePath, Action<string> progressCallback = null)
        {
            Elements.Clear();

            // Close any previously open model
            _geometryContext = null;
            if (_currentModel != null)
            {
                try { _currentModel.Close(); _currentModel.Dispose(); }
                catch { }
                _currentModel = null;
            }

            try
            {
                Debug.WriteLine("\n" + new string('=', 80));
                Debug.WriteLine("LOADING IFC FILE: " + filePath);
                Debug.WriteLine(new string('=', 80));

                progressCallback?.Invoke("Opening IFC file...");
                _currentModel = IfcStore.Open(filePath);

                // Build the geometry context so GetDimensionsFromGeometry / GetCOGFromBoundingBox
                // can compute bounding-box dimensions and enable density-based weight estimation.
                // The 3-D renderer opens the same file in its OWN separate IfcStore instance,
                // so creating the context here does NOT interfere with rendering.
                progressCallback?.Invoke("Building geometry context (for weights / COG)...");
                try
                {
                    _geometryContext = new Xbim3DModelContext(_currentModel);
                    _geometryContext.CreateContext();
                    Debug.WriteLine("✓ Geometry context created for element property extraction");
                }
                catch (Exception gex)
                {
                    // Non-fatal — we still get data from IFC quantity/property sets.
                    Debug.WriteLine($"⚠ Could not create geometry context: {gex.Message}");
                    _geometryContext = null;
                }

                progressCallback?.Invoke("Filtering elements...");

                var products = _currentModel.Instances.OfType<IIfcProduct>()
                    .Where(p =>
                        p is IIfcBuildingElement ||
                        p is IIfcWall || p is IIfcSlab || p is IIfcColumn ||
                        p is IIfcBeam || p is IIfcDoor || p is IIfcWindow ||
                        p is IIfcRoof || p is IIfcStair || p is IIfcRailing ||
                        p is IIfcFooting || p is IIfcPile)
                    .ToList();

                Debug.WriteLine($"Found {products.Count} products to process");

                int count = 0;
                foreach (var p in products)
                {
                    count++;
                    var element = CreateElementModel(p);
                    Elements.Add(element);

                    if (count % 20 == 0)
                        progressCallback?.Invoke($"Processing {count}/{products.Count} elements...");
                }

                Debug.WriteLine($"✓ Loaded {Elements.Count} elements");
                progressCallback?.Invoke($"Loaded {Elements.Count} elements");

                // NOTE: We do NOT null out _geometryContext here and we do NOT
                // force GC.  MainWindow will use _currentModel via CurrentModel
                // to render the 3-D view.  ClearData() handles disposal later.
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"ERROR in LoadIfc: {ex.Message}");
                // Clean up on failure only
                _geometryContext = null;
                if (_currentModel != null)
                {
                    try { _currentModel.Close(); _currentModel.Dispose(); }
                    catch { }
                    _currentModel = null;
                }
                throw;
            }
        }

        private void ReloadElementsWithNewUnit()
        {
            if (_currentModel == null) return;

            var tempElements = Elements.ToList();
            Elements.Clear();

            try
            {
                // Reuse or recreate context
                if (_geometryContext == null)
                {
                    _geometryContext = new Xbim3DModelContext(_currentModel);
                    _geometryContext.CreateContext();
                }

                foreach (var oldElement in tempElements)
                {
                    var product = _currentModel.Instances
                        .OfType<IIfcProduct>()
                        .FirstOrDefault(p => p.GlobalId == oldElement.GlobalId);

                    if (product != null)
                    {
                        var newElement = CreateElementModel(product);
                        newElement.IsSelected = oldElement.IsSelected;
                        newElement.ElementColor = oldElement.ElementColor;
                        newElement.DisplayWeight = oldElement.DisplayWeight;
                        newElement.WeightUnit = oldElement.WeightUnit;
                        Elements.Add(newElement);
                    }
                }

                Debug.WriteLine($"✓ Reloaded {Elements.Count} elements with unit: {SelectedUnit}");
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Error reloading elements: {ex.Message}");
                _geometryContext = null;
                foreach (var old in tempElements)
                    Elements.Add(old);
            }
        }

        // ─────────────────────────────────────────────────────────────────────
        //  Element model building
        // ─────────────────────────────────────────────────────────────────────

        private IfcElementModel CreateElementModel(IIfcProduct p)
        {
            Debug.WriteLine($"\n── Element: {p.Name ?? "Unnamed"} ({p.GetType().Name})");

            double? lengthM = null, widthM = null, heightM = null;
            double? volumeM3 = null, weightKg = null;

            // Dimensions from geometry bounding box
            var geom = GetDimensionsFromGeometry(p);
            lengthM = geom.Length;
            widthM = geom.Width;
            heightM = geom.Height;

            // Volume & Weight from IFC Quantity sets
            var allQSets = p.IsDefinedBy
                .OfType<IIfcRelDefinesByProperties>()
                .Where(r => r.RelatingPropertyDefinition is IIfcElementQuantity)
                .Select(r => r.RelatingPropertyDefinition as IIfcElementQuantity)
                .ToList();

            foreach (var qSet in allQSets)
            {
                foreach (var q in qSet.Quantities)
                {
                    if (q is IIfcQuantityVolume qVol)
                    {
                        double rawVal = qVol.VolumeValue;
                        double valInM3 = rawVal > 100_000 ? rawVal / 1_000_000_000.0 : rawVal;
                        if (valInM3 > 0 && (volumeM3 == null || volumeM3 == 0))
                            volumeM3 = valInM3;
                    }
                    else if (q is IIfcQuantityWeight qWeight)
                    {
                        double rawVal = qWeight.WeightValue;
                        if (weightKg == null && rawVal > 0)
                            weightKg = rawVal;
                    }
                }
            }

            // Fallback: calculate volume from bounding box
            if ((volumeM3 == null || volumeM3 <= 0) &&
                lengthM > 0 && widthM > 0 && heightM > 0)
            {
                volumeM3 = lengthM.Value * widthM.Value * heightM.Value;
            }

            // Fallback 1: search property sets for a weight / mass property
            if (weightKg == null || weightKg == 0)
                weightKg = TryGetWeightFromPropertySets(p);

            // Fallback 2: calculate weight from volume × density
            if ((weightKg == null || weightKg == 0) && volumeM3 > 0)
            {
                // Try material association first, then fall back to guessing from the
                // element's own name (e.g. "Concrete-Rectangular Beam" → concrete).
                var mat = GetMaterial(p);
                if (string.IsNullOrEmpty(mat))
                    mat = p.Name?.ToString();    // element name often contains material hint
                var density = GetMaterialDensity(mat);
                if (density != null)
                    weightKg = volumeM3.Value * density.Value;
            }

            // COG from bounding box midpoint (result in metres)
            var cogM = GetCOGFromBoundingBox(p);

            string rawName = p.Name?.ToString();
            string mark = GetMark(p);
            string typeName = p.ExpressType?.Name;
            int entityLabel = p.EntityLabel;
            string displayName = IfcElementModel.GenerateDisplayName(rawName, typeName, mark, entityLabel);

            var element = new IfcElementModel
            {
                Name = rawName ?? "Unnamed",
                DisplayName = displayName,
                GlobalId = p.GlobalId,
                Mark = mark,
                Type = typeName,
                Length = ConvertToSelectedUnit(lengthM),
                Width = ConvertToSelectedUnit(widthM),
                Height = ConvertToSelectedUnit(heightM),
                Volume = volumeM3,
                Weight = weightKg,
                DisplayWeight = weightKg,
                WeightUnit = "kg",
                Material = GetMaterial(p),

                // COG stored in metres — used by IfcHelixLoader for sphere placement
                COG_X = cogM.X,
                COG_Y = cogM.Y,
                COG_Z = cogM.Z
            };

            Debug.WriteLine($"   → {element.DisplayName} | W={element.Weight:F0} kg | " +
                            $"COG=({element.COG_X:F2}, {element.COG_Y:F2}, {element.COG_Z:F2}) m");
            return element;
        }

        private double? ConvertToSelectedUnit(double? valueInMeters)
        {
            if (!valueInMeters.HasValue) return null;
            return UnitConverter.ConvertLength(valueInMeters.Value, SelectedUnit);
        }

        // ─────────────────────────────────────────────────────────────────────
        //  COG from bounding-box midpoint  (returns METRES)
        // ─────────────────────────────────────────────────────────────────────

        private (double? X, double? Y, double? Z) GetCOGFromBoundingBox(IIfcProduct product)
        {
            try
            {
                if (_geometryContext == null) return (null, null, null);

                var shapes = _geometryContext.ShapeInstancesOf(product).ToList();
                if (!shapes.Any()) return (null, null, null);

                double minX = double.MaxValue, minY = double.MaxValue, minZ = double.MaxValue;
                double maxX = double.MinValue, maxY = double.MinValue, maxZ = double.MinValue;

                foreach (var shape in shapes)
                {
                    var bbox = shape.BoundingBox;
                    var xform = shape.Transformation;

                    // 8 corners of the AABB
                    var corners = new[]
                    {
                        new XbimPoint3D(bbox.Min.X, bbox.Min.Y, bbox.Min.Z),
                        new XbimPoint3D(bbox.Max.X, bbox.Min.Y, bbox.Min.Z),
                        new XbimPoint3D(bbox.Min.X, bbox.Max.Y, bbox.Min.Z),
                        new XbimPoint3D(bbox.Max.X, bbox.Max.Y, bbox.Min.Z),
                        new XbimPoint3D(bbox.Min.X, bbox.Min.Y, bbox.Max.Z),
                        new XbimPoint3D(bbox.Max.X, bbox.Min.Y, bbox.Max.Z),
                        new XbimPoint3D(bbox.Min.X, bbox.Max.Y, bbox.Max.Z),
                        new XbimPoint3D(bbox.Max.X, bbox.Max.Y, bbox.Max.Z)
                    };

                    foreach (var c in corners)
                    {
                        var wc = xform.Transform(c);
                        if (wc.X < minX) minX = wc.X; if (wc.X > maxX) maxX = wc.X;
                        if (wc.Y < minY) minY = wc.Y; if (wc.Y > maxY) maxY = wc.Y;
                        if (wc.Z < minZ) minZ = wc.Z; if (wc.Z > maxZ) maxZ = wc.Z;
                    }
                }

                if (minX == double.MaxValue) return (null, null, null);

                // Convert from IFC internal units to metres using OneMeter
                double om = (_currentModel?.ModelFactors?.OneMeter > 0)
                    ? _currentModel.ModelFactors.OneMeter : 1000.0;
                return (
                    (minX + maxX) / (2.0 * om),
                    (minY + maxY) / (2.0 * om),
                    (minZ + maxZ) / (2.0 * om)
                );
            }
            catch { return (null, null, null); }
        }

        // ─────────────────────────────────────────────────────────────────────
        //  Dimensions from bounding box  (returns METRES)
        // ─────────────────────────────────────────────────────────────────────

        private (double? Length, double? Width, double? Height) GetDimensionsFromGeometry(IIfcProduct product)
        {
            try
            {
                if (_geometryContext == null) return (null, null, null);

                var shapes = _geometryContext.ShapeInstancesOf(product).ToList();
                if (!shapes.Any()) return (null, null, null);

                double minX = double.MaxValue, minY = double.MaxValue, minZ = double.MaxValue;
                double maxX = double.MinValue, maxY = double.MinValue, maxZ = double.MinValue;

                foreach (var shape in shapes)
                {
                    var bbox = shape.BoundingBox;
                    var xform = shape.Transformation;

                    var corners = new[]
                    {
                        new XbimPoint3D(bbox.Min.X, bbox.Min.Y, bbox.Min.Z),
                        new XbimPoint3D(bbox.Max.X, bbox.Min.Y, bbox.Min.Z),
                        new XbimPoint3D(bbox.Min.X, bbox.Max.Y, bbox.Min.Z),
                        new XbimPoint3D(bbox.Max.X, bbox.Max.Y, bbox.Min.Z),
                        new XbimPoint3D(bbox.Min.X, bbox.Min.Y, bbox.Max.Z),
                        new XbimPoint3D(bbox.Max.X, bbox.Min.Y, bbox.Max.Z),
                        new XbimPoint3D(bbox.Min.X, bbox.Max.Y, bbox.Max.Z),
                        new XbimPoint3D(bbox.Max.X, bbox.Max.Y, bbox.Max.Z)
                    };

                    foreach (var c in corners)
                    {
                        var wc = xform.Transform(c);
                        if (wc.X < minX) minX = wc.X; if (wc.X > maxX) maxX = wc.X;
                        if (wc.Y < minY) minY = wc.Y; if (wc.Y > maxY) maxY = wc.Y;
                        if (wc.Z < minZ) minZ = wc.Z; if (wc.Z > maxZ) maxZ = wc.Z;
                    }
                }

                if (minX == double.MaxValue) return (null, null, null);

                // Convert from IFC internal units to metres using OneMeter
                double om = (_currentModel?.ModelFactors?.OneMeter > 0)
                    ? _currentModel.ModelFactors.OneMeter : 1000.0;
                double sX = (maxX - minX) / om;
                double sY = (maxY - minY) / om;
                double sZ = (maxZ - minZ) / om;

                if (product is IIfcColumn)
                    return (Math.Max(sX, sY), Math.Min(sX, sY), sZ);
                if (product is IIfcWall)
                    return sX > sY ? (sX, sY, sZ) : (sY, sX, sZ);

                return (Math.Max(sX, sY), Math.Min(sX, sY), sZ);
            }
            catch { }
            return (null, null, null);
        }

        // ─────────────────────────────────────────────────────────────────────
        //  Property helpers
        // ─────────────────────────────────────────────────────────────────────

        private string GetMark(IIfcProduct product)
        {
            try
            {
                if (product is IIfcElement element && !string.IsNullOrWhiteSpace(element.Tag))
                    return element.Tag;

                var pSets = product.IsDefinedBy
                    .OfType<IIfcRelDefinesByProperties>()
                    .Where(r => r.RelatingPropertyDefinition is IIfcPropertySet)
                    .Select(r => r.RelatingPropertyDefinition as IIfcPropertySet);

                foreach (var pSet in pSets)
                {
                    if (pSet?.HasProperties == null) continue;
                    foreach (var prop in pSet.HasProperties.OfType<IIfcPropertySingleValue>())
                    {
                        var name = prop.Name.ToString().ToLower();
                        if (name.Contains("mark") || name.Contains("tag"))
                            return prop.NominalValue?.ToString();
                    }
                }
            }
            catch { }
            return null;
        }

        /// <summary>
        /// Searches all property sets on the product for a single-value property whose name
        /// contains "weight" or "mass" (case-insensitive) and returns its numeric value in kg.
        /// Returns null if nothing suitable is found.
        /// Many IFC exporters (Tekla, Advance Steel, Revit steel add-ins) write weight as a
        /// plain property rather than a formal IIfcQuantityWeight quantity.
        /// </summary>
        private static double? TryGetWeightFromPropertySets(IIfcProduct product)
        {
            try
            {
                var pSets = product.IsDefinedBy
                    .OfType<IIfcRelDefinesByProperties>()
                    .Where(r => r.RelatingPropertyDefinition is IIfcPropertySet)
                    .Select(r => r.RelatingPropertyDefinition as IIfcPropertySet);

                foreach (var pSet in pSets)
                {
                    if (pSet?.HasProperties == null) continue;
                    foreach (var prop in pSet.HasProperties.OfType<IIfcPropertySingleValue>())
                    {
                        var nameLower = prop.Name.ToString().ToLowerInvariant();

                        // Match "Weight", "Mass", "NetWeight", "GrossWeight", "TotalWeight", etc.
                        if (!nameLower.Contains("weight") && !nameLower.Contains("mass"))
                            continue;

                        // Skip dimensionless ratio properties ("WeightPerLength", "SpecificMass", etc.)
                        // whose unit would be kg/m – we only want total mass in kg
                        if (nameLower.Contains("perlength") || nameLower.Contains("per_length") ||
                            nameLower.Contains("specific") || nameLower.Contains("linear"))
                            continue;

                        var rawStr = prop.NominalValue?.ToString();
                        if (string.IsNullOrEmpty(rawStr)) continue;

                        if (double.TryParse(rawStr,
                            System.Globalization.NumberStyles.Any,
                            System.Globalization.CultureInfo.InvariantCulture,
                            out double val) && val > 0)
                        {
                            Debug.WriteLine($"   [WeightPSet] Found '{prop.Name}' = {val} in '{pSet.Name}'");
                            return val;
                        }
                    }
                }
            }
            catch { }
            return null;
        }

        private double? GetMaterialDensity(string materialName)
        {
            if (string.IsNullOrWhiteSpace(materialName)) return null;
            var m = materialName.ToLower();
            if (m.Contains("concrete") || m.Contains("beton")) return 2400.0;
            if (m.Contains("steel") || m.Contains("metal")) return 7850.0;
            if (m.Contains("wood") || m.Contains("timber")) return 600.0;
            if (m.Contains("brick")) return 1800.0;
            return null;
        }

        private string GetMaterial(IIfcProduct p)
        {
            try
            {
                var relMat = p.HasAssociations?.OfType<IIfcRelAssociatesMaterial>().FirstOrDefault();
                if (relMat == null) return null;

                var matDef = relMat.RelatingMaterial;
                if (matDef is IIfcMaterial mat) return mat.Name;
                if (matDef is IIfcMaterialLayer ml) return ml.Material?.Name;
                if (matDef is IIfcMaterialLayerSet ls)
                    return string.Join(", ", ls.MaterialLayers.Select(l => l.Material?.Name).Where(n => n != null));
                if (matDef is IIfcMaterialList list)
                    return string.Join(", ", list.Materials.Select(m2 => m2.Name.ToString()).Where(n => !string.IsNullOrEmpty(n)));
                if (matDef is IIfcMaterialLayerSetUsage u)
                    return string.Join(", ", u.ForLayerSet.MaterialLayers.Select(l => l.Material?.Name).Where(n => n != null));
                if (matDef is IIfcMaterialProfileSet ps)
                    return string.Join(", ", ps.MaterialProfiles.Select(mp => mp.Material?.Name.ToString()).Where(n => !string.IsNullOrEmpty(n)));
            }
            catch { }
            return null;
        }
    }
}
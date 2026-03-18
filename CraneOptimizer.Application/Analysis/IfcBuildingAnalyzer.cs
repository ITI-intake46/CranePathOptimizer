using System;
using System.Linq;
using Xbim.Ifc;
using Xbim.Ifc4.Interfaces;

namespace WPF_IFC_test1.ViewModel
{
    public class IfcBuildingAnalyzer
    {
        public double BuildingArea { get; private set; }
        public double BuildingHeight { get; private set; }
        public string BuildingName { get; private set; }
        public int NumberOfStoreys { get; private set; }
        public bool IsAnalyzed { get; private set; }

        // ─────────────────────────────────────────────────────────────────────
        //  PUBLIC ENTRY POINTS
        // ─────────────────────────────────────────────────────────────────────

        /// <summary>
        /// Analyzes an IFC file by path.
        /// IMPORTANT: Does NOT call CreateContext() — geometry context must be
        /// created only once, inside LoadIfcToHelix in MainWindow, so that
        /// ShapeInstances() returns the correct meshes for the 3-D viewer.
        /// </summary>
        public void Analyze(string ifcFilePath)
        {
            ValidateInput(ifcFilePath);
            ResetProperties();

            try
            {
                using (var model = IfcStore.Open(ifcFilePath))
                {
                    System.Diagnostics.Debug.WriteLine(
                        $"=== Starting Analysis of: {ifcFilePath} ===");
                    AnalyzeModel(model);
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine("=== CRITICAL ERROR ===");
                System.Diagnostics.Debug.WriteLine(ex.ToString());
                throw new InvalidOperationException(
                    $"Failed to analyze IFC file: {System.IO.Path.GetFileName(ifcFilePath)}", ex);
            }
        }

        /// <summary>
        /// Analyzes an already-open IfcStore (no file I/O, no CreateContext).
        /// </summary>
        public void Analyze(IfcStore model)
        {
            if (model == null)
                throw new ArgumentNullException(nameof(model));

            ResetProperties();

            try
            {
                System.Diagnostics.Debug.WriteLine("=== Starting Analysis from Model ===");
                AnalyzeModel(model);
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine("=== CRITICAL ERROR ===");
                System.Diagnostics.Debug.WriteLine(ex.ToString());
                throw new InvalidOperationException("Failed to analyze IFC model", ex);
            }
        }

        public void Reset() => ResetProperties();

        public string GetSummary()
        {
            if (!IsAnalyzed)
                return "Building has not been analyzed yet";

            return $@"Building Analysis Summary:
--------------------------
Name: {BuildingName}
Total Area: {BuildingArea:F2} m²
Total Height: {BuildingHeight:F2} m
Number of Storeys: {NumberOfStoreys}";
        }

        // ─────────────────────────────────────────────────────────────────────
        //  CORE ANALYSIS  (shared by both public overloads)
        // ─────────────────────────────────────────────────────────────────────

        private void AnalyzeModel(IfcStore model)
        {
            // Building name
            try
            {
                var building = GetBuilding(model);

                BuildingName = building.Name?.ToString()
                            ?? building.LongName?.ToString()
                            ?? "Unnamed Building";
                System.Diagnostics.Debug.WriteLine($"✓ Building Name: {BuildingName}");

                // Storey count
                try
                {
                    NumberOfStoreys = CountStoreys(model);
                    System.Diagnostics.Debug.WriteLine($"✓ Storeys: {NumberOfStoreys}");
                }
                catch (Exception ex)
                {
                    System.Diagnostics.Debug.WriteLine($"✗ Storeys: {ex.Message}");
                    NumberOfStoreys = 0;
                }

                // Area  — NO geometry context, properties / quantities only
                try
                {
                    BuildingArea = CalculateBuildingArea(model, building);
                    System.Diagnostics.Debug.WriteLine($"✓ Area: {BuildingArea:F2} m²");
                }
                catch (Exception ex)
                {
                    System.Diagnostics.Debug.WriteLine($"✗ Area: {ex.Message}");
                    BuildingArea = 0;
                }

                // Height — NO geometry context, storey elevations / properties only
                try
                {
                    BuildingHeight = CalculateBuildingHeight(model, building);
                    System.Diagnostics.Debug.WriteLine($"✓ Height: {BuildingHeight:F2} m");
                }
                catch (Exception ex)
                {
                    System.Diagnostics.Debug.WriteLine($"✗ Height: {ex.Message}");
                    BuildingHeight = 0;
                }

                IsAnalyzed = true;
                System.Diagnostics.Debug.WriteLine("=== Analysis Complete ===");
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"✗ Building: {ex.Message}");
                throw;
            }
        }

        // ─────────────────────────────────────────────────────────────────────
        //  AREA  — pure property/quantity reads, zero geometry calls
        //
        //  *** CalculateFootprintAreaFromGeometry has been REMOVED ***
        //  It called CreateContext() which corrupted xBIM's .wexbim cache
        //  before LoadIfcToHelix ran, causing ShapeInstances() to return 0.
        // ─────────────────────────────────────────────────────────────────────
        private double CalculateBuildingArea(IfcStore model, IIfcBuilding building)
        {
            System.Diagnostics.Debug.WriteLine("--- Calculating Building Area ---");

            // Method 1: Sum storey areas from properties / quantities
            try
            {
                var storeyAreas = model.Instances
                    .OfType<IIfcBuildingStorey>()
                    .Select(s => GetStoreyArea(s, model))
                    .Where(a => a > 0 && a < 1_000_000)
                    .ToList();

                if (storeyAreas.Any())
                {
                    double total = storeyAreas.Sum();
                    System.Diagnostics.Debug.WriteLine(
                        $"Method 1 - Area from Storeys: {total:F2} m²");
                    return total;
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Method 1 Error: {ex.Message}");
            }

            // Method 2: Sum slab areas from quantities
            try
            {
                var slabAreas = model.Instances
                    .OfType<IIfcSlab>()
                    .Select(s => GetElementArea(s, model))
                    .Where(a => a > 0 && a < 1_000_000)
                    .ToList();

                if (slabAreas.Any())
                {
                    double total = slabAreas.Sum();
                    System.Diagnostics.Debug.WriteLine(
                        $"Method 2 - Area from Slabs: {total:F2} m²");
                    return total;
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Method 2 Error: {ex.Message}");
            }

            // Method 3: Building-level property
            try
            {
                var fromProps = GetAreaFromProperties(building, model);
                if (fromProps.HasValue && fromProps.Value > 0)
                {
                    System.Diagnostics.Debug.WriteLine(
                        $"Method 3 - Area from Properties: {fromProps.Value:F2} m²");
                    return fromProps.Value;
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Method 3 Error: {ex.Message}");
            }

            System.Diagnostics.Debug.WriteLine("⚠ Could not calculate area — returning 0");
            return 0;
        }

        // ─────────────────────────────────────────────────────────────────────
        //  HEIGHT  — pure storey-elevation / property reads, zero geometry calls
        //
        //  *** CalculateHeightFromGeometry has been REMOVED ***
        //  Same reason as above.
        // ─────────────────────────────────────────────────────────────────────
        private double CalculateBuildingHeight(IfcStore model, IIfcBuilding building)
        {
            System.Diagnostics.Debug.WriteLine("--- Calculating Building Height ---");

            // Method 1: Storey elevations
            try
            {
                var storeys = model.Instances
                    .OfType<IIfcBuildingStorey>()
                    .Where(s => s.Elevation.HasValue)
                    .OrderBy(s => s.Elevation.Value)
                    .ToList();

                if (storeys.Count >= 2)
                {
                    double minElev = ConvertToMeters(storeys.First().Elevation.Value, model);
                    double maxElev = ConvertToMeters(storeys.Last().Elevation.Value, model);
                    double topH = GetStoreyHeight(storeys.Last(), model);
                    double height = (maxElev - minElev) + topH;
                    System.Diagnostics.Debug.WriteLine(
                        $"Method 1 - Height from Storeys: {height:F2} m");
                    return height;
                }

                if (storeys.Count == 1)
                {
                    double h = GetStoreyHeight(storeys.First(), model);
                    System.Diagnostics.Debug.WriteLine(
                        $"Method 1 - Single storey height: {h:F2} m");
                    return h;
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Method 1 Error: {ex.Message}");
            }

            // Method 2: Building-level property
            try
            {
                var fromProps = GetHeightFromProperties(building, model);
                if (fromProps.HasValue && fromProps.Value > 0)
                {
                    System.Diagnostics.Debug.WriteLine(
                        $"Method 2 - Height from Properties: {fromProps.Value:F2} m");
                    return fromProps.Value;
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Method 2 Error: {ex.Message}");
            }

            System.Diagnostics.Debug.WriteLine("⚠ Could not calculate height — returning 0");
            return 0;
        }

        // ─────────────────────────────────────────────────────────────────────
        //  UNIT CONVERSION  (no geometry, safe to call at any time)
        // ─────────────────────────────────────────────────────────────────────

        private double ConvertToMeters(double value, IfcStore model)
        {
            try
            {
                var project = model.Instances.OfType<IIfcProject>().FirstOrDefault();
                var lengthUnit = project?.UnitsInContext?.Units
                    .OfType<IIfcSIUnit>()
                    .FirstOrDefault(u => u.UnitType == IfcUnitEnum.LENGTHUNIT);

                if (lengthUnit != null)
                {
                    if (lengthUnit.Prefix == IfcSIPrefix.MILLI) return value / 1000.0;
                    if (lengthUnit.Prefix == IfcSIPrefix.CENTI) return value / 100.0;
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"ConvertToMeters: {ex.Message}");
            }
            return value;
        }

        private double ConvertAreaToSquareMeters(double value, IfcStore model)
        {
            try
            {
                var project = model.Instances.OfType<IIfcProject>().FirstOrDefault();
                var lengthUnit = project?.UnitsInContext?.Units
                    .OfType<IIfcSIUnit>()
                    .FirstOrDefault(u => u.UnitType == IfcUnitEnum.LENGTHUNIT);

                if (lengthUnit != null)
                {
                    if (lengthUnit.Prefix == IfcSIPrefix.MILLI) return value / 1_000_000.0;
                    if (lengthUnit.Prefix == IfcSIPrefix.CENTI) return value / 10_000.0;
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"ConvertAreaToSquareMeters: {ex.Message}");
            }
            return value;
        }

        // ─────────────────────────────────────────────────────────────────────
        //  PROPERTY / QUANTITY HELPERS
        // ─────────────────────────────────────────────────────────────────────

        private double GetStoreyArea(IIfcBuildingStorey storey, IfcStore model)
        {
            try
            {
                // Try IIfcPropertySingleValue first
                var areaProp = storey.IsDefinedBy
                    .OfType<IIfcRelDefinesByProperties>()
                    .Where(r => r.RelatingPropertyDefinition != null)
                    .SelectMany(r => r.RelatingPropertyDefinition.PropertySetDefinitions)
                    .OfType<IIfcPropertySet>()
                    .SelectMany(ps => ps.HasProperties)
                    .OfType<IIfcPropertySingleValue>()
                    .FirstOrDefault(p => p.Name.ToString().ToLower().Contains("area"));

                if (areaProp?.NominalValue != null)
                {
                    double val = Convert.ToDouble(areaProp.NominalValue.Value);
                    return ConvertAreaToSquareMeters(val, model);
                }

                // Try IIfcQuantityArea
                var areaQty = storey.IsDefinedBy
                    .OfType<IIfcRelDefinesByProperties>()
                    .Where(r => r.RelatingPropertyDefinition != null)
                    .SelectMany(r => r.RelatingPropertyDefinition.PropertySetDefinitions)
                    .OfType<IIfcElementQuantity>()
                    .SelectMany(eq => eq.Quantities)
                    .OfType<IIfcQuantityArea>()
                    .FirstOrDefault();

                if (areaQty != null)
                    return ConvertAreaToSquareMeters(areaQty.AreaValue, model);
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"GetStoreyArea: {ex.Message}");
            }
            return 0;
        }

        private double GetElementArea(IIfcElement element, IfcStore model)
        {
            try
            {
                var areaQty = element.IsDefinedBy
                    .OfType<IIfcRelDefinesByProperties>()
                    .Where(r => r.RelatingPropertyDefinition != null)
                    .SelectMany(r => r.RelatingPropertyDefinition.PropertySetDefinitions)
                    .OfType<IIfcElementQuantity>()
                    .SelectMany(eq => eq.Quantities)
                    .OfType<IIfcQuantityArea>()
                    .FirstOrDefault();

                if (areaQty != null)
                    return ConvertAreaToSquareMeters(areaQty.AreaValue, model);
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"GetElementArea: {ex.Message}");
            }
            return 0;
        }

        private double? GetAreaFromProperties(IIfcBuilding building, IfcStore model)
        {
            try
            {
                var areaProp = building.IsDefinedBy
                    .OfType<IIfcRelDefinesByProperties>()
                    .Where(r => r.RelatingPropertyDefinition != null)
                    .SelectMany(r => r.RelatingPropertyDefinition.PropertySetDefinitions)
                    .OfType<IIfcPropertySet>()
                    .SelectMany(ps => ps.HasProperties)
                    .OfType<IIfcPropertySingleValue>()
                    .FirstOrDefault(p => p.Name.ToString().ToLower().Contains("area"));

                if (areaProp?.NominalValue != null)
                {
                    double val = Convert.ToDouble(areaProp.NominalValue.Value);
                    return ConvertAreaToSquareMeters(val, model);
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"GetAreaFromProperties: {ex.Message}");
            }
            return null;
        }

        private double GetStoreyHeight(IIfcBuildingStorey storey, IfcStore model)
        {
            try
            {
                var heightProp = storey.IsDefinedBy
                    .OfType<IIfcRelDefinesByProperties>()
                    .Where(r => r.RelatingPropertyDefinition != null)
                    .SelectMany(r => r.RelatingPropertyDefinition.PropertySetDefinitions)
                    .OfType<IIfcPropertySet>()
                    .SelectMany(ps => ps.HasProperties)
                    .OfType<IIfcPropertySingleValue>()
                    .FirstOrDefault(p => p.Name.ToString().ToLower().Contains("height"));

                if (heightProp?.NominalValue != null)
                {
                    double h = Convert.ToDouble(heightProp.NominalValue.Value);
                    return ConvertToMeters(h, model);
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"GetStoreyHeight: {ex.Message}");
            }
            return 3.0; // sensible default
        }

        private double? GetHeightFromProperties(IIfcBuilding building, IfcStore model)
        {
            try
            {
                var heightProp = building.IsDefinedBy
                    .OfType<IIfcRelDefinesByProperties>()
                    .Where(r => r.RelatingPropertyDefinition != null)
                    .SelectMany(r => r.RelatingPropertyDefinition.PropertySetDefinitions)
                    .OfType<IIfcPropertySet>()
                    .SelectMany(ps => ps.HasProperties)
                    .OfType<IIfcPropertySingleValue>()
                    .FirstOrDefault(p => p.Name.ToString().ToLower().Contains("height"));

                if (heightProp?.NominalValue != null)
                {
                    double h = Convert.ToDouble(heightProp.NominalValue.Value);
                    return ConvertToMeters(h, model);
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"GetHeightFromProperties: {ex.Message}");
            }
            return null;
        }

        // ─────────────────────────────────────────────────────────────────────
        //  MISC HELPERS
        // ─────────────────────────────────────────────────────────────────────

        private void ValidateInput(string ifcFilePath)
        {
            if (string.IsNullOrWhiteSpace(ifcFilePath))
                throw new ArgumentNullException(nameof(ifcFilePath),
                    "IFC file path cannot be null or empty");

            if (!System.IO.File.Exists(ifcFilePath))
                throw new System.IO.FileNotFoundException("IFC file not found", ifcFilePath);
        }

        private void ResetProperties()
        {
            BuildingArea = 0;
            BuildingHeight = 0;
            BuildingName = string.Empty;
            NumberOfStoreys = 0;
            IsAnalyzed = false;
        }

        private IIfcBuilding GetBuilding(IfcStore model)
        {
            var building = model.Instances
                .OfType<IIfcBuilding>()
                .FirstOrDefault();

            if (building == null)
                throw new InvalidOperationException(
                    "No IfcBuilding found in the IFC file. " +
                    "The file may contain only geometry without building structure.");

            return building;
        }

        private int CountStoreys(IfcStore model)
        {
            var storeys = model.Instances.OfType<IIfcBuildingStorey>().ToList();
            foreach (var s in storeys)
                System.Diagnostics.Debug.WriteLine(
                    $"  Storey: {s.Name}, Elevation: {s.Elevation?.ToString() ?? "N/A"}");
            return storeys.Count;
        }
    }
}
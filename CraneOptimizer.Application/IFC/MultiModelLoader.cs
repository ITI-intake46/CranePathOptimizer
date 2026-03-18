//using System;
//using System.Collections.Generic;
//using System.IO;
//using System.Linq;
//using System.Windows;
//using System.Windows.Media.Media3D;
//using Xbim.Ifc;
//using Xbim.ModelGeometry.Scene;
//using Xbim.Presentation;

//namespace WPF_IFC_test1
//{
//    /// <summary>
//    /// Load multiple IFC models (e.g., building + crane) in the same scene
//    /// </summary>
//    public class MultiModelLoader
//    {
//        private Xbim.Presentation.DrawingControl3D viewport;
//        private List<IfcStore> loadedModels = new List<IfcStore>();

//        public MultiModelLoader(Xbim.Presentation.DrawingControl3D viewport)
//        {
//            this.viewport = viewport;
//        }

//        /// <summary>
//        /// Load building IFC and crane IFC together
//        /// </summary>
//        public IfcStore LoadBuildingWithCrane(string buildingPath, string cranePath = null,
//            Vector3D cranePosition = default)
//        {
//            try
//            {
//                // Clear existing models
//                ClearModels();

//                // Load building (main model)
//                var buildingModel = LoadModel(buildingPath, new Vector3D(0, 0, 0), "Building");
//                if (buildingModel != null)
//                {
//                    loadedModels.Add(buildingModel);
//                    System.Diagnostics.Debug.WriteLine($"✓ Building loaded: {Path.GetFileName(buildingPath)}");
//                }

//                // Load crane (positioned separately) if provided
//                if (!string.IsNullOrEmpty(cranePath) && File.Exists(cranePath))
//                {
//                    // If no position specified, place crane at building center
//                    if (cranePosition == default)
//                    {
//                        cranePosition = new Vector3D(0, 0, 0);
//                    }

//                    var craneModel = LoadModel(cranePath, cranePosition, "Crane");
//                    if (craneModel != null)
//                    {
//                        loadedModels.Add(craneModel);
//                        System.Diagnostics.Debug.WriteLine($"✓ Crane loaded: {Path.GetFileName(cranePath)}");
//                    }
//                }

//                // Zoom to fit all models - FIXED
//                viewport.ViewHome();

//                return buildingModel; // Return building model as primary
//            }
//            catch (Exception ex)
//            {
//                System.Diagnostics.Debug.WriteLine($"Error loading models: {ex.Message}");
//                MessageBox.Show($"Error loading models:\n{ex.Message}",
//                    "Error", MessageBoxButton.OK, MessageBoxImage.Error);
//                return null;
//            }
//        }

//        /// <summary>
//        /// Load a single IFC model at specified position
//        /// </summary>
//        private IfcStore LoadModel(string ifcPath, Vector3D position, string modelName)
//        {
//            try
//            {
//                System.Diagnostics.Debug.WriteLine($"--- Loading {modelName}: {Path.GetFileName(ifcPath)} ---");

//                // Open IFC file
//                var model = IfcStore.Open(ifcPath);
//                System.Diagnostics.Debug.WriteLine($"IFC Schema: {model.SchemaVersion}");

//                // Create 3D context
//                var context = new Xbim3DModelContext(model);
//                context.CreateContext();

//                // Set model to viewport
//                if (loadedModels.Count == 0)
//                {
//                    // First model (building) - set as main model
//                    viewport.Model = model;
//                }
//                else
//                {
//                    // Additional models (cranes, etc.) - add to scene
//                    // Note: Xbim viewer shows one model at a time by default
//                    // For true multi-model support, you'd need custom rendering
//                    System.Diagnostics.Debug.WriteLine("⚠ Note: Additional models loaded but Xbim shows one model at a time");
//                }

//                System.Diagnostics.Debug.WriteLine($"✓ {modelName} loaded successfully");
//                return model;
//            }
//            catch (Exception ex)
//            {
//                System.Diagnostics.Debug.WriteLine($"✗ {modelName} loading failed: {ex.Message}");
//                MessageBox.Show($"Failed to load {modelName}:\n{ex.Message}",
//                    "Error", MessageBoxButton.OK, MessageBoxImage.Error);
//                return null;
//            }
//        }

//        /// <summary>
//        /// Add crane IFC at specific coordinates
//        /// </summary>
//        public void AddCraneAtPosition(string cranePath, double x, double y, double z)
//        {
//            var cranePosition = new Vector3D(x, y, z);
//            var craneModel = LoadModel(cranePath, cranePosition, "Additional Crane");

//            if (craneModel != null)
//            {
//                loadedModels.Add(craneModel);
//                viewport.ViewHome(); // FIXED

//                MessageBox.Show($"Crane added at position ({x}, {y}, {z})",
//                    "Crane Added", MessageBoxButton.OK, MessageBoxImage.Information);
//            }
//        }

//        /// <summary>
//        /// Add multiple cranes at different positions
//        /// </summary>
//        public void AddMultipleCranes(string cranePath, List<Vector3D> positions)
//        {
//            int successCount = 0;

//            foreach (var position in positions)
//            {
//                var craneModel = LoadModel(cranePath, position, $"Crane at ({position.X}, {position.Y}, {position.Z})");
//                if (craneModel != null)
//                {
//                    loadedModels.Add(craneModel);
//                    successCount++;
//                }
//            }

//            if (successCount > 0)
//            {
//                viewport.ViewHome(); // FIXED
//                MessageBox.Show($"{successCount} crane(s) added successfully!",
//                    "Cranes Added", MessageBoxButton.OK, MessageBoxImage.Information);
//            }
//        }

//        /// <summary>
//        /// Clear all loaded models
//        /// </summary>
//        public void ClearModels()
//        {
//            try
//            {
//                // Close all IFC models
//                foreach (var model in loadedModels)
//                {
//                    try
//                    {
//                        model?.Close();
//                        model?.Dispose();
//                    }
//                    catch (Exception ex)
//                    {
//                        System.Diagnostics.Debug.WriteLine($"Error closing model: {ex.Message}");
//                    }
//                }
//                loadedModels.Clear();

//                // Clear viewport
//                if (viewport != null && viewport.Model != null)
//                {
//                    viewport.Model.Close();
//                    viewport.Model = null;
//                }

//                System.Diagnostics.Debug.WriteLine("✓ All models cleared");
//            }
//            catch (Exception ex)
//            {
//                System.Diagnostics.Debug.WriteLine($"Error clearing models: {ex.Message}");
//            }
//        }

//        /// <summary>
//        /// Get list of loaded models
//        /// </summary>
//        public List<IfcStore> GetLoadedModels()
//        {
//            return loadedModels;
//        }

//        /// <summary>
//        /// Get count of loaded models
//        /// </summary>
//        public int GetModelCount()
//        {
//            return loadedModels.Count;
//        }

//        /// <summary>
//        /// Helper method to zoom viewport to fit all content
//        /// </summary>
//        private void ZoomToFit()
//        {
//            try
//            {
//                viewport.ViewHome();
//            }
//            catch (Exception ex)
//            {
//                System.Diagnostics.Debug.WriteLine($"Error zooming to fit: {ex.Message}");
//            }
//        }
//    }
//}
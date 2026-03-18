using System;
using System.IO;
using System.Linq;
using System.Windows;
using Xbim.Common;
using Xbim.Ifc;
using Xbim.Ifc4.Interfaces;

namespace WPF_IFC_test1
{
    /// <summary>
    /// Merge multiple IFC files into one
    /// Useful for permanently combining building + crane models
    /// </summary>
    public class IfcMerger
    {
        /// <summary>
        /// Merge building IFC with crane IFC into a new file
        /// </summary>
        public static bool MergeBuildingAndCrane(
            string buildingPath,
            string cranePath,
            string outputPath,
            double offsetX = 0,
            double offsetY = 0,
            double offsetZ = 0)
        {
            try
            {
                System.Diagnostics.Debug.WriteLine("\n=== Starting IFC Merge ===");
                System.Diagnostics.Debug.WriteLine($"Building: {Path.GetFileName(buildingPath)}");
                System.Diagnostics.Debug.WriteLine($"Crane: {Path.GetFileName(cranePath)}");
                System.Diagnostics.Debug.WriteLine($"Output: {Path.GetFileName(outputPath)}");

                // Create new IFC model
                using (var targetModel = IfcStore.Create(null, Xbim.Common.Step21.XbimSchemaVersion.Ifc4, Xbim.IO.XbimStoreType.InMemoryModel))
                {
                    using (var txn = targetModel.BeginTransaction("Merge Models"))
                    {
                        // Load building model
                        System.Diagnostics.Debug.WriteLine("\nCopying building elements...");
                        using (var buildingModel = IfcStore.Open(buildingPath))
                        {
                            // Copy building to target
                            var mapping = new XbimInstanceHandleMap(buildingModel, targetModel);

                            int count = 0;
                            foreach (var item in buildingModel.Instances)
                            {
                                targetModel.InsertCopy(item, mapping, null, false, false);
                                count++;
                            }

                            System.Diagnostics.Debug.WriteLine($"✓ Copied {count} building elements");
                        }

                        // Load crane model
                        if (File.Exists(cranePath))
                        {
                            System.Diagnostics.Debug.WriteLine("\nCopying crane elements...");
                            using (var craneModel = IfcStore.Open(cranePath))
                            {
                                // Copy crane to target
                                var mapping = new XbimInstanceHandleMap(craneModel, targetModel);

                                int count = 0;
                                foreach (var item in craneModel.Instances)
                                {
                                    targetModel.InsertCopy(item, mapping, null, false, false);
                                    count++;
                                }

                                System.Diagnostics.Debug.WriteLine($"✓ Copied {count} crane elements");
                            }
                        }

                        txn.Commit();
                    }

                    // Save merged model
                    System.Diagnostics.Debug.WriteLine("\nSaving merged model...");
                    targetModel.SaveAs(outputPath);

                    System.Diagnostics.Debug.WriteLine("✓ Merge complete!");

                    MessageBox.Show(
                        $"Models merged successfully!\n\nOutput: {Path.GetFileName(outputPath)}\n\nYou can now load the merged file.",
                        "Success",
                        MessageBoxButton.OK,
                        MessageBoxImage.Information);

                    return true;
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"\n✗ Merge failed: {ex.Message}");
                MessageBox.Show(
                    $"Error merging IFC files:\n{ex.Message}",
                    "Error",
                    MessageBoxButton.OK,
                    MessageBoxImage.Error);

                return false;
            }
        }

        /// <summary>
        /// Merge multiple IFC files into one
        /// </summary>
        public static bool MergeMultipleFiles(string[] ifcPaths, string outputPath)
        {
            try
            {
                if (ifcPaths == null || ifcPaths.Length < 2)
                {
                    MessageBox.Show("At least 2 IFC files are required for merging.",
                        "Error", MessageBoxButton.OK, MessageBoxImage.Error);
                    return false;
                }

                System.Diagnostics.Debug.WriteLine($"\n=== Merging {ifcPaths.Length} IFC Files ===");

                // Create new IFC model
                using (var targetModel = IfcStore.Create(null, Xbim.Common.Step21.XbimSchemaVersion.Ifc4, Xbim.IO.XbimStoreType.InMemoryModel))
                {
                    using (var txn = targetModel.BeginTransaction("Merge Multiple Models"))
                    {
                        foreach (var ifcPath in ifcPaths)
                        {
                            if (!File.Exists(ifcPath))
                            {
                                System.Diagnostics.Debug.WriteLine($"⚠ File not found: {ifcPath}");
                                continue;
                            }

                            System.Diagnostics.Debug.WriteLine($"\nCopying: {Path.GetFileName(ifcPath)}");

                            using (var sourceModel = IfcStore.Open(ifcPath))
                            {
                                var mapping = new XbimInstanceHandleMap(sourceModel, targetModel);

                                int count = 0;
                                foreach (var item in sourceModel.Instances)
                                {
                                    targetModel.InsertCopy(item, mapping, null, false, false);
                                    count++;
                                }

                                System.Diagnostics.Debug.WriteLine($"✓ Copied {count} elements");
                            }
                        }

                        txn.Commit();
                    }

                    // Save merged model
                    System.Diagnostics.Debug.WriteLine("\nSaving merged model...");
                    targetModel.SaveAs(outputPath);

                    System.Diagnostics.Debug.WriteLine("✓ Merge complete!");

                    MessageBox.Show(
                        $"Successfully merged {ifcPaths.Length} IFC files!\n\nOutput: {Path.GetFileName(outputPath)}",
                        "Success",
                        MessageBoxButton.OK,
                        MessageBoxImage.Information);

                    return true;
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"\n✗ Merge failed: {ex.Message}");
                MessageBox.Show(
                    $"Error merging IFC files:\n{ex.Message}",
                    "Error",
                    MessageBoxButton.OK,
                    MessageBoxImage.Error);

                return false;
            }
        }
    }
}
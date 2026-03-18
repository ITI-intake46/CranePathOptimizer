using System;
using System.Windows;
using System.Windows.Controls;
using WPF_IFC_test1.Models;

namespace WPF_IFC_test1
{
    public partial class CreateProjectControl : UserControl
    {
        public ProjectSettings ProjectSettings { get; private set; }

        public CreateProjectControl()
        {
            InitializeComponent();

            // Initialize ProjectSettings FIRST
            ProjectSettings = new ProjectSettings();

            // Set default values BEFORE setting DataContext
            ProjectSettings.UnitSystem = "Metric";
            ProjectSettings.Country = "Egypt";
            ProjectSettings.LengthUnit = "m";
            ProjectSettings.MassUnit = "kg";
            ProjectSettings.PressureUnit = "kPa";
            ProjectSettings.ForceUnit = "kN";
            ProjectSettings.AreaUnit = "m²";
            ProjectSettings.VolumeUnit = "m³";
            ProjectSettings.DecimalSeparator = ".";

            // NOW set DataContext
            DataContext = ProjectSettings;
        }

        private void ShowAdvanced_Changed(object sender, RoutedEventArgs e)
        {
            if (AdvancedParametersPanel != null)
            {
                AdvancedParametersPanel.Visibility = chkShowAdvanced.IsChecked == true
                    ? Visibility.Visible
                    : Visibility.Collapsed;
            }
        }

        private void UnitSystem_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            // ✅ CHECK if ProjectSettings is initialized
            if (ProjectSettings == null || cmbUnits == null)
                return;

            // ✅ FIXED: Extract the actual string value from the selected item
            string selectedSystem = null;

            if (cmbUnits.SelectedItem is ComboBoxItem selectedItem)
            {
                // If ComboBoxItem is used in XAML
                selectedSystem = selectedItem.Content?.ToString();
            }
            else if (cmbUnits.SelectedItem is string str)
            {
                // If sys:String is used in XAML
                selectedSystem = str;
            }

            if (string.IsNullOrEmpty(selectedSystem))
                return;

            // ✅ Set the UnitSystem - this triggers UpdateUnitsForSystem() in ProjectSettings
            ProjectSettings.UnitSystem = selectedSystem;

            System.Diagnostics.Debug.WriteLine($"\n=== UnitSystem_SelectionChanged ===");
            System.Diagnostics.Debug.WriteLine($"✓ Unit system changed to: {ProjectSettings.UnitSystem}");
            System.Diagnostics.Debug.WriteLine($"  Length: {ProjectSettings.LengthUnit}");
            System.Diagnostics.Debug.WriteLine($"  Mass: {ProjectSettings.MassUnit}");
            System.Diagnostics.Debug.WriteLine($"  Area: {ProjectSettings.AreaUnit}");
            System.Diagnostics.Debug.WriteLine($"  Volume: {ProjectSettings.VolumeUnit}");
            System.Diagnostics.Debug.WriteLine($"  Pressure: {ProjectSettings.PressureUnit}");
            System.Diagnostics.Debug.WriteLine($"  Force: {ProjectSettings.ForceUnit}");
            System.Diagnostics.Debug.WriteLine($"===================================\n");
        }

        private void Create_Click(object sender, RoutedEventArgs e)
        {
            // Validate
            if (string.IsNullOrWhiteSpace(ProjectSettings.ProjectName))
            {
                MessageBox.Show("Please enter a project name.", "Validation Error",
                    MessageBoxButton.OK, MessageBoxImage.Warning);
                txtProjectName.Focus();
                return;
            }

            if (string.IsNullOrWhiteSpace(ProjectSettings.Country))
            {
                MessageBox.Show("Please select a country.", "Validation Error",
                    MessageBoxButton.OK, MessageBoxImage.Warning);
                cmbCountry.Focus();
                return;
            }

            try
            {
                // Save globally
                App.CurrentProjectSettings = ProjectSettings;

                System.Diagnostics.Debug.WriteLine("\n=== Project Created ===");
                System.Diagnostics.Debug.WriteLine($"Name: {ProjectSettings.ProjectName}");
                System.Diagnostics.Debug.WriteLine($"Country: {ProjectSettings.Country}");
                System.Diagnostics.Debug.WriteLine($"Unit System: {ProjectSettings.UnitSystem}");
                System.Diagnostics.Debug.WriteLine($"Length Unit: {ProjectSettings.LengthUnit}");
                System.Diagnostics.Debug.WriteLine($"Mass Unit: {ProjectSettings.MassUnit}");
                System.Diagnostics.Debug.WriteLine($"Area Unit: {ProjectSettings.AreaUnit}");
                System.Diagnostics.Debug.WriteLine($"Volume Unit: {ProjectSettings.VolumeUnit}");
                System.Diagnostics.Debug.WriteLine($"Pressure Unit: {ProjectSettings.PressureUnit}");
                System.Diagnostics.Debug.WriteLine($"Force Unit: {ProjectSettings.ForceUnit}");
                System.Diagnostics.Debug.WriteLine($"========================\n");

                // Open MainWindow with settings
                var mainWindow = new MainWindow();
                mainWindow.ApplyProjectSettings(ProjectSettings);
                mainWindow.Show();

                // Close welcome page
                Window parentWindow = Window.GetWindow(this);
                if (parentWindow != null)
                {
                    parentWindow.Close();
                }
            }
            catch (Exception ex)
            {
                // Walk the full inner-exception chain so the real SQLite / init error is visible
                var sb = new System.Text.StringBuilder();
                var current = ex;
                int depth = 0;
                while (current != null)
                {
                    sb.AppendLine(depth == 0 ? current.Message : $"  ➜  {current.Message}");
                    current = current.InnerException;
                    depth++;
                }
                string fullMsg = sb.ToString().TrimEnd();
                MessageBox.Show($"Error creating project:\n{fullMsg}",
                    "Error", MessageBoxButton.OK, MessageBoxImage.Error);
                System.Diagnostics.Debug.WriteLine($"Error creating project: {fullMsg}");
            }
        }
    }
}
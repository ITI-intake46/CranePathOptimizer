using System;
using System.Windows;
using WPF_IFC_test1.Models;
using WPF_IFC_test1.ViewModels;

namespace WPF_IFC_test1
{
    public partial class MainWindow
    {
        #region Save Project

        private void SaveProject_Click(object sender, RoutedEventArgs e)
        {
            if (string.IsNullOrEmpty(_currentIfcFilePath))
            {
                MessageBox.Show("No IFC loaded.");
                return;
            }

            try
            {
                RecentProjectsHelper.AddProject(
                    _currentIfcFilePath,
                    _currentProjectName,
                    "Active");
                MessageBox.Show($"Project '{_currentProjectName}' saved!");
                StatusText.Text = "✓ Project saved";
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Save failed:\n{ex.Message}");
            }
        }

        #endregion

        #region Navigation

        private void BackToWelcome_Click(object sender, RoutedEventArgs e)
        {
            if (MessageBox.Show("Return to Welcome?", "Back", MessageBoxButton.YesNo) != MessageBoxResult.Yes)
                return;

            Hide();
            var welcomePage = new WelcomePage();
            welcomePage.SetReturnToProject(this);
            welcomePage.ShowDialog();

            if (!welcomePage.ShouldReturnToProject)
                Close();
            else
            {
                Show();
                StatusText.Text = "✓ Returned";
            }
        }

        #endregion

        #region Project Settings

        public void ApplyProjectSettings(ProjectSettings settings)
        {
            if (settings == null) return;

            try
            {
                _viewModel.UnitSystem = settings.UnitSystem;
                _viewModel.SelectedUnit = settings.LengthUnit;
                _viewModel.AreaUnit = settings.AreaUnit;

                Dispatcher.BeginInvoke(
                    new Action(() => UnitSelector.SelectedItem = _viewModel.SelectedUnit),
                    System.Windows.Threading.DispatcherPriority.Loaded);

                if (_analyzer?.IsAnalyzed == true)
                    UpdateAnalysisUI();

                if (_viewModel.Elements?.Count > 0)
                    ColorizeElementsByWeight();

                if (!string.IsNullOrEmpty(settings.ProjectName))
                    Title = $"IFC Viewer - {settings.ProjectName}";

                StatusText.Text = $"✓ Settings: {settings.UnitSystem} ({settings.LengthUnit})";
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Settings error:\n{ex.Message}");
            }
        }

        #endregion
    }
}

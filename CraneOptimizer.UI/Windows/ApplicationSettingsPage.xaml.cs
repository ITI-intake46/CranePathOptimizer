using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;

namespace WPF_IFC_test1
{
    public partial class ApplicationSettingsPage:Window
    {
        public ApplicationSettingsPage()
        {
            InitializeComponent();
            LoadSettings();
        }

        #region Load Settings
        private void LoadSettings()
        {
            // Load saved settings here if needed
            // For example, from app.config or a settings file
        }
        #endregion

        #region Window Controls
        private void TitleBar_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            if (e.ClickCount == 2)
            {
                MaximizeWindow_Click(null, null);
            }
            else
            {
                try { DragMove(); } catch { }
            }
        }

        private void MinimizeWindow_Click(object sender, RoutedEventArgs e)
        {
            WindowState = WindowState.Minimized;
        }

        private void MaximizeWindow_Click(object sender, RoutedEventArgs e)
        {
            if (WindowState == WindowState.Maximized)
            {
                WindowState = WindowState.Normal;
            }
            else
            {
                WindowState = WindowState.Maximized;
            }
        }

        private void CloseWindow_Click(object sender, RoutedEventArgs e)
        {
            this.Close();
        }
        #endregion

        #region Navigation
        private void UserSettings_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                var userSettingsPage = new UserSettingsPage();
                userSettingsPage.Show();
                this.Close();
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Error opening User Settings:\n{ex.Message}",
                    "Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }
        #endregion

        #region Settings Controls
        private void SafetyFactorSlider_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
        {
            // Update the text display with the current slider value
            if (SafetyFactorValue != null)
            {
                SafetyFactorValue.Text = $"{(int)e.NewValue}%";
            }
        }

        private void Save_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                // Gather all settings
                string language = ((ComboBoxItem)LanguageComboBox.SelectedItem).Content.ToString();
                string theme = ((ComboBoxItem)ThemeComboBox.SelectedItem).Content.ToString();
                string fontSize = ((ComboBoxItem)FontSizeComboBox.SelectedItem).Content.ToString();
                int safetyFactor = (int)SafetyFactorSlider.Value;
                string graphicQuality = ((ComboBoxItem)GraphicQualityComboBox.SelectedItem).Content.ToString();
                string units = ((ComboBoxItem)UnitsComboBox.SelectedItem).Content.ToString();
                bool showTooltips = TooltipsShowRadio.IsChecked == true;
                bool saveBehavior = BehaviorAllowRadio.IsChecked == true;

                // TODO: Save these settings to a configuration file or database
                // For now, just show a confirmation message
                MessageBox.Show(
                    "Settings saved successfully!\n\n" +
                    $"Language: {language}\n" +
                    $"Theme: {theme}\n" +
                    $"Font Size: {fontSize}\n" +
                    $"Safety Factor: {safetyFactor}%\n" +
                    $"Graphic Quality: {graphicQuality}\n" +
                    $"Units: {units}\n" +
                    $"Tooltips: {(showTooltips ? "Show" : "Hide")}\n" +
                    $"Save User Behavior: {(saveBehavior ? "Allow" : "Deny")}",
                    "Settings Saved",
                    MessageBoxButton.OK,
                    MessageBoxImage.Information);

                // Optionally close the window after saving
                // this.Close();
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Error saving settings:\n{ex.Message}",
                    "Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void Cancel_Click(object sender, RoutedEventArgs e)
        {
            var result = MessageBox.Show(
                "Are you sure you want to cancel?\n\nAny unsaved changes will be lost.",
                "Cancel Confirmation",
                MessageBoxButton.YesNo,
                MessageBoxImage.Question);

            if (result == MessageBoxResult.Yes)
            {
                this.Close();
            }
        }
        #endregion
    }
}
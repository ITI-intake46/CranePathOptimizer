using System;
using System.Windows;
using System.Windows.Input;

namespace WPF_IFC_test1
{
    public partial class UserSettingsPage : Window
    {
        public UserSettingsPage()
        {
            InitializeComponent();
            LoadUserData();
        }

        #region Load User Data
        private void LoadUserData()
        {
            // In a real application, you would load this from a database or settings file
            // For now, we'll use the default values set in XAML
            // You can also set the offline date dynamically
            OfflineUntilText.Text = DateTime.Now.AddDays(30).ToString("MM/dd/yyyy HH:mm:ss zzz");
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

        #region Button Handlers
        private void ApplicationSettings_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                var appSettingsPage = new ApplicationSettingsPage();
                appSettingsPage.Show();
                this.Close();
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Error opening Application Settings:\n{ex.Message}",
                    "Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void Logout_Click(object sender, RoutedEventArgs e)
        {
            var result = MessageBox.Show(
                "Are you sure you want to logout?\n\nYou will need to login again to access the application.",
                "Confirm Logout",
                MessageBoxButton.YesNo,
                MessageBoxImage.Question);

            if (result == MessageBoxResult.Yes)
            {
                // Show logout successful message
                MessageBox.Show(
                    "Logout successful!\n\nThank you for using Crane Optimization.",
                    "Logged Out",
                    MessageBoxButton.OK,
                    MessageBoxImage.Information);

                // In a real application, you would:
                // 1. Clear session data
                // 2. Clear authentication tokens
                // 3. Return to login screen
                // For now, we'll just close the window
                this.Close();
            }
        }

        private void LicenseInfo_Click(object sender, MouseButtonEventArgs e)
        {
            var licenseInfo = @"License Information

License Type: Crane Planner Free
Status: ACTIVE

User: mustafa abdo
Email: ispgt71e@uiebehrr.extranet

Offline Mode:
Available until: 01/13/2026 17:13:40 +02:00

Features Included:
✓ IFC Building Viewer
✓ Element Weight Analysis
✓ Basic Crane Planning
✓ Export Reports

For Premium features, please upgrade to:
• Crane Planner Pro
• Crane Planner Enterprise

Contact: support@craneplanner.com";

            MessageBox.Show(licenseInfo, "License Information",
                MessageBoxButton.OK, MessageBoxImage.Information);
        }
        #endregion
    }
}
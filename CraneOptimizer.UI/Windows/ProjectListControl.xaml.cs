using System;
using System.Windows;
using System.Windows.Controls;

namespace WPF_IFC_test1
{
    public partial class ProjectListControl : UserControl
    {
        public ProjectListControl()
        {
            InitializeComponent();
        }

        private void CreateProject_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                // Find the main window and close it
                Window parentWindow = Window.GetWindow(this);

                var mainWindow = new MainWindow();
                mainWindow.Show();

                if (parentWindow != null)
                {
                    parentWindow.Close();
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Error creating project:\n{ex.Message}",
                    "Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void UpdateList_Click(object sender, RoutedEventArgs e)
        {
            MessageBox.Show("Project list updated!\n\nCurrently showing: 0 projects",
                "Update List",
                MessageBoxButton.OK,
                MessageBoxImage.Information);
        }

        private void LicenseInfo_Click(object sender, RoutedEventArgs e)
        {
            var licenseInfo = @"License Information

License Type: Crane Planner Free
Status: ACTIVE
User: mustafa abdo

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
    }
}
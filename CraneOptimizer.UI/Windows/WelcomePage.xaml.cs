using System;
using System.Windows;

namespace WPF_IFC_test1
{
    public partial class WelcomePage : Window
    {
        private bool isSettingsExpanded = false;
        private MainWindow _returnToMainWindow = null;
        public bool ShouldReturnToProject { get; private set; } = false;

        public WelcomePage()
        {
            InitializeComponent();
        }

        public void SetReturnToProject(MainWindow mainWindow)
        {
            _returnToMainWindow = mainWindow;
        }

        #region Window Controls

        private void TitleBar_MouseLeftButtonDown(object sender, System.Windows.Input.MouseButtonEventArgs e)
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
                MaximizeButton.Content = "☐";
            }
            else
            {
                WindowState = WindowState.Maximized;
                MaximizeButton.Content = "❐";
            }
        }

        private void CloseWindow_Click(object sender, RoutedEventArgs e)
        {
            if (_returnToMainWindow != null)
            {
                var result = MessageBox.Show(
                    "You have an open project. Do you want to return to it?",
                    "Return to Project?",
                    MessageBoxButton.YesNoCancel,
                    MessageBoxImage.Question);

                if (result == MessageBoxResult.Yes)
                {
                    ShouldReturnToProject = true;
                    this.Close();
                }
                else if (result == MessageBoxResult.No)
                {
                    ShouldReturnToProject = false;
                    Application.Current.Shutdown();
                }
            }
            else
            {
                Application.Current.Shutdown();
            }
        }

        #endregion

        #region Content Switching

        private void ShowWelcomeContent()
        {
            WelcomeContent.Visibility = Visibility.Visible;
            ProjectListContent.Visibility = Visibility.Collapsed;
            RecentProjectsContent.Visibility = Visibility.Collapsed;
            ImportProjectContent.Visibility = Visibility.Collapsed;

            WelcomeButton.Style = (Style)FindResource("SelectedMenuButton");
            ProjectListButton.Style = (Style)FindResource("MenuButton");
            RecentProjectsButton.Style = (Style)FindResource("MenuButton");
        }

        private void ShowProjectListContent()
        {
            WelcomeContent.Visibility = Visibility.Collapsed;
            ProjectListContent.Visibility = Visibility.Visible;
            RecentProjectsContent.Visibility = Visibility.Collapsed;
            ImportProjectContent.Visibility = Visibility.Collapsed;

            WelcomeButton.Style = (Style)FindResource("MenuButton");
            ProjectListButton.Style = (Style)FindResource("SelectedMenuButton");
            RecentProjectsButton.Style = (Style)FindResource("MenuButton");
        }

        private void ShowRecentProjectsContent()
        {
            WelcomeContent.Visibility = Visibility.Collapsed;
            ProjectListContent.Visibility = Visibility.Collapsed;
            RecentProjectsContent.Visibility = Visibility.Visible;
            ImportProjectContent.Visibility = Visibility.Collapsed;

            WelcomeButton.Style = (Style)FindResource("MenuButton");
            ProjectListButton.Style = (Style)FindResource("MenuButton");
            RecentProjectsButton.Style = (Style)FindResource("SelectedMenuButton");
        }

        private void ShowImportProjectContent()
        {
            WelcomeContent.Visibility = Visibility.Collapsed;
            ProjectListContent.Visibility = Visibility.Collapsed;
            RecentProjectsContent.Visibility = Visibility.Collapsed;
            ImportProjectContent.Visibility = Visibility.Visible;

            WelcomeButton.Style = (Style)FindResource("MenuButton");
            ProjectListButton.Style = (Style)FindResource("MenuButton");
            RecentProjectsButton.Style = (Style)FindResource("MenuButton");
        }

        #endregion

        #region Menu Click Handlers

        private void Welcome_Click(object sender, RoutedEventArgs e)
        {
            ShowWelcomeContent();
        }

        private void ProjectList_Click(object sender, RoutedEventArgs e)
        {
            ShowProjectListContent();
        }

        private void CreateProject_Click(object sender, RoutedEventArgs e)
        {
            // Show the Create Project form instead of opening MainWindow directly
            ShowCreateProjectContent();
        }

        private void ShowCreateProjectContent()
        {
            WelcomeContent.Visibility = Visibility.Collapsed;
            ProjectListContent.Visibility = Visibility.Collapsed;
            RecentProjectsContent.Visibility = Visibility.Collapsed;
            ImportProjectContent.Visibility = Visibility.Collapsed;
            CreateProjectContent.Visibility = Visibility.Visible;

            WelcomeButton.Style = (Style)FindResource("MenuButton");
            ProjectListButton.Style = (Style)FindResource("MenuButton");
            RecentProjectsButton.Style = (Style)FindResource("MenuButton");
        }

        private void RecentProjects_Click(object sender, RoutedEventArgs e)
        {
            ShowRecentProjectsContent();
        }

        private void ImportProject_Click(object sender, RoutedEventArgs e)
        {
            ShowImportProjectContent();
        }

        private void Settings_Click(object sender, RoutedEventArgs e)
        {
            isSettingsExpanded = !isSettingsExpanded;
            SettingsSubMenu.Visibility = isSettingsExpanded ? Visibility.Visible : Visibility.Collapsed;
            SettingsButton.Content = isSettingsExpanded ? "⚙️ Settings ▼" : "⚙️ Settings ▶";
        }

        private void ApplicationSettings_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                var appSettingsPage = new ApplicationSettingsPage();
                appSettingsPage.Owner = this;
                appSettingsPage.ShowDialog();
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Error opening Application Settings:\n{ex.Message}",
                    "Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void UserSettings_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                var userSettingsPage = new UserSettingsPage();
                userSettingsPage.Owner = this;
                userSettingsPage.ShowDialog();
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Error opening User Settings:\n{ex.Message}",
                    "Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }




        private void AppInfo_Click(object sender, RoutedEventArgs e)
        {
            var aboutMessage = @"Crane Optimization
Version: 1.0.0
© 2024 Crane Optimization Team

This application helps you plan crane positions to maximize efficiency, 
reduce costs, and ensure top-level project safety and success!

For support: CraneOptimization@gmail.com";

            MessageBox.Show(aboutMessage, "About Crane Optimization",
                MessageBoxButton.OK, MessageBoxImage.Information);
        }

        private void WhatsNew_Click(object sender, RoutedEventArgs e)
        {
            var whatsNew = @"What's New in Version 1.0:

✅ 3D IFC Building Viewer
✅ Automatic Element Weight Calculation
✅ Heavy Element Color Coding (Red for >5000kg)
✅ Building Analysis Tools
✅ Export Analysis Reports
✅ Dark Theme UI
✅ Project Management System
✅ Save to Recent Projects
✅ Navigate between Welcome and Project
✅ Import Project Feature with Drag & Drop

Coming Soon:
🔜 Crane Optimization Algorithms
🔜 Multi-Language Support
🔜 Cloud Project Storage";

            MessageBox.Show(whatsNew, "What's New",
                MessageBoxButton.OK, MessageBoxImage.Information);
        }

        #endregion
    }
}
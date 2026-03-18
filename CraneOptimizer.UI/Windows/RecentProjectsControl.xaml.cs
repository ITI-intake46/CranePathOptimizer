using System;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;

namespace WPF_IFC_test1
{
    public partial class RecentProjectsControl : UserControl
    {
        private ObservableCollection<ProjectInfo> projects;
        private const string PROJECTS_FILE = "recent_projects.json";

        public RecentProjectsControl()
        {
            InitializeComponent();
            LoadProjects();
        }

        #region Data Models

        public class ProjectInfo
        {
            public string Name { get; set; }
            public string LastModified { get; set; }
            public string Location { get; set; }
            public string IfcFile { get; set; }
            public string Status { get; set; }

            // For JSON serialization - we'll save color as string and convert it
            public string StatusColorString { get; set; }

            // This property is not serialized - we recreate it from StatusColorString
            [System.Text.Json.Serialization.JsonIgnore]
            public Brush StatusColor
            {
                get
                {
                    if (!string.IsNullOrEmpty(StatusColorString))
                    {
                        try
                        {
                            return (Brush)new BrushConverter().ConvertFromString(StatusColorString);
                        }
                        catch
                        {
                            return new SolidColorBrush(Color.FromRgb(76, 175, 80)); // Default green
                        }
                    }
                    return new SolidColorBrush(Color.FromRgb(76, 175, 80)); // Default green
                }
                set
                {
                    if (value is SolidColorBrush solidBrush)
                    {
                        StatusColorString = $"#{solidBrush.Color.R:X2}{solidBrush.Color.G:X2}{solidBrush.Color.B:X2}";
                    }
                }
            }
        }

        #endregion

        #region Load/Save Projects

        private void LoadProjects()
        {
            projects = new ObservableCollection<ProjectInfo>();

            // Try to load from file
            if (File.Exists(PROJECTS_FILE))
            {
                try
                {
                    string json = File.ReadAllText(PROJECTS_FILE);
                    var loadedProjects = JsonSerializer.Deserialize<ObservableCollection<ProjectInfo>>(json);

                    if (loadedProjects != null && loadedProjects.Count > 0)
                    {
                        projects = loadedProjects;
                    }
                }
                catch (Exception ex)
                {
                    MessageBox.Show($"Error loading recent projects:\n{ex.Message}",
                        "Warning", MessageBoxButton.OK, MessageBoxImage.Warning);
                }
            }

            ProjectsDataGrid.ItemsSource = projects;
            UpdateProjectCount();

            // Show/hide "no projects" message
            NoProjectsPanel.Visibility = projects.Count == 0 ? Visibility.Visible : Visibility.Collapsed;
            ProjectsDataGrid.Visibility = projects.Count > 0 ? Visibility.Visible : Visibility.Collapsed;
        }

        private void SaveProjects()
        {
            try
            {
                var options = new JsonSerializerOptions
                {
                    WriteIndented = true
                };

                string json = JsonSerializer.Serialize(projects, options);
                File.WriteAllText(PROJECTS_FILE, json);
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Error saving recent projects:\n{ex.Message}",
                    "Warning", MessageBoxButton.OK, MessageBoxImage.Warning);
            }
        }

        private void UpdateProjectCount()
        {
            ProjectCountText.Text = $"{projects.Count} Project{(projects.Count != 1 ? "s" : "")}";
        }

        #endregion

        #region Button Handlers

        private void OpenProject_Click(object sender, RoutedEventArgs e)
        {
            var button = sender as Button;
            var project = button?.DataContext as ProjectInfo;

            if (project != null)
            {
                try
                {
                    string fullPath = Path.Combine(project.Location, project.IfcFile);

                    System.Diagnostics.Debug.WriteLine($"Attempting to open: {fullPath}");

                    if (!File.Exists(fullPath))
                    {
                        MessageBox.Show($"IFC file not found:\n{fullPath}\n\nThe file may have been moved or deleted.",
                            "File Not Found", MessageBoxButton.OK, MessageBoxImage.Warning);
                        return;
                    }

                    // Update last modified time
                    project.LastModified = DateTime.Now.ToString("MM/dd/yyyy HH:mm");
                    SaveProjects();

                    // Create MainWindow
                    var mainWindow = new MainWindow();

                    // Show it first
                    mainWindow.Show();

                    // Then load the file using the PUBLIC method
                    mainWindow.LoadIFCFile(fullPath);

                    // Close the parent window
                    Window parentWindow = Window.GetWindow(this);
                    if (parentWindow != null)
                    {
                        parentWindow.Close();
                    }

                    System.Diagnostics.Debug.WriteLine("✓ Project opened successfully");
                }
                catch (Exception ex)
                {
                    System.Diagnostics.Debug.WriteLine($"Error: {ex.Message}\n{ex.StackTrace}");
                    MessageBox.Show($"Error opening project:\n{ex.Message}",
                        "Error", MessageBoxButton.OK, MessageBoxImage.Error);
                }
            }
        }
        private void DeleteProject_Click(object sender, RoutedEventArgs e)
        {
            var button = sender as Button;
            var project = button?.DataContext as ProjectInfo;

            if (project != null)
            {
                var result = MessageBox.Show(
                    $"Are you sure you want to remove '{project.Name}' from recent projects?\n\nNote: This will not delete the actual project files.",
                    "Confirm Deletion",
                    MessageBoxButton.YesNo,
                    MessageBoxImage.Warning);

                if (result == MessageBoxResult.Yes)
                {
                    projects.Remove(project);
                    SaveProjects();
                    UpdateProjectCount();

                    // Show/hide "no projects" message
                    NoProjectsPanel.Visibility = projects.Count == 0 ? Visibility.Visible : Visibility.Collapsed;
                    ProjectsDataGrid.Visibility = projects.Count > 0 ? Visibility.Visible : Visibility.Collapsed;

                    MessageBox.Show($"'{project.Name}' removed from recent projects.",
                        "Success", MessageBoxButton.OK, MessageBoxImage.Information);
                }
            }
        }

        private void ProjectsDataGrid_MouseDoubleClick(object sender, MouseButtonEventArgs e)
        {
            var grid = sender as DataGrid;
            if (grid?.SelectedItem is ProjectInfo project)
            {
                // Double-click to open project
                OpenProject_Click(sender, null);
            }
        }

        private void Refresh_Click(object sender, RoutedEventArgs e)
        {
            // Reload projects from file
            LoadProjects();
            MessageBox.Show("Recent projects list refreshed!",
                "Refresh", MessageBoxButton.OK, MessageBoxImage.Information);
        }

        private void ClearAll_Click(object sender, RoutedEventArgs e)
        {
            if (projects.Count == 0)
            {
                MessageBox.Show("No projects to clear.",
                    "Clear All", MessageBoxButton.OK, MessageBoxImage.Information);
                return;
            }

            var result = MessageBox.Show(
                $"Are you sure you want to clear all {projects.Count} recent projects?\n\nNote: This will not delete the actual project files.",
                "Confirm Clear All",
                MessageBoxButton.YesNo,
                MessageBoxImage.Warning);

            if (result == MessageBoxResult.Yes)
            {
                projects.Clear();
                SaveProjects();
                UpdateProjectCount();

                NoProjectsPanel.Visibility = Visibility.Visible;
                ProjectsDataGrid.Visibility = Visibility.Collapsed;

                MessageBox.Show("All recent projects cleared.",
                    "Success", MessageBoxButton.OK, MessageBoxImage.Information);
            }
        }

        private void NewProject_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                var mainWindow = new MainWindow();
                mainWindow.Show();

                Window parentWindow = Window.GetWindow(this);
                if (parentWindow != null)
                {
                    parentWindow.Close();
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Error creating new project:\n{ex.Message}",
                    "Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        #endregion

        #region Public Methods (للاستخدام من خارج الـ Control)

        /// <summary>
        /// Add a project to recent projects list
        /// </summary>
        public void AddProject(string name, string location, string ifcFile, string status = "Active")
        {
            var newProject = new ProjectInfo
            {
                Name = name,
                LastModified = DateTime.Now.ToString("MM/dd/yyyy HH:mm"),
                Location = location,
                IfcFile = ifcFile,
                Status = status
            };

            // Set color based on status
            if (status == "Active")
            {
                newProject.StatusColor = new SolidColorBrush(Color.FromRgb(76, 175, 80)); // Green
            }
            else if (status == "Completed")
            {
                newProject.StatusColor = new SolidColorBrush(Color.FromRgb(33, 150, 243)); // Blue
            }
            else
            {
                newProject.StatusColor = new SolidColorBrush(Color.FromRgb(158, 158, 158)); // Gray
            }

            // Remove if already exists (to update it)
            var existing = projects.FirstOrDefault(p =>
                p.Name == name && p.Location == location);
            if (existing != null)
            {
                projects.Remove(existing);
            }

            // Add to beginning
            projects.Insert(0, newProject);

            // Keep only last 20 projects
            while (projects.Count > 20)
            {
                projects.RemoveAt(projects.Count - 1);
            }

            SaveProjects();
            UpdateProjectCount();

            NoProjectsPanel.Visibility = Visibility.Collapsed;
            ProjectsDataGrid.Visibility = Visibility.Visible;
        }

        /// <summary>
        /// Get all recent projects
        /// </summary>
        public ObservableCollection<ProjectInfo> GetProjects()
        {
            return projects;
        }

        #endregion
    }
}
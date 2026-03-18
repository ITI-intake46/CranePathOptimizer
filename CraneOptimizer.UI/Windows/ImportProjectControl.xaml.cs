using Microsoft.Win32;
using System;
using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;

namespace WPF_IFC_test1
{
    public partial class ImportProjectControl : UserControl
    {
        private string _selectedFilePath = null;
        private SolidColorBrush _originalDropZoneBrush;

        public ImportProjectControl()
        {
            InitializeComponent();
            _originalDropZoneBrush = (SolidColorBrush)DropZone.Background;
            LoadRecentImports();
        }

        #region File Selection

        private void BrowseFile_Click(object sender, RoutedEventArgs e)
        {
            var dialog = new OpenFileDialog
            {
                Filter = "IFC files (*.ifc)|*.ifc|All files (*.*)|*.*",
                Title = "Select IFC File to Import"
            };

            if (dialog.ShowDialog() == true)
            {
                SelectFile(dialog.FileName);
            }
        }

        private void DropZone_DragEnter(object sender, DragEventArgs e)
        {
            if (e.Data.GetDataPresent(DataFormats.FileDrop))
            {
                e.Effects = DragDropEffects.Copy;
                DropZone.Background = new SolidColorBrush(Color.FromRgb(66, 66, 66));
                DropZone.BorderBrush = new SolidColorBrush(Color.FromRgb(212, 175, 55));
            }
            else
            {
                e.Effects = DragDropEffects.None;
            }
        }

        private void DropZone_DragLeave(object sender, DragEventArgs e)
        {
            DropZone.Background = _originalDropZoneBrush;
            DropZone.BorderBrush = (SolidColorBrush)FindResource("BorderBrush");
        }

        private void DropZone_Drop(object sender, DragEventArgs e)
        {
            DropZone.Background = _originalDropZoneBrush;
            DropZone.BorderBrush = (SolidColorBrush)FindResource("BorderBrush");

            if (e.Data.GetDataPresent(DataFormats.FileDrop))
            {
                string[] files = (string[])e.Data.GetData(DataFormats.FileDrop);

                if (files.Length > 0)
                {
                    string filePath = files[0];
                    string extension = Path.GetExtension(filePath).ToLower();

                    if (extension == ".ifc")
                    {
                        SelectFile(filePath);
                    }
                    else
                    {
                        MessageBox.Show("Please drop a valid IFC file (.ifc extension)",
                            "Invalid File Type", MessageBoxButton.OK, MessageBoxImage.Warning);
                    }
                }
            }
        }

        private void SelectFile(string filePath)
        {
            try
            {
                if (!File.Exists(filePath))
                {
                    MessageBox.Show("File not found.", "Error",
                        MessageBoxButton.OK, MessageBoxImage.Error);
                    return;
                }

                _selectedFilePath = filePath;

                // Update UI
                FileInfoCard.Visibility = Visibility.Visible;
                ActionButtons.Visibility = Visibility.Visible;

                // Display file info
                FileInfo fileInfo = new FileInfo(filePath);
                txtFileName.Text = fileInfo.Name;
                txtFileLocation.Text = fileInfo.DirectoryName;
                txtFileSize.Text = FormatFileSize(fileInfo.Length);
                txtProjectName.Text = Path.GetFileNameWithoutExtension(filePath);

                System.Diagnostics.Debug.WriteLine($"✓ File selected: {filePath}");
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Error selecting file:\n{ex.Message}",
                    "Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private string FormatFileSize(long bytes)
        {
            string[] sizes = { "B", "KB", "MB", "GB" };
            double len = bytes;
            int order = 0;

            while (len >= 1024 && order < sizes.Length - 1)
            {
                order++;
                len = len / 1024;
            }

            return $"{len:0.##} {sizes[order]}";
        }

        #endregion

        #region Import Actions

        private void ImportProject_Click(object sender, RoutedEventArgs e)
        {
            if (string.IsNullOrEmpty(_selectedFilePath))
            {
                MessageBox.Show("Please select an IFC file first.",
                    "No File Selected", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            if (string.IsNullOrWhiteSpace(txtProjectName.Text))
            {
                MessageBox.Show("Please enter a project name.",
                    "Project Name Required", MessageBoxButton.OK, MessageBoxImage.Warning);
                txtProjectName.Focus();
                return;
            }

            try
            {
                // Add to recent projects
                RecentProjectsHelper.AddProject(
                    _selectedFilePath,
                    txtProjectName.Text.Trim(),
                    "Active"
                );

                // Show success message
                var result = MessageBox.Show(
                    $"Project '{txtProjectName.Text}' imported successfully!\n\n" +
                    $"Would you like to open it now?",
                    "Import Successful",
                    MessageBoxButton.YesNo,
                    MessageBoxImage.Information);

                if (result == MessageBoxResult.Yes)
                {
                    // Open the project
                    OpenProject(_selectedFilePath);
                }
                else
                {
                    // Add to recent imports display
                    AddToRecentImports(txtProjectName.Text, _selectedFilePath);

                    // Clear the form
                    Clear_Click(null, null);
                }

                System.Diagnostics.Debug.WriteLine($"✓ Project imported: {txtProjectName.Text}");
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Error importing project:\n{ex.Message}",
                    "Import Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void Clear_Click(object sender, RoutedEventArgs e)
        {
            _selectedFilePath = null;
            FileInfoCard.Visibility = Visibility.Collapsed;
            ActionButtons.Visibility = Visibility.Collapsed;
            txtFileName.Text = "-";
            txtFileLocation.Text = "-";
            txtFileSize.Text = "-";
            txtProjectName.Text = "";
        }

        private void OpenProject(string filePath)
        {
            try
            {
                var mainWindow = new MainWindow();
                mainWindow.Show();
                mainWindow.LoadIFCFile(filePath);

                // Close the welcome page
                Window parentWindow = Window.GetWindow(this);
                if (parentWindow != null)
                {
                    parentWindow.Close();
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Error opening project:\n{ex.Message}",
                    "Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        #endregion

        #region Recent Imports

        private void LoadRecentImports()
        {
            // This could load from a file or database
            // For now, it's empty
        }

        private void AddToRecentImports(string projectName, string filePath)
        {
            // Clear the "no recent imports" message if it exists
            if (RecentImportsPanel.Children.Count == 1 &&
                RecentImportsPanel.Children[0] is TextBlock tb &&
                tb.Text == "No recent imports")
            {
                RecentImportsPanel.Children.Clear();
            }

            // Create a recent import item
            var border = new Border
            {
                Background = (SolidColorBrush)FindResource("Surface2Brush"),
                BorderBrush = (SolidColorBrush)FindResource("BorderBrush"),
                BorderThickness = new Thickness(1),
                CornerRadius = new CornerRadius(5),
                Padding = new Thickness(15, 10, 15, 10),
                Margin = new Thickness(0, 0, 0, 10)
            };

            var grid = new Grid();
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });

            var stack = new StackPanel();

            var nameText = new TextBlock
            {
                Text = projectName,
                FontWeight = FontWeights.SemiBold,
                FontSize = 13,
                Foreground = (SolidColorBrush)FindResource("TextPrimaryBrush")
            };

            var pathText = new TextBlock
            {
                Text = filePath,
                FontSize = 11,
                Foreground = (SolidColorBrush)FindResource("TextSecondaryBrush"),
                Margin = new Thickness(0, 3, 0, 0)
            };

            var timeText = new TextBlock
            {
                Text = $"Imported: {DateTime.Now:MM/dd/yyyy HH:mm}",
                FontSize = 10,
                Foreground = (SolidColorBrush)FindResource("TextSecondaryBrush"),
                Margin = new Thickness(0, 3, 0, 0)
            };

            stack.Children.Add(nameText);
            stack.Children.Add(pathText);
            stack.Children.Add(timeText);

            var statusBadge = new Border
            {
                Background = (SolidColorBrush)FindResource("SuccessBrush"),
                CornerRadius = new CornerRadius(3),
                Padding = new Thickness(8, 4, 8, 4),
                VerticalAlignment = VerticalAlignment.Center
            };

            var badgeText = new TextBlock
            {
                Text = "✓ Imported",
                Foreground = Brushes.White,
                FontSize = 10,
                FontWeight = FontWeights.SemiBold
            };

            statusBadge.Child = badgeText;

            Grid.SetColumn(stack, 0);
            Grid.SetColumn(statusBadge, 1);

            grid.Children.Add(stack);
            grid.Children.Add(statusBadge);
            border.Child = grid;

            RecentImportsPanel.Children.Insert(0, border);

            // Keep only last 5 imports
            while (RecentImportsPanel.Children.Count > 5)
            {
                RecentImportsPanel.Children.RemoveAt(RecentImportsPanel.Children.Count - 1);
            }
        }

        #endregion
    }
}
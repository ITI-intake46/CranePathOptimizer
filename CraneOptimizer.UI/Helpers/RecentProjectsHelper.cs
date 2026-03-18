using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Windows.Media;

namespace WPF_IFC_test1
{
    public static class RecentProjectsHelper
    {
        private const string PROJECTS_FILE = "recent_projects.json";
        private const int MAX_RECENT_PROJECTS = 20;

        public class ProjectData
        {
            public string Name { get; set; }
            public string LastModified { get; set; }
            public string Location { get; set; }
            public string IfcFile { get; set; }
            public string Status { get; set; }
            public string StatusColorString { get; set; }
        }

        public static void AddProject(string ifcFilePath, string projectName = null, string status = "Active")
        {
            try
            {
                if (string.IsNullOrEmpty(ifcFilePath) || !File.Exists(ifcFilePath))
                {
                    System.Diagnostics.Debug.WriteLine("Invalid IFC file path");
                    return;
                }

                // Load existing projects
                var projects = LoadProjects();

                // Create new project entry
                var newProject = new ProjectData
                {
                    Name = projectName ?? Path.GetFileNameWithoutExtension(ifcFilePath),
                    LastModified = DateTime.Now.ToString("MM/dd/yyyy HH:mm"),
                    Location = Path.GetDirectoryName(ifcFilePath),
                    IfcFile = Path.GetFileName(ifcFilePath),
                    Status = status,
                    StatusColorString = GetStatusColor(status)
                };

                // Remove existing entry with same file path (to update it)
                var existing = projects.FirstOrDefault(p =>
                    p.Location == newProject.Location && p.IfcFile == newProject.IfcFile);

                if (existing != null)
                {
                    projects.Remove(existing);
                }

                // Add to beginning
                projects.Insert(0, newProject);

                // Keep only last MAX_RECENT_PROJECTS
                while (projects.Count > MAX_RECENT_PROJECTS)
                {
                    projects.RemoveAt(projects.Count - 1);
                }

                // Save to file
                SaveProjects(projects);

                System.Diagnostics.Debug.WriteLine($"✓ Project added to recent: {newProject.Name}");
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error adding project to recent: {ex.Message}");
            }
        }

        private static List<ProjectData> LoadProjects()
        {
            if (!File.Exists(PROJECTS_FILE))
            {
                return new List<ProjectData>();
            }

            try
            {
                string json = File.ReadAllText(PROJECTS_FILE);
                var projects = JsonSerializer.Deserialize<List<ProjectData>>(json);
                return projects ?? new List<ProjectData>();
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error loading projects: {ex.Message}");
                return new List<ProjectData>();
            }
        }

        private static void SaveProjects(List<ProjectData> projects)
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
                System.Diagnostics.Debug.WriteLine($"Error saving projects: {ex.Message}");
            }
        }

        private static string GetStatusColor(string status)
        {
            return status switch
            {
                "Active" => "#4CAF50",      // Green
                "Completed" => "#2196F3",    // Blue
                "Archived" => "#9E9E9E",     // Gray
                _ => "#4CAF50"               // Default Green
            };
        }
    }
}
using System.Diagnostics;
using System.IO;

namespace CraneOptimizer.UI.Features.F10_ProjectManagement.Services
{
    public sealed class ProjectShellService : IProjectShellService
    {
        public void ShowFileInFolder(string filePath)
        {
            if (string.IsNullOrWhiteSpace(filePath))
                return;

            var argument = File.Exists(filePath)
                ? $"/select,\"{filePath}\""
                : Directory.Exists(filePath)
                    ? $"\"{filePath}\""
                    : $"\"{Path.GetDirectoryName(filePath) ?? filePath}\"";

            Process.Start(new ProcessStartInfo
            {
                FileName = "explorer.exe",
                Arguments = argument,
                UseShellExecute = true
            });
        }
    }
}

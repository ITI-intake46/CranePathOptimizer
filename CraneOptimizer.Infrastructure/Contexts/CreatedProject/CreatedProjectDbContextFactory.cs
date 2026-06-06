using Microsoft.EntityFrameworkCore;
using System.IO;

namespace CraneOptimizer.Infrastructure.Contexts.CreatedProject
{
    public static class CreatedProjectDbContextFactory
    {
        public const string ProjectDatabaseFileName = "project.db";

        public static CreatedProjectDbContext Create(string projectFolderPath)
        {
            if (string.IsNullOrWhiteSpace(projectFolderPath))
                throw new ArgumentException("Project folder path is required.", nameof(projectFolderPath));

            Directory.CreateDirectory(projectFolderPath);

            var dbPath = Path.Combine(projectFolderPath, ProjectDatabaseFileName);

            var options = new DbContextOptionsBuilder<CreatedProjectDbContext>()
                .UseSqlite($"Data Source={dbPath}")
                .Options;

            var context = new CreatedProjectDbContext(options);

            ApplyPragmas(context);

            return context;
        }

        public static string GetDatabasePath(string projectFolderPath)
        {
            if (string.IsNullOrWhiteSpace(projectFolderPath))
                throw new ArgumentException("Project folder path is required.", nameof(projectFolderPath));

            return Path.Combine(projectFolderPath, ProjectDatabaseFileName);
        }

        private static void ApplyPragmas(CreatedProjectDbContext context)
        {
            context.Database.ExecuteSqlRaw("PRAGMA foreign_keys=ON;");
            context.Database.ExecuteSqlRaw("PRAGMA journal_mode=WAL;");
            context.Database.ExecuteSqlRaw("PRAGMA synchronous=NORMAL;");
            context.Database.ExecuteSqlRaw("PRAGMA temp_store=MEMORY;");
        }
    }
}

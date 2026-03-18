using System;
using System.IO;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using WPF_IFC_test1.Database.Repositories;

namespace WPF_IFC_test1.Database
{
    /// <summary>
    /// Top-level façade.  Create one instance at app startup (singleton).
    /// Exposes all repositories and handles DB creation / migration.
    ///
    /// Usage in App.xaml.cs:
    ///   DatabaseService.Instance.Initialize();
    ///   var projects = await DatabaseService.Instance.Projects.GetAllAsync();
    /// </summary>
    public sealed class DatabaseService
    {
        // ── Singleton ────────────────────────────────────────────────────────
        private static readonly Lazy<DatabaseService> _instance =
            new(() => new DatabaseService());

        public static DatabaseService Instance => _instance.Value;

        // ── DB file path (AppData\Roaming\WPF_IFC_Helix\app.db) ─────────────
        private static string DbPath =>
            Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
                "WPF_IFC_Helix",
                "app.db");

        // ── Context factory ──────────────────────────────────────────────────
        private AppDbContext _context;

        public AppDbContext CreateContext()
        {
            var opts = new DbContextOptionsBuilder<AppDbContext>()
                .UseSqlite($"Data Source={DbPath}")
                .Options;
            return new AppDbContext(opts);
        }

        // ── Repositories ─────────────────────────────────────────────────────
        public IProjectRepository     Projects    { get; private set; }
        public ICraneRepository       Cranes      { get; private set; }
        public IZoneRepository        Zones       { get; private set; }
        public IIfcElementRepository  IfcElements { get; private set; }

        // ── Init ─────────────────────────────────────────────────────────────
        private DatabaseService() { }

        /// <summary>
        /// Call once on app startup (before any UI is shown).
        /// Creates the SQLite file + applies pending EF migrations automatically.
        /// </summary>
        public void Initialize()
        {
            // Ensure directory exists
            Directory.CreateDirectory(Path.GetDirectoryName(DbPath)!);

            _context = CreateContext();

            // Auto-migrate: creates tables on first run, applies new migrations on update
            _context.Database.Migrate();

            // Wire up repositories
            Projects    = new ProjectRepository(_context);
            Cranes      = new CraneRepository(_context);
            Zones       = new ZoneRepository(_context);
            IfcElements = new IfcElementRepository(_context);
        }

        /// <summary>
        /// Call this if you need a fresh context (e.g. after an external DB change).
        /// Disposes the old context and creates a new one.
        /// </summary>
        public async Task ResetContextAsync()
        {
            await _context.DisposeAsync();
            _context = CreateContext();
            Projects    = new ProjectRepository(_context);
            Cranes      = new CraneRepository(_context);
            Zones       = new ZoneRepository(_context);
            IfcElements = new IfcElementRepository(_context);
        }
    }
}

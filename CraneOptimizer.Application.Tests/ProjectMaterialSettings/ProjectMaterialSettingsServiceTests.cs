using CraneOptimizer.Application.Services;
using CraneOptimizer.Domain.Entities;
using CraneOptimizer.Domain.Interfaces;

namespace CraneOptimizer.Application.Tests.ProjectMaterialSettings
{
    public class ProjectMaterialSettingsServiceTests
    {
        [Fact]
        public async Task GetConcreteDensityKgPerM3_ReturnsDefault_WhenProjectNull()
        {
            var repo = new FakeProjectRepository();
            var service = new ProjectMaterialSettingsService(repo);

            var result = await service.GetConcreteDensityKgPerM3Async(Guid.NewGuid());

            Assert.Equal(2500.0, result);
        }

        [Fact]
        public async Task GetConcreteDensityKgPerM3_ReturnsProjectValue_WhenSet()
        {
            var projectId = Guid.NewGuid();
            var repo = new FakeProjectRepository
            {
                Project = new Project
                {
                    Id = projectId,
                    ConcreteDensityKgPerM3 = 2600.0
                }
            };
            var service = new ProjectMaterialSettingsService(repo);

            var result = await service.GetConcreteDensityKgPerM3Async(projectId);

            Assert.Equal(2600.0, result);
        }

        [Fact]
        public async Task SetConcreteDensityKgPerM3_UpdatesRepository_WhenValid()
        {
            var projectId = Guid.NewGuid();
            var repo = new FakeProjectRepository
            {
                Project = new Project { Id = projectId }
            };
            var service = new ProjectMaterialSettingsService(repo);

            await service.SetConcreteDensityKgPerM3Async(projectId, 2450.0);

            Assert.Equal(2450.0, repo.Project.ConcreteDensityKgPerM3);
        }

        private sealed class FakeProjectRepository : IProjectRepository
        {
            public Project? Project { get; set; }

            public Task CreateAsync(Project project, CancellationToken ct)
            {
                Project = project;
                return Task.CompletedTask;
            }

            public Task<List<Project>> GetAllAsync(CancellationToken ct)
            {
                return Task.FromResult(Project == null ? new List<Project>() : new List<Project> { Project });
            }

            public Task<List<Project>> GetRecentAsync(int count, CancellationToken ct)
            {
                return GetAllAsync(ct);
            }

            public Task<Project?> GetByIdAsync(Guid id, CancellationToken ct)
            {
                return Task.FromResult(Project?.Id == id ? Project : null);
            }

            public Task<Project?> GetProjectByIdAsync(Guid id, CancellationToken ct = default)
            {
                return GetByIdAsync(id, ct);
            }

            public Task UpdateConcreteDensityAsync(Guid projectId, double density, CancellationToken ct = default)
            {
                if (Project == null || Project.Id != projectId)
                    throw new KeyNotFoundException($"Project {projectId} not found");

                Project.ConcreteDensityKgPerM3 = density;
                return Task.CompletedTask;
            }

            public Task<bool> SoftDeleteAsync(Guid id, CancellationToken ct)
            {
                return Task.FromResult(false);
            }

            public Task OpenAsync(Guid id, DateTime openedAtUtc, CancellationToken ct)
            {
                return Task.CompletedTask;
            }

            public Task<bool> ToggleFavoriteAsync(Guid id, bool isFavorite, DateTime modifiedAtUtc, CancellationToken ct)
            {
                return Task.FromResult(false);
            }
        }
    }
}

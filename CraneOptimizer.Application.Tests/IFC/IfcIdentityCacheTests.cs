using System.Text.Json;
using CraneOptimizer.Domain.Common;
using CraneOptimizer.Domain.CreatedProject;
using CraneOptimizer.Domain.Diagnostics;
using CraneOptimizer.Domain.Schemas;
using CraneOptimizer.Infrastructure.Contexts.CreatedProject;
using CraneOptimizer.UI.Features.F10_ProjectManagement.Models;
using CraneOptimizer.UI.Features.F10_ProjectManagement.Services;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace CraneOptimizer.Application.Tests.IFC;

public sealed class IfcIdentityCacheTests : IDisposable
{
    private readonly string _projectFolder = Path.Combine(
        Path.GetTempPath(),
        $"CraneOptimizerIdentityCache_{Guid.NewGuid():N}");

    [Fact]
    public async Task LoadProjectStateAsync_OldIdentityVersion_RequestsCacheRefresh()
    {
        var (service, session, state) = await CreateSavedProjectAsync("My BEAM");

        await using (var context = CreatedProjectDbContextFactory.Create(_projectFolder))
        {
            var file = await context.IfcFiles.SingleAsync();
            file.MetadataJson = JsonSerializer.Serialize(new Dictionary<string, object?>
            {
                ["ifcNormalizationCacheVersion"] = "revit-explicit-weight-v2"
            });
            await context.SaveChangesAsync();
        }

        var restored = await service.LoadProjectStateAsync(session);

        Assert.NotNull(restored.IfcFile);
        Assert.Empty(restored.IfcElements);
        Assert.True(restored.RequiresIfcCacheRefresh);
    }


    [Fact]
    public async Task LoadProjectStateAsync_ByteIdenticalIfcWithChangedTimestamp_RestoresCacheByHash()
    {
        var (service, session, state) = await CreateSavedProjectAsync("Portable cache fixture");
        string ifcPath = state.IfcFile!.AbsolutePath;

        // Simulate ZIP/copy extraction on another machine: bytes are unchanged but mtime drifts.
        File.SetLastWriteTimeUtc(ifcPath, DateTime.UtcNow.AddDays(-30));

        var restored = await service.LoadProjectStateAsync(session);

        Assert.False(restored.RequiresIfcCacheRefresh);
        Assert.Single(restored.IfcElements);
        Assert.Equal("B-101", restored.IfcElements[0].ElementMark);
    }

    [Fact]
    public async Task LoadProjectStateAsync_SameSizeAndTimestampButChangedIfcBytes_RejectsCacheByHash()
    {
        var (service, session, state) = await CreateSavedProjectAsync("Hash tamper fixture");
        string ifcPath = state.IfcFile!.AbsolutePath;

        DateTime persistedTimestamp;
        await using (var context = CreatedProjectDbContextFactory.Create(_projectFolder))
        {
            var file = await context.IfcFiles.AsNoTracking().SingleAsync();
            Assert.False(string.IsNullOrWhiteSpace(file.FileHash));
            persistedTimestamp = DateTime.SpecifyKind(file.LastWriteUtc!.Value, DateTimeKind.Utc);
        }

        byte[] bytes = await File.ReadAllBytesAsync(ifcPath);
        bytes[0] = bytes[0] == (byte)'I' ? (byte)'J' : (byte)'I';
        await File.WriteAllBytesAsync(ifcPath, bytes);
        File.SetLastWriteTimeUtc(ifcPath, persistedTimestamp);

        var restored = await service.LoadProjectStateAsync(session);

        Assert.True(restored.RequiresIfcCacheRefresh);
        Assert.Empty(restored.IfcElements);
    }

    [Fact]
    public async Task SaveProjectStateAsync_SameFileWithChangedName_RewritesDisplayName()
    {
        var (service, session, state) = await CreateSavedProjectAsync("Old cached name");
        state.IfcElements[0].ElementName = "My BEAM";

        await service.SaveProjectStateAsync(session, state);

        await using var context = CreatedProjectDbContextFactory.Create(_projectFolder);
        var row = await context.IfcElements.AsNoTracking().SingleAsync();
        Assert.Equal("My BEAM", row.DisplayName);
        Assert.Equal("B-101", row.Mark);
    }

    private async Task<(ProjectStateService Service, ProjectSession Session, ProjectStateSnapshot State)>
        CreateSavedProjectAsync(string elementName)
    {
        Directory.CreateDirectory(_projectFolder);
        await using (var context = CreatedProjectDbContextFactory.Create(_projectFolder))
        {
            await context.Database.MigrateAsync();
            context.ProjectInfo.Add(new ProjectInfoEntity { Name = "Identity cache test" });
            await context.SaveChangesAsync();
        }

        var ifcPath = Path.Combine(_projectFolder, "identity.ifc");
        await File.WriteAllTextAsync(ifcPath, "IFC identity cache fixture");
        var session = new ProjectSession
        {
            ProjectId = Guid.NewGuid(),
            ProjectName = "Identity cache test",
            ProjectFolderPath = _projectFolder,
            ProjectInfoId = 1
        };
        var state = new ProjectStateSnapshot
        {
            IfcFile = new ProjectIfcFileState
            {
                AbsolutePath = ifcPath,
                FileName = "identity.ifc",
                InsertionOffset = new DomainPoint3D()
            },
            IfcElements =
            [
                new PrecastElement
                {
                    ElementID = "1zqWVRVLz5h8EdjS4I_HxE",
                    ElementName = elementName,
                    ElementMark = "B-101",
                    ElementType = "Beam",
                    ElementMaterial = "Concrete",
                    Role = IfcElementRole.LiftableElement,
                    IncludeInGa = true
                }
            ]
        };
        var service = new ProjectStateService();
        await service.SaveProjectStateAsync(session, state);
        return (service, session, state);
    }

    public void Dispose()
    {
        try
        {
            if (Directory.Exists(_projectFolder))
                Directory.Delete(_projectFolder, recursive: true);
        }
        catch
        {
            // Best-effort cleanup for transient SQLite handles on Windows.
        }
    }
}

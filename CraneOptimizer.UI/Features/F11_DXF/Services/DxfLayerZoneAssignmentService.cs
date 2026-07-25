using CraneOptimizer.Domain.Common;
using CraneOptimizer.Domain.Schemas;
using CraneOptimizer.Domain.Schemas.Enums;
using CraneOptimizer.UI.Core.Interfaces;
using CraneOptimizer.UI.Features.F11_DXF.DTOs;
using CraneOptimizer.UI.Features.F11_DXF.Models;
using System.Collections.Concurrent;
using System.IO;

namespace CraneOptimizer.UI.Features.F11_DXF.Services;

public sealed class DxfLayerZoneAssignmentService : IDxfLayerZoneAssignmentService
{
    public const string ImportMethod = "DxfByLayer";

    private readonly ISceneService _scene;
    private readonly ConcurrentDictionary<DxfLayerZoneKey, SemaphoreSlim> _operationLocks = new();

    public DxfLayerZoneAssignmentService(ISceneService scene)
    {
        _scene = scene;
    }

    public Task<IReadOnlyDictionary<string, DxfLayerZoneState>> GetAssignmentStatesAsync(
        DxfLayerZoneSource source,
        IEnumerable<string> layerNames,
        CancellationToken cancellationToken)
    {
        string[] names = layerNames.Distinct(StringComparer.OrdinalIgnoreCase).ToArray();
        return Task.Run<IReadOnlyDictionary<string, DxfLayerZoneState>>(() =>
        {
            cancellationToken.ThrowIfCancellationRequested();
            IReadOnlyList<GreenAreaZone> zones = _scene.GetZoneSnapshot();
            var result = new Dictionary<string, DxfLayerZoneState>(StringComparer.OrdinalIgnoreCase);
            foreach (string layerName in names)
            {
                cancellationToken.ThrowIfCancellationRequested();
                var key = new DxfLayerZoneKey(source.SourceDxfId, layerName);
                GreenAreaZone[] matches = FindGeneratedZones(zones, key, source);
                result[layerName] = CreateState(matches);
            }

            return result;
        }, cancellationToken);
    }

    public Task<DxfLayerZoneTransaction> AssignAsync(
        DxfLayerZoneSource source,
        string layerName,
        ZoneType zoneType,
        Func<CancellationToken, IReadOnlyList<DxfZoneCandidate>> candidateProvider,
        CancellationToken cancellationToken) =>
        PrepareAsync(source, layerName, zoneType, candidateProvider, cancellationToken);

    public Task<DxfLayerZoneTransaction> ReplaceAsync(
        DxfLayerZoneSource source,
        string layerName,
        ZoneType zoneType,
        Func<CancellationToken, IReadOnlyList<DxfZoneCandidate>> candidateProvider,
        CancellationToken cancellationToken) =>
        PrepareAsync(source, layerName, zoneType, candidateProvider, cancellationToken);

    public Task<DxfLayerZoneTransaction> ClearAsync(
        DxfLayerZoneSource source,
        string layerName,
        CancellationToken cancellationToken) =>
        PrepareAsync(source, layerName, null, null, cancellationToken);

    public Task<IReadOnlyList<DxfLayerZoneTransaction>> ClearAllAsync(
        DxfLayerZoneSource source,
        IEnumerable<string> layerNames,
        CancellationToken cancellationToken)
    {
        string[] names = layerNames
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToArray();
        return Task.Run<IReadOnlyList<DxfLayerZoneTransaction>>(() =>
        {
            cancellationToken.ThrowIfCancellationRequested();
            IReadOnlyList<GreenAreaZone> sceneZones = _scene.GetZoneSnapshot();
            return names.Select(layerName =>
            {
                cancellationToken.ThrowIfCancellationRequested();
                var key = new DxfLayerZoneKey(source.SourceDxfId, layerName);
                GreenAreaZone[] matches = FindGeneratedZones(sceneZones, key, source);
                DxfLayerZoneState previousState = CreateState(matches);
                GreenAreaZone[] previousSnapshots = matches.Select(CloneZone).ToArray();
                return new DxfLayerZoneTransaction(
                    key,
                    previousState.Assignment,
                    DxfLayerZoneAssignment.None,
                    previousSnapshots,
                    Array.Empty<GreenAreaZone>(),
                    $"remove {previousSnapshots.Length} DXF layer zones");
            }).ToArray();
        }, cancellationToken);
    }

    private async Task<DxfLayerZoneTransaction> PrepareAsync(
        DxfLayerZoneSource source,
        string layerName,
        ZoneType? zoneType,
        Func<CancellationToken, IReadOnlyList<DxfZoneCandidate>>? candidateProvider,
        CancellationToken cancellationToken)
    {
        var key = new DxfLayerZoneKey(source.SourceDxfId, layerName);
        SemaphoreSlim operationLock = _operationLocks.GetOrAdd(key, _ => new SemaphoreSlim(1, 1));
        await operationLock.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            return await Task.Run(() =>
            {
                cancellationToken.ThrowIfCancellationRequested();
                IReadOnlyList<GreenAreaZone> sceneZones = _scene.GetZoneSnapshot();
                GreenAreaZone[] matches = FindGeneratedZones(sceneZones, key, source);
                DxfLayerZoneState previousState = CreateState(matches);
                GreenAreaZone[] previousSnapshots = matches.Select(CloneZone).ToArray();

                if (zoneType == null)
                {
                    return new DxfLayerZoneTransaction(
                        key,
                        previousState.Assignment,
                        DxfLayerZoneAssignment.None,
                        previousSnapshots,
                        Array.Empty<GreenAreaZone>(),
                        $"remove {previousSnapshots.Length} DXF layer zones");
                }

                cancellationToken.ThrowIfCancellationRequested();
                IReadOnlyList<DxfZoneCandidate> candidates =
                    candidateProvider?.Invoke(cancellationToken) ?? Array.Empty<DxfZoneCandidate>();
                DxfZoneCandidate[] validCandidates = candidates
                    .Where(candidate => candidate.IsValid)
                    .ToArray();
                cancellationToken.ThrowIfCancellationRequested();

                int baseZoneCount = sceneZones.Count - matches.Length;
                GreenAreaZone[] nextZones = validCandidates
                    .Select((candidate, index) => CreateZone(
                        source,
                        layerName,
                        zoneType.Value,
                        candidate,
                        baseZoneCount + index + 1))
                    .ToArray();
                DxfLayerZoneAssignment nextAssignment = zoneType == ZoneType.Allowed
                    ? DxfLayerZoneAssignment.Allowed
                    : DxfLayerZoneAssignment.Forbidden;
                string operation = previousSnapshots.Length == 0 ? "import" : "replace";

                return new DxfLayerZoneTransaction(
                    key,
                    previousState.Assignment,
                    nextAssignment,
                    previousSnapshots,
                    nextZones,
                    $"{operation} {nextZones.Length} {nextAssignment.ToString().ToLowerInvariant()} DXF layer zones");
            }, cancellationToken).ConfigureAwait(false);
        }
        finally
        {
            operationLock.Release();
        }
    }

    internal static bool IsGeneratedForLayer(GreenAreaZone zone, DxfLayerZoneKey key)
    {
        if (!string.Equals(zone.SourceLayerName, key.LayerName, StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        return zone.SourceDxfId == key.SourceDxfId && IsDxfByLayerZone(zone);
    }

    internal static DxfLayerZoneState CreateState(IReadOnlyList<GreenAreaZone> zones)
    {
        DxfLayerZoneAssignment assignment;
        if (zones.Count == 0)
        {
            assignment = DxfLayerZoneAssignment.None;
        }
        else
        {
            ZoneType[] types = zones.Select(zone => zone.Type).Distinct().ToArray();
            assignment = types.Length != 1
                ? DxfLayerZoneAssignment.Inconsistent
                : types[0] == ZoneType.Allowed
                    ? DxfLayerZoneAssignment.Allowed
                    : DxfLayerZoneAssignment.Forbidden;
        }

        return new DxfLayerZoneState(
            assignment,
            zones.Count,
            zones.Select(zone => zone.ZoneId).ToArray());
    }

    private static GreenAreaZone[] FindGeneratedZones(
        IReadOnlyList<GreenAreaZone> zones,
        DxfLayerZoneKey key,
        DxfLayerZoneSource source) =>
        zones.Where(zone =>
            IsGeneratedForLayer(zone, key) ||
            IsLegacyGeneratedForLayer(zone, key, source)).ToArray();

    private static bool IsLegacyGeneratedForLayer(
        GreenAreaZone zone,
        DxfLayerZoneKey key,
        DxfLayerZoneSource source)
    {
        if (!IsDxfByLayerZone(zone) ||
            !string.Equals(zone.SourceLayerName, key.LayerName, StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        if (!string.IsNullOrWhiteSpace(zone.SourceDxfFilePath) &&
            !string.IsNullOrWhiteSpace(source.FilePath) &&
            PathsMatch(zone.SourceDxfFilePath, source.FilePath))
        {
            return true;
        }

        return source.AllowLegacyFileNameMatch &&
               string.Equals(
                   zone.SourceDxfFileName,
                   source.DisplayName,
                   StringComparison.OrdinalIgnoreCase);
    }

    private static bool PathsMatch(string left, string right)
    {
        try
        {
            return string.Equals(
                Path.GetFullPath(left),
                Path.GetFullPath(right),
                StringComparison.OrdinalIgnoreCase);
        }
        catch (Exception ex) when (ex is ArgumentException or NotSupportedException)
        {
            return false;
        }
    }

    private static bool IsDxfByLayerZone(GreenAreaZone zone) =>
        string.Equals(zone.ImportMethod, ImportMethod, StringComparison.Ordinal) ||
        string.IsNullOrWhiteSpace(zone.ImportMethod) &&
        !string.IsNullOrWhiteSpace(zone.SourceCandidateId);

    private static GreenAreaZone CreateZone(
        DxfLayerZoneSource source,
        string layerName,
        ZoneType zoneType,
        DxfZoneCandidate candidate,
        int sequence)
    {
        List<DomainPoint3D> vertices = candidate.Vertices
            .Select(point => new DomainPoint3D(point.X, point.Y, point.Z))
            .ToList();
        double? minimumHeight = vertices.Count == 0 ? null : vertices.Min(point => point.Z);
        double? maximumHeight = vertices.Count == 0 ? null : vertices.Max(point => point.Z);
        return new GreenAreaZone
        {
            ZoneId = Guid.NewGuid(),
            Vertices = vertices,
            Area = candidate.Area,
            Type = zoneType,
            Name = $"{zoneType} DXF Zone {sequence}",
            MinimumHeight = minimumHeight,
            MaximumHeight = maximumHeight,
            SourceDxfId = source.SourceDxfId,
            SourceDxfFileName = source.DisplayName,
            SourceDxfFilePath = source.FilePath,
            SourceLayerName = layerName,
            SourceCandidateId = candidate.Id.ToString(),
            ImportMethod = ImportMethod,
            OriginalAssignmentType = zoneType
        };
    }

    private static GreenAreaZone CloneZone(GreenAreaZone zone) => new()
    {
        ZoneId = zone.ZoneId,
        Vertices = zone.Vertices
            .Select(point => new DomainPoint3D(point.X, point.Y, point.Z))
            .ToList(),
        Area = zone.Area,
        Name = zone.Name,
        Type = zone.Type,
        MinimumHeight = zone.MinimumHeight,
        MaximumHeight = zone.MaximumHeight,
        SourceDxfId = zone.SourceDxfId,
        SourceDxfFileName = zone.SourceDxfFileName,
        SourceDxfFilePath = zone.SourceDxfFilePath,
        SourceLayerName = zone.SourceLayerName,
        SourceCandidateId = zone.SourceCandidateId,
        ImportMethod = zone.ImportMethod,
        OriginalAssignmentType = zone.OriginalAssignmentType
    };
}

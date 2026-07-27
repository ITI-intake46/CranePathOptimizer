using System.Text.Json;
using System.Text.Json.Serialization;
using CraneOptimizer.Application.GAAnalysis;

namespace CraneOptimizer.Infrastructure.GAAnalysis;

public sealed class SystemTextJsonGaLiftAnalysisSerializer : IGaLiftAnalysisSerializer
{
    private static readonly JsonSerializerOptions Options = new()
    {
        WriteIndented = true,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        PropertyNameCaseInsensitive = true,
        AllowTrailingCommas = true,
        Converters = { new JsonStringEnumConverter(JsonNamingPolicy.SnakeCaseUpper) }
    };

    public byte[] Serialize(GaLiftAnalysisResult analysis) =>
        JsonSerializer.SerializeToUtf8Bytes(analysis, Options);

    public byte[] Serialize(GaLiftAnalysisManifest manifest) =>
        JsonSerializer.SerializeToUtf8Bytes(manifest, Options);

    public GaLiftAnalysisResult? DeserializeAnalysis(ReadOnlySpan<byte> json) =>
        JsonSerializer.Deserialize<GaLiftAnalysisResult>(json, Options);

    public GaLiftAnalysisManifest? DeserializeManifest(ReadOnlySpan<byte> json) =>
        JsonSerializer.Deserialize<GaLiftAnalysisManifest>(json, Options);
}

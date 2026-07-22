using System.Security.Cryptography;
using System.Text;
using CraneOptimizer.AI.Agents.Contracts;

namespace CraneOptimizer.AI.Agents.Runtime
{
    public sealed class FileSystemAiAgentDraftArtifactStore : IAiAgentDraftArtifactStore
    {
        private readonly string _rootDirectory;

        public FileSystemAiAgentDraftArtifactStore()
            : this(Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                "CraneOptimizer",
                "AI",
                "Drafts"))
        {
        }

        public FileSystemAiAgentDraftArtifactStore(string rootDirectory)
        {
            if (string.IsNullOrWhiteSpace(rootDirectory))
                throw new ArgumentException("A draft artifact directory is required.", nameof(rootDirectory));

            _rootDirectory = Path.GetFullPath(rootDirectory.Trim());
        }

        public async Task<AiAgentDraftArtifactResult> WriteAsync(
            AiAgentDraftArtifactRequest request,
            CancellationToken cancellationToken = default)
        {
            ArgumentNullException.ThrowIfNull(request);
            cancellationToken.ThrowIfCancellationRequested();

            if (string.IsNullOrWhiteSpace(request.Content))
            {
                return new AiAgentDraftArtifactResult
                {
                    Succeeded = false,
                    Error = "The draft content is empty."
                };
            }

            Directory.CreateDirectory(_rootDirectory);

            var safeProject = SanitizeFileName(request.ProjectName, "project");
            var safeStem = SanitizeFileName(request.FileStem, "readiness-remediation");
            var runId = request.RunId ?? string.Empty;
            var runSuffix = SanitizeFileName(
                runId.Length > 8 ? runId[..8] : runId,
                "run");
            var timestamp = DateTimeOffset.UtcNow.ToString("yyyyMMdd-HHmmss-fff");
            var uniqueSuffix = Guid.NewGuid().ToString("N")[..6];
            var fileName = $"{safeProject}-{safeStem}-{timestamp}-{runSuffix}-{uniqueSuffix}.md";
            var artifactPath = Path.Combine(_rootDirectory, fileName);
            var temporaryPath = artifactPath + ".tmp-" + Guid.NewGuid().ToString("N");
            var encoding = new UTF8Encoding(encoderShouldEmitUTF8Identifier: false);

            try
            {
                await File.WriteAllTextAsync(
                    temporaryPath,
                    request.Content,
                    encoding,
                    cancellationToken).ConfigureAwait(false);

                cancellationToken.ThrowIfCancellationRequested();
                File.Move(temporaryPath, artifactPath);

                var bytes = await File.ReadAllBytesAsync(artifactPath, cancellationToken).ConfigureAwait(false);
                return new AiAgentDraftArtifactResult
                {
                    Succeeded = true,
                    ArtifactPath = artifactPath,
                    ArtifactKind = "Markdown readiness-remediation draft",
                    SizeBytes = bytes.LongLength,
                    Sha256 = Convert.ToHexString(SHA256.HashData(bytes)).ToLowerInvariant()
                };
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                TryDelete(temporaryPath);
                return new AiAgentDraftArtifactResult
                {
                    Succeeded = false,
                    Error = ex.Message
                };
            }
            finally
            {
                TryDelete(temporaryPath);
            }
        }

        private static string SanitizeFileName(string? value, string fallback)
        {
            var normalized = string.IsNullOrWhiteSpace(value) ? fallback : value.Trim();
            var invalid = Path.GetInvalidFileNameChars();
            var builder = new StringBuilder(normalized.Length);

            foreach (var character in normalized)
            {
                if (invalid.Contains(character))
                {
                    builder.Append('-');
                    continue;
                }

                builder.Append(char.IsWhiteSpace(character) ? '-' : char.ToLowerInvariant(character));
            }

            var result = builder.ToString().Trim('-', '.');
            while (result.Contains("--", StringComparison.Ordinal))
                result = result.Replace("--", "-", StringComparison.Ordinal);

            if (string.IsNullOrWhiteSpace(result))
                return fallback;

            return result.Length <= 60 ? result : result[..60];
        }

        private static void TryDelete(string path)
        {
            try
            {
                if (File.Exists(path))
                    File.Delete(path);
            }
            catch
            {
                // Best-effort cleanup only. The authoritative result is the final artifact write.
            }
        }
    }
}

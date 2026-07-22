using System.Text;
using CraneOptimizer.AI.Agents.Contracts;

namespace CraneOptimizer.AI.Agents.Actions
{
    public sealed class PrepareReadinessRemediationDraftActionTool : IAiAgentActionTool
    {
        private readonly IAiAgentDraftArtifactStore _artifactStore;

        public PrepareReadinessRemediationDraftActionTool(IAiAgentDraftArtifactStore artifactStore)
        {
            _artifactStore = artifactStore ?? throw new ArgumentNullException(nameof(artifactStore));
        }

        public AiAgentActionToolDefinition Definition { get; } = new()
        {
            Name = "prepare_readiness_remediation_draft",
            Title = "Prepare readiness remediation draft",
            Description = "Create a local Markdown draft containing the current blockers, evidence, and ordered remediation plan for engineering review.",
            ActionType = "CreateDraftArtifact",
            Target = "Local application data / CraneOptimizer / AI / Drafts",
            ButtonLabel = "Prepare draft",
            InputSchema = "{\"type\":\"object\",\"properties\":{\"format\":{\"const\":\"markdown\"},\"scope\":{\"const\":\"readiness-remediation\"}}}",
            AccessMode = AiToolAccessMode.Mutating,
            PermissionLevel = "LocalArtifactWrite",
            Risk = "Medium — writes one reversible local draft file; it does not change project, IFC, crane, zone, GA, report, or optimizer state.",
            Impact = "Creates one new Markdown file in the user's local CraneOptimizer AI Drafts folder. No engineering input or optimizer setting is changed.",
            Reversibility = "Fully reversible — delete the generated draft file. The approval applies to this proposal once and cannot be replayed.",
            Timeout = TimeSpan.FromSeconds(10)
        };

        public async Task<AiAgentActionToolResult> ExecuteAsync(
            AiAgentActionContext context,
            CancellationToken cancellationToken = default)
        {
            ArgumentNullException.ThrowIfNull(context);
            cancellationToken.ThrowIfCancellationRequested();

            if (!context.UserApproved)
            {
                return new AiAgentActionToolResult
                {
                    Succeeded = false,
                    Error = "Explicit one-time approval is required before a draft artifact can be created."
                };
            }

            var run = context.AgentRun;
            var projectName = ReadFact(run, "get_project_overview", "Project", "Project");
            var content = BuildDraft(run, context.Proposal, context.InitiatedBy, projectName);
            var result = await _artifactStore.WriteAsync(new AiAgentDraftArtifactRequest
            {
                RunId = run.RunId,
                ProjectName = projectName,
                FileStem = "readiness-remediation",
                Title = "Project readiness remediation draft",
                Content = content
            }, cancellationToken).ConfigureAwait(false);

            if (!result.Succeeded)
            {
                return new AiAgentActionToolResult
                {
                    Succeeded = false,
                    Error = string.IsNullOrWhiteSpace(result.Error)
                        ? "The readiness-remediation draft could not be created."
                        : result.Error
                };
            }

            return new AiAgentActionToolResult
            {
                Succeeded = true,
                Summary = $"Created a reversible readiness-remediation draft: {result.ArtifactPath}",
                ArtifactPath = result.ArtifactPath,
                ArtifactKind = result.ArtifactKind,
                ArtifactSha256 = result.Sha256,
                ArtifactSizeBytes = result.SizeBytes,
                Governance = "Only a local draft artifact was created. No project, IFC, crane, zone, GA, report, optimizer, or engineering input state was changed."
            };
        }

        private static string BuildDraft(
            AiAgentRunSnapshot run,
            AiAgentActionProposal proposal,
            string initiatedBy,
            string projectName)
        {
            var builder = new StringBuilder();
            builder.AppendLine("# Project Readiness Remediation Draft");
            builder.AppendLine();
            builder.AppendLine("> Draft artifact for engineering review. This document does not certify lift safety, crane feasibility, or optimizer eligibility.");
            builder.AppendLine();
            builder.AppendLine("## Provenance");
            builder.AppendLine();
            builder.AppendLine($"- Project: {projectName}");
            builder.AppendLine($"- Agent: {run.AgentName}");
            builder.AppendLine($"- Agent run: {run.RunId}");
            builder.AppendLine($"- Initiated by: {Normalize(initiatedBy, "User")}");
            builder.AppendLine($"- Model revision: {run.ModelRevision}");
            builder.AppendLine($"- Model fingerprint: {run.ModelFingerprint}");
            builder.AppendLine($"- Captured at: {run.CompletedAt:O}");
            builder.AppendLine($"- Approval evidence: {proposal.EvidenceLabel}");
            builder.AppendLine();
            builder.AppendLine("## Goal");
            builder.AppendLine();
            builder.AppendLine(Normalize(run.Goal, "Assess project readiness and identify the next safe action."));
            builder.AppendLine();
            builder.AppendLine("## Deterministic conclusion");
            builder.AppendLine();
            builder.AppendLine(Normalize(run.Conclusion, "No deterministic conclusion was captured."));
            builder.AppendLine();
            builder.AppendLine("## Tool findings");
            builder.AppendLine();

            foreach (var result in run.ToolResults)
            {
                builder.AppendLine($"### `{result.ToolName}`");
                builder.AppendLine();
                builder.AppendLine($"- Status: {(result.Succeeded ? "Succeeded" : "Failed")}");
                builder.AppendLine($"- Summary: {Normalize(result.Summary, result.Error)}");
                builder.AppendLine($"- Evidence: {Normalize(result.Evidence, "No evidence text was captured.")}");
                if (result.Facts.Count > 0)
                {
                    builder.AppendLine("- Facts:");
                    foreach (var fact in result.Facts.OrderBy(item => item.Key, StringComparer.OrdinalIgnoreCase))
                        builder.AppendLine($"  - {fact.Key}: {fact.Value}");
                }
                builder.AppendLine();
            }

            builder.AppendLine("## Recommended remediation actions");
            builder.AppendLine();
            if (run.RecommendedActions.Count == 0)
            {
                builder.AppendLine("1. No deterministic remediation action was captured. Request engineering review.");
            }
            else
            {
                for (var index = 0; index < run.RecommendedActions.Count; index++)
                    builder.AppendLine($"{index + 1}. {run.RecommendedActions[index]}");
            }

            builder.AppendLine();
            builder.AppendLine("## Grounding evidence");
            builder.AppendLine();
            if (run.Citations.Count == 0)
            {
                builder.AppendLine("No citation-addressable evidence was attached to this run.");
            }
            else
            {
                foreach (var citation in run.Citations)
                {
                    builder.AppendLine($"### [{citation.Id}] {citation.SourceLabel}");
                    builder.AppendLine();
                    builder.AppendLine(citation.Snippet);
                    builder.AppendLine();
                }
            }

            builder.AppendLine("## Governance and approval");
            builder.AppendLine();
            builder.AppendLine("- This file was created only after explicit one-time user approval.");
            builder.AppendLine("- The approval was scoped to creating this single local draft artifact.");
            builder.AppendLine("- No project, IFC, crane, zone, GA, report, optimizer, or engineering input state was changed.");
            builder.AppendLine("- The file is reversible and may be deleted without affecting the project.");
            builder.AppendLine("- Engineering review remains mandatory before any operational decision.");
            builder.AppendLine();
            builder.AppendLine("## Agent governance statement");
            builder.AppendLine();
            builder.AppendLine(Normalize(run.GovernanceSummary, "No governance summary was captured."));

            return builder.ToString();
        }

        private static string ReadFact(
            AiAgentRunSnapshot run,
            string toolName,
            string key,
            string fallback)
        {
            var result = run.ToolResults.FirstOrDefault(item =>
                item.ToolName.Equals(toolName, StringComparison.OrdinalIgnoreCase));
            return result != null
                   && result.Facts.TryGetValue(key, out var value)
                   && !string.IsNullOrWhiteSpace(value)
                ? value.Trim()
                : fallback;
        }

        private static string Normalize(string? value, string fallback)
            => string.IsNullOrWhiteSpace(value) ? fallback : value.Trim();
    }
}

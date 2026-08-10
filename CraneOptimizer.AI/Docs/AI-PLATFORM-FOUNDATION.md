# CraneOptimizer AI Platform Foundation

The AI platform uses one governed runtime for local and BYOK cloud providers.

## Architectural rules

1. Model calls enter through `IAiChatService` / `IAiStreamingChatService`, backed by `AiChatGateway`.
2. Concrete providers implement `IAiChatProvider` and are resolved by `IAiProviderRegistry`.
3. Request policy is evaluated before a provider is called.
4. Every request receives a correlation id and an explicit provider id.
5. There is no implicit fallback between providers.
6. Operational telemetry records sizes, timings, provider/model identifiers, success state, and error category—not prompts, responses, or credentials.
7. Provider errors are redacted before they leave the governed gateway.
8. Structured JSON output is parsed through `AiStructuredOutputParser`.
9. API keys are accessed through `IAiCredentialStore`; provider settings contain only credential references.

## Runtime flow

```text
UI / AI feature
    -> IAiChatService or IAiStreamingChatService
    -> AiChatGateway
       -> IAiRequestPolicy
       -> IAiProviderRegistry
       -> IAiChatProvider
       -> IAiTelemetrySink
    -> correlated AiChatResponseDto
```

## Registered providers

- `OllamaChatService` for local inference and local model management.
- `OpenAiCompatibleChatProvider` profiles for OpenRouter, OpenAI, Azure OpenAI v1, and custom compatible endpoints.

Provider profiles define endpoint paths, authentication style, model discovery, streaming, temperature behavior, and optional non-secret headers. The generic adapter uses raw HTTP and does not add a vendor SDK dependency.

## Credentials

`WindowsCredentialStore` uses Windows Credential Manager generic credentials. `AiCopilotSettings` stores a `CredentialTarget`, never the secret. The provider dialog supports save, replace, remove, and connection testing without exposing a stored key.

## Cloud governance

Cloud project-context requests require explicit per-provider consent. `DefaultAiRequestPolicy` provides defense in depth: cloud requests marked as containing project context are denied until that provider has consent. Connection tests and model benchmarks explicitly mark themselves as context-free.

The default project payload is structured and summarized. This runtime does not upload the IFC file or project database as files.

## OpenAI-compatible adapter

The adapter supports:

- standard chat-completions request/response shapes;
- Bearer or custom API-key-header authentication;
- configurable base URL and relative paths;
- model-list responses shaped as `data`, `models`, or a root array;
- SSE streaming with cancellation and in-stream error detection;
- string or multipart textual content;
- rate-limit, authentication, quota, model, timeout, and outage errors;
- optional omission of `temperature` for incompatible models.

Azure OpenAI uses the same adapter through a separately configured v1 base URL and `api-key` header. Native adapters can still be added later when a provider materially differs from the compatible contract.

## Extension pattern

A future provider should:

1. implement `IAiChatProvider`;
2. return a stable `AiProviderDescriptor` id;
3. support buffered and streaming calls or a documented buffered fallback;
4. register itself as `IAiChatProvider` in `AddCraneOptimizerAi`;
5. store secrets only through `IAiCredentialStore`;
6. add deterministic mocked-transport tests;
7. preserve explicit selection, consent, redaction, and no-fallback rules.

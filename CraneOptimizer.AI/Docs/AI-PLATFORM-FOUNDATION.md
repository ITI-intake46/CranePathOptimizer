# CraneOptimizer AI Platform Foundation

This milestone introduces the governed runtime that all future LLM, RAG, agent, MCP, voice, and automation capabilities should use.

## Architectural rules

1. Model calls enter through `IAiChatService` / `IAiStreamingChatService`, which are now backed by `AiChatGateway`.
2. Concrete providers implement `IAiChatProvider` and are registered in `IAiProviderRegistry`.
3. Request policy is evaluated before a provider is called.
4. Every request receives a correlation id and provider id.
5. Operational telemetry records only sizes, timings, model/provider identifiers, success state, and an error category. Prompt and response text are not stored in telemetry.
6. Provider error messages are redacted before they leave the governed gateway.
7. Structured JSON output must be parsed through `AiStructuredOutputParser` rather than ad-hoc substring logic.
8. AI implementation remains isolated from the UI, Domain, Application, Infrastructure, and GA experiment projects.

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

## Current provider

`OllamaChatService` is the first registered provider. It advertises chat, streaming, and structured-output capabilities. The endpoint is configurable through `AiCopilotSettings.OllamaEndpoint` and defaults to `http://localhost:11434/`.

## Configuration added

- `DefaultProviderId`
- `OllamaEndpoint`
- `MaxSystemPromptCharacters`
- `MaxUserPromptCharacters`
- `MaxContextCharacters`
- `TelemetryBufferCapacity`

## Extension pattern

A future provider should:

1. Implement `IAiChatProvider`.
2. Return a stable `AiProviderDescriptor` id.
3. Support buffered and streaming calls, using buffered fallback when native streaming is unavailable.
4. Register itself as `IAiChatProvider` in `AddCraneOptimizerAi`.
5. Add contract tests using a deterministic fake transport.

No feature should instantiate or call a concrete provider directly.

## Security and production behavior

`AiSensitiveDataRedactor` removes common bearer tokens, secret assignments, and OpenAI-style keys from surfaced errors. `InMemoryAiTelemetrySink` is bounded and thread-safe. It is intentionally a local operational store; a later production milestone can bridge `IAiTelemetrySink` to OpenTelemetry or another exporter without changing feature code.

## Structured output

`AiStructuredOutputParser` extracts the first complete JSON object or array from plain text or fenced Markdown and deserializes it into a typed contract. Later agent tools and RAG pipelines should use typed contracts and validate them before any host action is proposed.

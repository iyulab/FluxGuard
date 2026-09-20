# FluxGuard

Secure by Default — Guardrails for LLM Applications.

[![NuGet](https://img.shields.io/nuget/v/FluxGuard.svg)](https://www.nuget.org/packages/FluxGuard)
[![.NET](https://img.shields.io/badge/.NET-10.0-blue.svg)](https://dotnet.microsoft.com/)
[![License](https://img.shields.io/badge/License-MIT-green.svg)](LICENSE)

## Philosophy

FluxGuard is a guardrail library designed to **accelerate secure LLM application development**.

### Core Principles

1. **Secure by Default**
   - Protection starts immediately upon installation
   - All core guards are enabled by default
   - No unsafe defaults

2. **Minimal Boilerplate**
   - Start with a single line
   - Ready to use without complex configuration
   - Sensible defaults even with many options

3. **Full Customization**
   - Intercept at every decision point
   - Modify behavior through hook system
   - Completely override default policies

4. **Local First**
   - 95%+ requests processed locally in <20ms
   - Works without external services
   - Remote is optional extension

5. **Graceful Degradation**
   - Guard failures don't block requests (default)
   - Remote timeouts fall back to local results
   - All failure behaviors can be overridden

## Installation

```bash
# This is all you need for most cases
dotnet add package FluxGuard

# For advanced analysis (LLM Judge, Semantic Cache)
dotnet add package FluxGuard.Remote

# For framework integrations (ASP.NET Core, Microsoft.Extensions.AI)
dotnet add package FluxGuard.SDK
```

## Quick Start

```csharp
// One line: the standard preset (the L1 pattern guards below)
var guard = FluxGuard.Create();

var inputCheck = await guard.CheckInputAsync(userMessage);
if (inputCheck.IsBlocked)
{
    return inputCheck.BlockReason;
}

var response = await llm.CompleteAsync(userMessage);

var outputCheck = await guard.CheckOutputAsync(userMessage, response);
if (outputCheck.IsBlocked)
{
    return outputCheck.BlockReason;
}
return response;
```

**This alone provides (standard preset, L1):**
- Prompt injection detection ✅
- Jailbreak attempt detection ✅
- Encoding bypass attack defense ✅
- PII exposure (input) / leakage (output) detection ✅
- Refusal detection on output ✅
- Input / output length limits (`MaxInputLength`, `MaxOutputLength`; 128,000 characters by default) ✅

Not part of any preset: the L2 (local ML) guards and L3 (remote) guards are added explicitly — see
[Guard Layers](#guard-layers).

## Architecture

```
┌─────────────────────────────────────────────────────────────┐
│                      FluxGuard (Core)                       │
├─────────────────────────────────────────────────────────────┤
│                                                             │
│  INPUT ──▶ [L1: Regex] ──▶ [L2: Local ML] ──▶ DECISION     │
│             <1ms            5-20ms                          │
│                                                             │
│  OUTPUT ◀── [L1: Regex] ◀── [L2: Local ML] ◀── LLM        │
│                                                             │
└─────────────────────────────────────────────────────────────┘
                              │
                              ▼ (Optional: FluxGuard.Remote)
┌─────────────────────────────────────────────────────────────┐
│                     FluxGuard.Remote                        │
├─────────────────────────────────────────────────────────────┤
│  [L3: LLM Judge] ──▶ Semantic Analysis ──▶ Final Decision  │
│       50-200ms          (on escalation)                     │
└─────────────────────────────────────────────────────────────┘
```

## Guard Layers

| Layer | Location | Latency | Default |
|-------|----------|---------|---------|
| **L1** | Local | <1ms | ✅ ON (presets register these) |
| **L2** | Local | 5-20ms | ❌ OFF — no preset registers them; `builder.AddL2Guards(sessionManager)` adds them on top of the preset. They need an `OnnxSessionManager` and the model files, and the call throws if a file is missing |
| **L3** | Remote | 50-200ms | ❌ OFF (opt-in) |

## Guards

### Input Guards

| Guard | Description | Layer | On by default |
|-------|-------------|-------|---------------|
| `PromptInjection` | Instruction override detection | L1 | ✅ `EnablePromptInjection` |
| `Jailbreak` | DAN, AIM persona attack blocking | L1 | ✅ `EnableJailbreak` |
| `EncodingBypass` | Base64, Unicode bypass detection | L1 | ✅ `EnableEncodingBypass` |
| `PIIExposure` | PII detection in input | L1 | ✅ `EnablePIIExposure` |
| `L2.PromptInjection` | ML prompt-injection classifier | L2 | ❌ `AddL2Guards(...)` |

### Output Guards

| Guard | Description | Layer | On by default |
|-------|-------------|-------|---------------|
| `PIILeakage` | PII detection in the response (detects and blocks; it does not rewrite the response) | L1 | ✅ `EnablePIILeakage` |
| `Refusal` | Model refusal response detection | L1 | ✅ `EnableRefusal` |
| `L2.Toxicity` | ML toxicity classifier | L2 | ❌ `AddL2Guards(...)` |

Every guard is bounded by `GuardTimeoutMs` (5000 by default). A guard that throws or times out is a guard error:
skipped under `FailMode.Open`, blocking under `FailMode.Closed`.

Rate limiting is not part of this library; use the host's rate limiter (ASP.NET Core `RateLimiter`, or the gateway).
Groundedness / hallucination checks live in the `FluxGuard.Remote` package.

## Configuration

### Builder Pattern

```csharp
var guard = FluxGuard.Create(builder => builder
    .ApplyStandardPreset()
    .ConfigureInputGuards(opt =>
    {
        // Longer inputs are blocked before any guard runs. 0 means no limit.
        opt.MaxInputLength = 8192;
    })
    .ConfigureOutputGuards(opt =>
    {
        opt.MaxOutputLength = 4096;
    }));
```

### Presets

```csharp
// Standard (default)
var guard = FluxGuard.Create();
var guard = FluxGuard.Create(b => b.WithPreset(GuardPreset.Standard));

// Strict - every standard guard with lower escalation thresholds; ApplyStrictPreset() also lowers
// the block / flag thresholds
var guard = FluxGuard.Create(b => b.ApplyStrictPreset());

// Minimal - prompt injection, jailbreak and PII leakage only, minimum latency
var guard = FluxGuard.Create(b => b.ApplyMinimalPreset());
```

Switches set with `ConfigureInputGuards` / `ConfigureOutputGuards` apply to the preset's guards whether they
are set before or after the preset is chosen.

### Dependency Injection

```csharp
// Default registration - Standard preset
services.AddFluxGuard();

// Custom configuration
services.AddFluxGuard(opt =>
{
    opt.FailMode = FailMode.Open;  // default for Minimal/Standard; Strict defaults to Closed
    opt.GuardTimeoutMs = 2000;     // how long the pipeline waits for one guard (default 5000)
});

// Log output follows the host's ILoggerFactory and its filters; the library has no log-level option of its own.
```

## Remote Guard (Optional)

Add only when advanced analysis is needed.

```bash
dotnet add package FluxGuard.Remote
```

```csharp
// OpenAI — model must be set explicitly (no default since 0.11.0)
IFluxGuard guard = FluxGuardBuilder.Create()
    .WithRemoteGuard("your-openai-api-key")
    .WithModel("gpt-4o-mini")
    .WithTimeout(200)             // the local verdict stands when the judge takes longer
    .WithBlockThreshold(0.8)      // default: an "unsafe" verdict under this confidence is reported, not blocked
    .Build()                      // back to the FluxGuardBuilder
    .Build();

// Bring your own IRemoteLlmService
IFluxGuard guard = FluxGuardBuilder.Create()
    .WithRemoteGuard("unused")
    .WithModel("my-model")
    .WithCompletionService(myLlmService)
    .Build()
    .Build();

// DI (ASP.NET Core) — register remote separately
services.AddFluxGuard();
services.AddFluxGuardRemote("your-openai-api-key", opt =>
{
    opt.Judge.Model = "gpt-4o-mini";
    opt.TimeoutMs = 200;
});
```

**Remote provides:**
- LLM-as-Judge analysis of what the local guards escalate
- Caching of judge verdicts
- Hallucination / groundedness detection (L3)

A judge or detector that cannot answer is a guard error, so `FailMode` decides: passed under `Open`, blocked under
`Closed`.

## MCP Guard (Optional)

Validates MCP (Model Context Protocol) tool calls and results — for applications that connect to
MCP servers and want tool-poisoning defenses on that channel. Nothing else in `FluxGuard.Remote`
registers or requires it.

```csharp
// DI (ASP.NET Core)
services.AddFluxGuardMcpGuardrail();

// Manual construction
var guardrail = new MCPToolValidator();
guardrail.RegisterServer(new MCPServerInfo
{
    Name = "my-mcp-server",
    IsTrusted = true,
    AllowedTools = ["read_file", "list_files"]
});
```

**MCP Guard provides:**
- Server/tool allowlisting (`ValidateToolCallAsync`)
- Dangerous-argument pattern detection (shell injection, path traversal, etc.)
- Indirect-injection and sensitive-data checks on tool results (`ValidateToolResultAsync`)
- **Tool description integrity** (`ValidateToolDescriptionsAsync`, opt-in — pass
  `enableToolDescriptionIntegrityCheck: true` to `AddFluxGuardMcpGuardrail()`/`MCPToolValidator`'s
  constructor): hashes each tool's description and input schema the first time a server is seen,
  then flags drift on every later call — catches an MCP server silently rewriting a trusted tool's
  behavior contract after the fact.

## RAG Security Pipeline (Optional)

Validates documents *retrieved* by a RAG pipeline before they reach the LLM's context — for
applications indexing untrusted or third-party content that want indirect-prompt-injection
defenses on that channel. Nothing else in `FluxGuard.Remote` registers or requires it.

```csharp
// DI (ASP.NET Core)
services.AddFluxGuardRagSecurity();

// Manual construction
var pipeline = new IndirectInjectionDetector();
var result = await pipeline.ValidateDocumentAsync(new RAGDocument
{
    Content = retrievedText,
    Source = "vector-store"
});
if (!result.IsSafe)
{
    // result.Threats — detected indirect-injection patterns and their confidence
}
```

**RAG Security Pipeline provides:**
- Indirect prompt-injection detection in retrieved/ingested documents (`ValidateDocumentAsync`/
  `ValidateDocumentsAsync`) — the same detector `MCP Guard`'s `ValidateToolResultAsync` uses
  internally for tool results, exposed here for RAG retrieval content directly.

## SDK Integration

For ASP.NET Core and Microsoft.Extensions.AI integration.

```bash
dotnet add package FluxGuard.SDK
```

### ASP.NET Core Middleware

```csharp
// Program.cs
builder.Services.AddFluxGuard();
builder.Services.AddFluxGuardMiddleware(o =>
{
    o.ProtectedPaths.Add("/api/chat");   // none listed = every path
    o.InputFieldName = "input";          // JSON field holding the text to check
    o.MaxBodySize = 1024 * 1024;         // default; a larger body gets 413 and is neither checked nor passed on
});

app.UseFluxGuard();
```

The middleware checks the body of POST / PUT / PATCH requests. A blocked request is answered with
`BlockedStatusCode`; a flagged one is passed on with `X-FluxGuard-Flagged` / `X-FluxGuard-Score` headers.

### Microsoft.Extensions.AI

```csharp
var chatClient = new ChatClientBuilder(innerClient)
    .UseFluxGuard(new FluxGuardChatClientOptions
    {
        ValidateInput = true,             // default
        ValidateOutput = true,            // default
        ValidateStreamingOutput = true,   // default false: streamed responses are not checked unless you turn this on
    })
    .Build(serviceProvider);              // resolves IFluxGuard from DI
```

A blocked request or response throws `FluxGuardChatBlockedException` (its `Result` is the `GuardResult`). A streamed
response is checked once, on the whole text, when the stream ends: the updates have already been forwarded, so the
exception arrives after the last one and is the caller's signal to retract what it showed.

## Hooks & Customization

Intercept at every decision point.

```csharp
var guard = FluxGuard.Create(builder => builder.WithHooks(hooks => hooks
    // Return false to skip the check entirely
    .OnBeforeCheck(ctx => ValueTask.FromResult(true))
    .OnAfterCheck((ctx, result) => { /* audit */ return ValueTask.CompletedTask; })
    .OnBlocked(async (ctx, result) => await alertService.NotifyAsync(result))
    .OnPassed((ctx, result) => ValueTask.CompletedTask)
    .OnFlagged((ctx, result) => ValueTask.CompletedTask)
    // Override the verdict: null keeps it, AllowPass / ForceBlock replace it
    .OnCustomDecision((ctx, result) => ValueTask.FromResult(
        ctx.UserId == "admin" ? FailDecision.AllowPass("admin bypass") : null))
    // A guard threw or timed out: Continue applies FailMode, AllowPass / ForceBlock decide here
    .OnGuardError((ctx, guardName, ex) => ValueTask.FromResult(FailDecision.Continue))));
```

For the escalation hooks (`OnBeforeEscalationAsync`, `OnEscalationTimeoutAsync`) implement `IFluxGuardHooks`, or
derive from `FluxGuardHooks` and override what you need, and pass it to `WithHooks(...)`.

### Fail Mode

```csharp
services.AddFluxGuard(opt =>
{
    // What a guard that throws or times out means
    opt.FailMode = FailMode.Open;   // skip that guard (availability first)
    opt.FailMode = FailMode.Closed; // block the request (security first)
});
```

This holds for every guard, the L2 and L3 guards included: a guard that cannot run reports an error and the fail
mode decides. For per-error control use the `OnGuardError` hook above.

**When you don't set `FailMode`, it is derived from the preset** (since 0.12.0):

| Preset | Fail mode when unset |
|---|---|
| `Minimal` | `Open` |
| `Standard` (default) | `Open` |
| `Strict` | **`Closed`** |

Choosing `Strict` states "security over availability", so the fail mode follows that intent.
An explicit assignment always wins, in either direction and whatever order it is set in:

```csharp
// Strict, but keep availability first
FluxGuard.Create(b => b.WithPreset(GuardPreset.Strict).WithFailMode(FailMode.Open));
```

> **Security note — outside `Strict`, the default is fail-open.** With `FailMode.Open`, a guard
> that throws (e.g. a regex match timeout on a very long input) is logged as a warning and
> skipped: that request passes **without that guard's verdict**. This is the right default for
> observe-only deployments, but once you *enforce* guard verdicts (blocking requests on
> detection), use `Strict` or set `FailMode.Closed` — otherwise an input engineered to make one
> guard fail silently bypasses it. Guard regexes carry a 1s match timeout as a hard upper bound;
> every bundled pattern is backtracking-safe, so hitting it indicates extreme input size or
> severe host contention.

## Internationalization

The PII guards load pattern sets by language. `SupportedLanguages` selects which sets are loaded; the generic
patterns (email, credit card, API keys, ...) are always on.

| Code | Pattern set |
|------|-------------|
| `en` | US (SSN, phone, ...) |
| `ko` | Korean (resident registration number, phone, credentials, ...) |
| `ja` | Japanese (My Number, phone, ...) |

```csharp
var guard = FluxGuard.Create(builder => builder
    .ConfigureInputGuards(o => o.SupportedLanguages = ["ko", "en"]));   // default: every code
```

The other guards are not filtered by this list.

## Custom Guards

A custom rule is a guard: implement `IInputGuard` (or `IOutputGuard`) and add it. Adding a guard does not replace the
preset when you also name one.

```csharp
public sealed class CompetitorGuard : IInputGuard
{
    public string Name => "Competitor";
    public string Layer => "L1";
    public bool IsEnabled => true;
    public int Order => 200;

    public ValueTask<GuardCheckResult> CheckAsync(GuardContext context) =>
        ValueTask.FromResult(context.NormalizedInput.Contains("competitor1", StringComparison.OrdinalIgnoreCase)
            ? new GuardCheckResult { GuardName = Name, Passed = false, Score = 0.9, Severity = Severity.High, Details = "competitor mention" }
            : GuardCheckResult.Safe);
}

var guard = FluxGuard.Create(builder => builder
    .WithPreset(GuardPreset.Standard)
    .AddInputGuard(new CompetitorGuard()));
```

## Logging & Statistics

Logging goes through the `ILoggerFactory` you pass (`WithLogging(loggerFactory)`, or the container's with
`AddFluxGuard`). Blocks and guard errors are warnings or errors; the rest is debug.

Statistics are recorded when you hand the pipeline a collector. Without one, nothing is recorded.

```csharp
var stats = new InMemoryStatsCollector();          // or FluxGuardMetrics: System.Diagnostics.Metrics, meter "FluxGuard"
var guard = FluxGuard.Create(builder => builder.WithStats(stats));

var snapshot = stats.GetStats();
Console.WriteLine($"Total: {snapshot.TotalChecks}, blocked: {snapshot.BlockedCount} ({snapshot.BlockRate:P1})");
Console.WriteLine($"Avg latency: {snapshot.AverageLatencyMs:F1} ms, guard errors: {snapshot.ErrorCount}");
```

With `AddFluxGuard(...)`, register an `IGuardStatsCollector` in the container and the pipeline picks it up.

## Configuration File

```json
{
  "FluxGuard": {
    "Preset": "Standard",
    "FailMode": "Open",
    "GuardTimeoutMs": 5000,
    "InputGuards": {
      "EnablePromptInjection": true,
      "EnableJailbreak": true,
      "EnableEncodingBypass": true,
      "EnablePIIExposure": true,
      "MaxInputLength": 8192
    },
    "OutputGuards": {
      "EnablePIILeakage": true,
      "EnableRefusal": true,
      "MaxOutputLength": 4096
    }
  }
}
```

`services.AddFluxGuard(configuration)` binds the `FluxGuard` section to `FluxGuardOptions`, so the keys are that
type's property names.

## Performance

| Preset | Latency | Throughput |
|--------|---------|------------|
| Minimal (L1) | <1ms | 100K+ req/s |
| Standard (L1+L2) | 5-20ms | 5K req/s |
| + Remote (L3) | 50-200ms | 500 req/s |

## Packages

| Package | Description | Dependencies |
|---------|-------------|--------------|
| `FluxGuard` | Core guardrails (L1+L2) | ONNX Runtime |
| `FluxGuard.Remote` | Remote analysis (L3) | FluxGuard, HTTP |
| `FluxGuard.SDK` | Framework integrations | FluxGuard, ASP.NET Core, MEAI |

## License

MIT License - see [LICENSE](LICENSE) for details.

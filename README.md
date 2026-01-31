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
// Start with one line - all core guards are enabled
var guard = new FluxGuard();

var inputCheck = guard.CheckInput(userMessage);
if (inputCheck.Blocked)
{
    return inputCheck.BlockedResponse;
}

var response = await llm.CompleteAsync(userMessage);

var outputCheck = guard.CheckOutput(response);
return outputCheck.SanitizedContent ?? response;
```

**This alone provides:**
- Prompt injection detection ✅
- Jailbreak attempt blocking ✅
- Encoding bypass attack defense ✅
- PII exposure/leakage prevention ✅
- Toxic content filtering ✅
- Rate limiting ✅

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
| **L1** | Local | <1ms | ✅ ON |
| **L2** | Local | 5-20ms | ✅ ON |
| **L3** | Remote | 50-200ms | ❌ OFF (opt-in) |

## Default Guards (All ON)

### Input Guards

| Guard | Description | Layer |
|-------|-------------|-------|
| `PromptInjection` | Instruction override detection | L1+L2 |
| `Jailbreak` | DAN, AIM persona attack blocking | L1 |
| `EncodingBypass` | Base64, Unicode bypass detection | L1 |
| `PIIExposure` | PII detection in input | L1 |
| `RateLimit` | Request frequency limiting | L1 |
| `ContentPolicy` | Custom policy rules | L1 |

### Output Guards

| Guard | Description | Layer |
|-------|-------------|-------|
| `Toxicity` | Harmful content filtering | L2 |
| `PIILeakage` | PII masking in response | L1 |
| `FormatCompliance` | JSON schema, length validation | L1 |
| `Refusal` | Model refusal response detection | L1 |
| `Hallucination` | Hallucination detection (context-based) | L2+**L3** |

> The L3 capability of `Hallucination` guard requires the `FluxGuard.Remote` package.

## Configuration

### Builder Pattern

```csharp
var guard = new FluxGuardBuilder()
    .WithInputGuards(opt =>
    {
        // All guards are ON by default - turn OFF if needed
        opt.EnableRateLimit = false;
        opt.RateLimit.RequestsPerMinute = 120;
    })
    .WithOutputGuards(opt =>
    {
        opt.MaxOutputLength = 8192;
        opt.PIIMaskingPattern = "[REDACTED]";
    })
    .Build();
```

### Presets

```csharp
// Standard (default) - L1 + L2, all local guards enabled
var guard = new FluxGuard();
var guard = new FluxGuard(GuardPreset.Standard);

// Strict - Standard + stricter thresholds
var guard = new FluxGuard(GuardPreset.Strict);

// Minimal - L1 only, minimum latency
var guard = new FluxGuard(GuardPreset.Minimal);
```

### Dependency Injection

```csharp
// Default registration - Standard preset
services.AddFluxGuard();

// Custom configuration
services.AddFluxGuard(opt =>
{
    opt.FailMode = FailMode.Open;  // default
    opt.LogLevel = GuardLogLevel.Warning;  // log blocks/errors only
});
```

## Remote Guard (Optional)

Add only when advanced analysis is needed.

```bash
dotnet add package FluxGuard.Remote
```

```csharp
var guard = new FluxGuardBuilder()
    .WithRemoteGuard(opt =>
    {
        opt.CompletionService = myLlmService;
        opt.EscalationThreshold = 0.7f;  // Escalate to L3 at 70%+ suspicion
        opt.TimeoutMs = 200;             // Use L2 result on timeout
    })
    .Build();
```

**Remote provides:**
- LLM-as-Judge advanced analysis
- Semantic caching
- Hallucination detection (L3)
- Multi-model ensemble

## SDK Integration

For ASP.NET Core and Microsoft.Extensions.AI integration.

```bash
dotnet add package FluxGuard.SDK
```

### ASP.NET Core Middleware

```csharp
// Program.cs
builder.Services.AddFluxGuard();
builder.Services.AddFluxGuardMiddleware();

app.UseFluxGuard();
```

### Microsoft.Extensions.AI

```csharp
var chatClient = new ChatClientBuilder()
    .UseFluxGuard()
    .Use(new OpenAIChatClient(...))
    .Build();
```

## Hooks & Customization

Intercept at every decision point.

```csharp
var guard = new FluxGuardBuilder()
    .WithHooks(hooks =>
    {
        // Before/after checks
        hooks.OnBeforeCheck = async ctx => { /* logging, modification */ };
        hooks.OnAfterCheck = async (ctx, result) => { /* audit, notifications */ };

        // Result-specific hooks
        hooks.OnBlocked = async (ctx, result) =>
        {
            await alertService.NotifyAsync(result);
        };

        hooks.OnPassed = async (ctx, result) => { /* statistics */ };

        // Escalation (when using Remote)
        hooks.OnBeforeEscalation = async ctx => { /* pre-L3 processing */ };
        hooks.OnEscalationTimeout = async ctx => { /* fallback logic */ };

        // Custom decision - override default result
        hooks.OnCustomDecision = async (ctx, result) =>
        {
            // Return null to use default result
            // Return GuardDecision to override
            if (ctx.User.IsAdmin)
                return GuardDecision.Pass("Admin bypass");
            return null;
        };
    })
    .Build();
```

### Fail Mode

```csharp
services.AddFluxGuard(opt =>
{
    // Behavior on guard execution error
    opt.FailMode = FailMode.Open;   // Pass (default, availability priority)
    opt.FailMode = FailMode.Closed; // Block (security priority)

    // Or fine-grained control with hooks
    opt.OnGuardError = async (ctx, ex) =>
    {
        logger.LogError(ex, "Guard error");
        return FailDecision.Pass;  // or Block, Retry
    };
});
```

## Internationalization

Built-in support for PII patterns and toxicity detection in major languages.

**Supported Languages:**
- English, Korean, Japanese, Chinese (Simplified/Traditional)
- Spanish, Portuguese, French, German
- Arabic, Hindi, Russian

```csharp
var guard = new FluxGuardBuilder()
    .WithLanguages(Languages.Korean | Languages.English)  // Default: All
    .Build();
```

## Custom Rules

```csharp
// Add input rule
guard.AddInputRule(new PatternRule
{
    Name = "CompetitorBlock",
    Pattern = @"\b(competitor1|competitor2)\b",
    Action = GuardAction.Flag,
    Severity = Severity.Medium
});

// Add output rule
guard.AddOutputRule(new ContentRule
{
    Name = "InternalCodeFilter",
    Keywords = ["INTERNAL:", "DEBUG:", "TODO:"],
    Action = GuardAction.Remove
});
```

## Logging & Metrics

```csharp
var guard = new FluxGuardBuilder()
    .WithLogging(opt =>
    {
        opt.LogLevel = GuardLogLevel.Warning;  // Default: blocks/errors only
        opt.LogDestination = LogDestination.Console;
    })
    .WithMetrics(opt =>
    {
        opt.EnablePrometheus = true;
        opt.MetricsPrefix = "fluxguard";
    })
    .Build();

// Get statistics
var stats = guard.GetStats();
Console.WriteLine($"Total: {stats.TotalChecks}");
Console.WriteLine($"Blocked: {stats.BlockedCount} ({stats.BlockRate:P1})");
Console.WriteLine($"Avg Latency: {stats.AvgLatencyMs:F1}ms");
```

## Configuration File

```json
{
  "FluxGuard": {
    "Preset": "Standard",
    "FailMode": "Open",
    "LogLevel": "Warning",
    "Input": {
      "EnablePromptInjection": true,
      "EnableJailbreak": true,
      "EnableEncodingBypass": true,
      "EnablePII": true,
      "EnableRateLimit": true,
      "MaxInputLength": 8192,
      "RateLimit": {
        "RequestsPerMinute": 60,
        "RequestsPerHour": 500
      }
    },
    "Output": {
      "EnableToxicity": true,
      "EnablePII": true,
      "EnableFormatCompliance": true,
      "MaxOutputLength": 4096
    },
    "Remote": {
      "Enabled": false,
      "EscalationThreshold": 0.7,
      "TimeoutMs": 200
    }
  }
}
```

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

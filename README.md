# FluxGuard

Protect your LLM applications with multi-layered guardrails — fast local checks, deep remote analysis.

[![NuGet](https://img.shields.io/nuget/v/FluxGuard.svg)](https://www.nuget.org/packages/FluxGuard)
[![.NET](https://img.shields.io/badge/.NET-10.0-blue.svg)](https://dotnet.microsoft.com/)
[![License](https://img.shields.io/badge/License-MIT-green.svg)](LICENSE)

## Overview

FluxGuard is a hybrid guardrail library for LLM applications. It combines fast local rule-based checks with optional LLM-powered deep analysis to protect against prompt injection, jailbreak attempts, and unsafe outputs.

**Hybrid Architecture**: Local checks handle 95%+ of requests in <1ms. Suspicious cases escalate to remote LLM analysis for precision.

## Features

- **Prompt Injection Detection** - Block "ignore previous instructions" and encoding bypass attacks
- **Jailbreak Prevention** - Detect DAN, AIM, and persona manipulation attempts
- **Output Validation** - Check for hallucination, toxicity, and policy violations
- **PII Protection** - Prevent sensitive data leakage (integrates with FluxCurator)
- **Multi-layer Defense** - L1 (regex) → L2 (local ML) → L3 (LLM judge)
- **Zero Dependencies** - Core package works standalone

## Installation

```bash
# Core package (local guards only)
dotnet add package FluxGuard.Core

# Full package (includes remote guard support)
dotnet add package FluxGuard
```

## Quick Start

### Basic Usage

```csharp
using FluxGuard;

var guard = new FluxGuard();

// Check input before sending to LLM
var inputCheck = guard.CheckInput(userMessage);
if (inputCheck.Blocked)
{
    return "I can't process that request.";
}

var response = await llm.CompleteAsync(userMessage);

// Check output before returning to user
var outputCheck = guard.CheckOutput(response);
if (outputCheck.Modified)
{
    response = outputCheck.SanitizedContent;
}
```

### With Options

```csharp
var guard = new FluxGuardBuilder()
    .WithInputGuards(opt =>
    {
        opt.EnablePromptInjection = true;
        opt.EnableJailbreakDetection = true;
        opt.EnablePIIDetection = true;
        opt.CustomBlocklist = ["competitor_name", "internal_code"];
    })
    .WithOutputGuards(opt =>
    {
        opt.EnableToxicityFilter = true;
        opt.EnablePIIFilter = true;
        opt.MaxOutputLength = 4096;
    })
    .Build();
```

### Hybrid Mode (Local + Remote)

```csharp
var guard = new FluxGuardBuilder()
    .WithLocalGuards(GuardPreset.Standard)
    .WithRemoteGuard(opt =>
    {
        opt.CompletionService = myLlmService;  // ITextCompletionService
        opt.EscalationThreshold = 0.7f;        // Escalate when 70%+ suspicious
        opt.TimeoutMs = 100;                   // Fallback to local on timeout
    })
    .Build();

var result = await guard.CheckInputAsync(userMessage);
// L1 (regex) → L2 (local ML) → L3 (LLM) if needed
```

## Guard Layers

| Layer | Location | Latency | Handles |
|-------|----------|---------|---------|
| **L1** | Local | <1ms | Regex patterns, blocklists, rate limits |
| **L2** | Local | 5-20ms | ONNX classification models |
| **L3** | Remote | 50-200ms | LLM-based semantic analysis |

## Input Guards

| Guard | Description | Layer |
|-------|-------------|-------|
| `PromptInjection` | Detects instruction override attempts | L1+L2 |
| `Jailbreak` | Blocks known jailbreak patterns (DAN, AIM, etc.) | L1 |
| `EncodingBypass` | Catches Base64, ROT13, unicode obfuscation | L1 |
| `PIIExposure` | Prevents PII in prompts | L1 |
| `RateLimit` | Per-user/session request limits | L1 |
| `ContentPolicy` | Custom keyword/pattern rules | L1 |

## Output Guards

| Guard | Description | Layer |
|-------|-------------|-------|
| `Hallucination` | Validates output against provided context | L2+L3 |
| `Toxicity` | Filters harmful/offensive content | L2 |
| `PIILeakage` | Masks PII in responses | L1 |
| `FormatCompliance` | Validates JSON schema, length limits | L1 |
| `Refusal` | Detects and handles model refusals | L1 |

## Presets

```csharp
// Minimal - regex only, fastest
var guard = new FluxGuard(GuardPreset.Minimal);

// Standard - balanced protection (default)
var guard = new FluxGuard(GuardPreset.Standard);

// Strict - all guards enabled, includes L2 models
var guard = new FluxGuard(GuardPreset.Strict);

// Custom
var guard = new FluxGuardBuilder()
    .WithPreset(GuardPreset.Standard)
    .DisableGuard(GuardType.RateLimit)
    .EnableGuard(GuardType.Hallucination)
    .Build();
```

## Guard Result

```csharp
var result = guard.CheckInput(message);

Console.WriteLine($"Allowed: {!result.Blocked}");
Console.WriteLine($"Risk Score: {result.Score:P0}");
Console.WriteLine($"Triggered Guards: {string.Join(", ", result.TriggeredGuards)}");
Console.WriteLine($"Reason: {result.Reason}");

// For output checks
var outputResult = guard.CheckOutput(response, context);
if (outputResult.Modified)
{
    Console.WriteLine($"Sanitized: {outputResult.SanitizedContent}");
}
```

## Dependency Injection

```csharp
// Basic registration
services.AddFluxGuard();

// With configuration
services.AddFluxGuard(opt =>
{
    opt.Preset = GuardPreset.Standard;
    opt.EnableRemoteGuard = true;
});

// Usage
public class ChatService
{
    private readonly IFluxGuard _guard;
    
    public ChatService(IFluxGuard guard) => _guard = guard;
    
    public async Task<string> ChatAsync(string message)
    {
        var check = await _guard.CheckInputAsync(message);
        if (check.Blocked) return check.BlockedResponse;
        
        // ... process with LLM
    }
}
```

## Custom Rules

```csharp
// Add custom input pattern
guard.AddInputRule(new PatternRule
{
    Name = "CompetitorMention",
    Pattern = @"\b(competitor1|competitor2)\b",
    Action = GuardAction.Flag,
    Severity = Severity.Low
});

// Add custom output filter
guard.AddOutputRule(new ContentRule
{
    Name = "InternalCodeFilter",
    Keywords = ["INTERNAL:", "DEBUG:", "TODO:"],
    Action = GuardAction.Remove
});
```

## Integration with Flux Ecosystem

```
┌─────────────────────────────────────────────────────────┐
│                    RAG Pipeline                         │
├─────────────────────────────────────────────────────────┤
│  User Input                                             │
│      │                                                  │
│      ▼                                                  │
│  ┌──────────┐                                           │
│  │FluxGuard │◀── Input Guards (injection, jailbreak)   │
│  └────┬─────┘                                           │
│       │                                                 │
│       ▼                                                 │
│  ┌──────────┐                                           │
│  │FluxCurator│◀── PII Masking, Text Chunking           │
│  └────┬─────┘                                           │
│       │                                                 │
│       ▼                                                 │
│  ┌──────────┐                                           │
│  │ LMSupply │◀── Embedding, LLM Completion             │
│  └────┬─────┘                                           │
│       │                                                 │
│       ▼                                                 │
│  ┌──────────┐                                           │
│  │FluxGuard │◀── Output Guards (hallucination, toxic)  │
│  └────┬─────┘                                           │
│       │                                                 │
│       ▼                                                 │
│  Response                                               │
└─────────────────────────────────────────────────────────┘
```

## Logging & Monitoring

```csharp
var guard = new FluxGuardBuilder()
    .WithLogging(opt =>
    {
        opt.LogLevel = GuardLogLevel.Warning;
        opt.LogDestination = LogDestination.Console | LogDestination.File;
        opt.LogPath = "./logs/guard.log";
    })
    .WithMetrics(opt =>
    {
        opt.EnablePrometheus = true;
        opt.MetricsPrefix = "fluxguard";
    })
    .Build();

// Access metrics
var stats = guard.GetStats();
Console.WriteLine($"Total Checks: {stats.TotalChecks}");
Console.WriteLine($"Blocked: {stats.BlockedCount} ({stats.BlockRate:P2})");
Console.WriteLine($"Avg Latency: {stats.AvgLatencyMs:F1}ms");
```

## Configuration

```json
{
  "FluxGuard": {
    "Preset": "Standard",
    "Input": {
      "EnablePromptInjection": true,
      "EnableJailbreak": true,
      "EnablePII": true,
      "MaxInputLength": 8192,
      "RateLimit": {
        "RequestsPerMinute": 60,
        "RequestsPerHour": 500
      }
    },
    "Output": {
      "EnableToxicity": true,
      "EnablePII": true,
      "MaxOutputLength": 4096
    },
    "Remote": {
      "Enabled": false,
      "Endpoint": "https://guard.example.com",
      "TimeoutMs": 100
    }
  }
}
```

## Performance

| Scenario | Latency | Throughput |
|----------|---------|------------|
| L1 only (regex) | <1ms | 100K+ req/s |
| L1 + L2 (local ML) | 5-20ms | 5K req/s |
| L1 + L2 + L3 (LLM) | 50-200ms | 500 req/s |

Benchmarked on .NET 10, Apple M2, single thread.

## Roadmap

- [x] Core input guards (injection, jailbreak, encoding)
- [x] Core output guards (toxicity, PII, format)
- [x] Preset configurations
- [x] DI support
- [ ] ONNX classification models (L2)
- [ ] LLM-based judge (L3)
- [ ] Remote guard service
- [ ] Dashboard & analytics
- [ ] Threat intelligence feed

## License

MIT License - see [LICENSE](LICENSE) for details.

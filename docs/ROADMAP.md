# FluxGuard Development Roadmap

> **Secure by Default** — Protection starts immediately upon installation, maximum security with minimal configuration

## Package Structure

```
FluxGuard          → Core (L1 + L2), base package
FluxGuard.Remote   → L3 extension, references FluxGuard
FluxGuard.SDK      → Framework integrations (ASP.NET Core, MEAI)
```

```bash
dotnet add package FluxGuard         # For most cases
dotnet add package FluxGuard.Remote  # When advanced analysis is needed
dotnet add package FluxGuard.SDK     # For framework integrations
```

---

## Architecture

```
┌─────────────────────────────────────────────────────────────┐
│                    FluxGuard (Core)                         │
│                    Default: Standard Preset                 │
│                    All local guards ON                      │
├─────────────────────────────────────────────────────────────┤
│                                                             │
│  INPUT                                                      │
│    │                                                        │
│    ├──▶ [Hook: OnBeforeCheck]                               │
│    │                                                        │
│    ▼                                                        │
│  ┌────────────────┐                                         │
│  │ L1: Regex/Rules│ <1ms                                    │
│  │ ✅ PromptInject │                                        │
│  │ ✅ Jailbreak    │                                        │
│  │ ✅ Encoding     │                                        │
│  │ ✅ PII          │                                        │
│  │ ✅ RateLimit    │                                        │
│  │ ✅ ContentPolicy│                                        │
│  └───────┬────────┘                                         │
│          │                                                  │
│          ▼                                                  │
│  ┌────────────────┐                                         │
│  │ L2: Local ML   │ 5-20ms                                  │
│  │ ✅ PromptInject │ (DeBERTa)                              │
│  │ ✅ Toxicity     │ (Detoxify)                             │
│  └───────┬────────┘                                         │
│          │                                                  │
│          ├──▶ [Hook: OnAfterCheck]                          │
│          ├──▶ [Hook: OnBlocked / OnPassed / OnFlagged]      │
│          ├──▶ [Hook: OnCustomDecision] → Override possible  │
│          │                                                  │
│          ▼                                                  │
│       DECISION (or ESCALATE)                                │
│                                                             │
└──────────┼──────────────────────────────────────────────────┘
           │
           ▼ [Hook: OnBeforeEscalation]
┌─────────────────────────────────────────────────────────────┐
│               FluxGuard.Remote (Optional)                   │
│               Requires WithRemoteGuard() call               │
├─────────────────────────────────────────────────────────────┤
│                                                             │
│  ┌────────────────┐                                         │
│  │ L3: LLM Judge  │ 50-200ms                                │
│  │ - Llama Guard  │                                         │
│  │ - Semantic $   │                                         │
│  │ - Hallucinate  │                                         │
│  └───────┬────────┘                                         │
│          │                                                  │
│          ├──▶ [Hook: OnEscalationTimeout] → L2 fallback     │
│          │                                                  │
│          ▼                                                  │
│    FINAL DECISION                                           │
│                                                             │
└─────────────────────────────────────────────────────────────┘
```

---

## Default Policy

| Item | Default | Description |
|------|---------|-------------|
| Preset | `Standard` | L1 + L2 all local guards ON |
| FailMode | `Open` | Pass on guard error (availability priority) |
| LogLevel | `Warning` | Log only blocks/errors |
| Remote | `OFF` | Requires explicit `WithRemoteGuard()` |
| Languages | `All` | 10 major languages PII/Toxicity support |

---

## Implementation Status

### Phase 0-3: Foundation ✅
- [x] .NET 10 solution structure
- [x] CI/CD pipeline (GitHub Actions)
- [x] Core abstractions (`IFluxGuard`, `GuardResult`, etc.)
- [x] L1 Regex guards (PromptInjection, Jailbreak, Encoding, PII)
- [x] L2 ONNX ML guards (PromptInjection, Toxicity)
- [x] Hook system (`OnBeforeCheck`, `OnAfterCheck`, etc.)
- [x] Builder API and DI integration

### Phase 4: L3 Remote Layer ✅
- [x] `IRemoteGuard` interface
- [x] `ITextCompletionService` abstraction
- [x] L3 LLM Judge implementation
- [x] Semantic caching
- [x] `WithRemoteGuard()` builder extension

### Phase 5: Streaming Support ✅
- [x] `IAsyncEnumerable<TokenValidation>` interface
- [x] Chunk-based validation with sentence boundary detection
- [x] Early termination (`CancellationToken`)
- [x] L1 Streaming PII Guard

### Phase 6: Advanced Output Validation ✅
- [x] `IHallucinationDetector` interface
- [x] Groundedness verification
- [x] `IRAGSecurityPipeline` interface
- [x] Indirect injection detection
- [x] L3 Hallucination and RAG Security guards

### Phase 7: Framework Integration ✅
- [x] ASP.NET Core middleware (`UseFluxGuard()`)
- [x] Microsoft.Extensions.AI (`IChatClient` wrapper)
- [x] FluxGuard.SDK package

### Phase 8: Monitoring ✅
- [x] `GuardStats` statistics container
- [x] OpenTelemetry metrics integration
- [x] In-memory stats collector

### Phase 9: Advanced Security ✅
- [x] Agent permission management (`IAgentGrantManager`)
- [x] Tool invocation guard
- [x] MCP guardrails (`IMCPGuardrail`)
- [x] MCP tool validator

---

## Release Milestones

| Version | Phase | Package | Goal |
|---------|-------|---------|------|
| **0.1.0** | 0-1 | FluxGuard | L1 Regex, Hooks foundation, Standard default |
| **0.2.0** | 2-3 | FluxGuard | L2 ONNX, Hook system complete |
| **0.3.0** | 4 | Remote | L3 Remote (Optional) |
| **0.4.0** | 5 | FluxGuard | Streaming support |
| **0.5.0** | 6 | Remote | Hallucination, RAG security |
| **0.6.0** | 7 | SDK | Framework integrations |
| **0.7.0** | 8 | Both | Monitoring, benchmarks |
| **0.8.0** | 9 | Remote | Agent/MCP security (Preview) |
| **0.9.0** | - | Both | Stabilization, documentation |

---

## Default Values Summary

| Setting | Default |
|---------|---------|
| `new FluxGuard()` | `GuardPreset.Standard` |
| Input Guards | All L1+L2 guards ON |
| Output Guards | Toxicity, PII, Format, Refusal ON |
| FailMode | `Open` (pass) |
| LogLevel | `Warning` (blocks/errors only) |
| Remote | OFF (`WithRemoteGuard()` required) |
| Languages | All (10 languages) |

---

## Performance Targets

| Preset | Latency | Throughput |
|--------|---------|------------|
| Minimal (L1) | <1ms | 100K+ req/s |
| Standard (L1+L2) | 5-20ms | 5K req/s |
| + Remote (L3) | 50-200ms | 500 req/s |

---

## Guard Details

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
| `Hallucination` | Hallucination detection (context-based) | L2+L3 |

### Multilingual PII Support

- English: SSN, Driver License, Credit Card
- Korean: 주민등록번호, 전화번호, 사업자등록번호
- Japanese: マイナンバー, 電話番号
- Chinese: 身份证号, 手机号
- Additional: Spanish, Portuguese, French, German, Arabic, Hindi, Russian

---

## Technology Stack

- **.NET 10** with C# 14 features
- **ONNX Runtime** for local ML inference
- **[GeneratedRegex]** for high-performance pattern matching (56x faster)
- **OpenTelemetry** for metrics and tracing
- **Microsoft.Extensions.AI** for chat client integration

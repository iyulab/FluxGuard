# Changelog

All notable changes to FluxGuard are documented here.

FluxGuard is pre-1.0; minor versions may change behavior. Behavior changes are called out explicitly.

## 0.15.0

### Added

- **Korean credential labels (PII_KO008).** With `ko` enabled, a secret labelled `비밀번호`, `패스워드`,
  `비번` or `암호` followed by `:` or `=` is detected. The English Password pattern (PII009) knows English
  labels only, so a Korean operations document had none of its credentials flagged even with `ko` on.
  Two deliberate limits keep the false-positive cost down: `암호화` (encryption) is not a label, and the
  value must be printable ASCII without spaces, so a Korean phrase after the label (`비밀번호: 관리자에게
  문의`) is not a match. Forms without a separator (`비번 abcd1234`) are out of scope. Confidence 0.75,
  below the English pattern's 0.85.
- **`pw` shorthand on PII009.** `PW : abcd1234` and `pw=abcd1234` now match. The shorthand takes a word
  boundary of its own; the historical labels keep their boundary-free matching.

**Behavior change**: inputs and outputs that carry a Korean-labelled or `pw`-labelled secret now score on
the PII guards where they did not before.

## 0.14.2

### Changed

- **`Microsoft.ML.OnnxRuntime` raised from `1.24.4` to `1.30.0`.** The 1.24.4 hold existed because the
  1.25+ line had regressed a sibling package's CPU Whisper decoder; 1.30.0 has now been exercised
  against real models on that sibling (transcriber, embedder and reranker suites) and this package's
  own suite, with no regression. Consumers that also depend on ORT will resolve to `>= 1.30.0`.

## 0.14.1

### Changed

- **`Microsoft.ML.OnnxRuntime` floor lowered from `1.26.0` to `1.24.4`.** The prior floor landed via
  a routine multi-package dependency sweep, not a deliberate requirement — this package's ML layer
  uses only baseline ORT API (`InferenceSession`, `SessionOptions`, `Tensors`) with nothing specific
  to 1.25/1.26. Because NuGet resolves a consumer's whole graph to the *highest* floor any package
  declares, this package's own floor was forcing every consumer that also depends on ORT (even
  transitively) onto `>= 1.26.0`, overriding any lower pin they needed. 1.24.4 matches
  `modules/lm-supply`'s own re-pin (working around a known ORT 1.26.0 DirectML crash) in this
  ecosystem.

## 0.14.0

### Added

- **`IMCPGuardrail.ValidateToolDescriptionsAsync`** (`FluxGuard.Remote`) — MCP tool
  description/schema integrity verification. Hashes each tool's description and input schema the
  first time a server is seen and flags drift on every later call, defending against an MCP server
  silently rewriting a trusted tool's behavior contract after the fact (tool-poisoning via
  description drift). Opt-in and nested under the existing `IMCPGuardrail` opt-in: pass
  `enableToolDescriptionIntegrityCheck: true` to `AddFluxGuardMcpGuardrail()` or `MCPToolValidator`'s
  constructor — default `false` means zero behavior change for existing consumers. New
  `MCPIssueType.ToolDescriptionDrift` and `MCPToolDescriptor` record.

## 0.13.0

### Added

- **`IServiceCollection.AddFluxGuardRagSecurity()`/`.AddFluxGuardMcpGuardrail()`** (`FluxGuard.
  Remote`) — DI registration for `IRAGSecurityPipeline` (indirect prompt injection detection for
  RAG documents) and `IMCPGuardrail` (MCP tool-call/result validation). Both interfaces and their
  implementations (`IndirectInjectionDetector`, `MCPToolValidator`) have shipped since FluxGuard.
  Remote's first release, but had no DI entry point — a consumer wanting either had to construct
  them by hand. Both are opt-in: nothing else in `FluxGuard.Remote` registers or requires them.

## 0.12.0

### Changed — behavior

- **`FailMode` now derives from `GuardPreset` when it is not set explicitly.**
  `GuardPreset.Strict` resolves to `FailMode.Closed`; `Minimal` and `Standard` keep `FailMode.Open`.

  Choosing `Strict` states "security over availability", but until now a consumer who picked it
  and did not also call `WithFailMode(FailMode.Closed)` still got fail-open: a guard that threw was
  logged and skipped, and the request passed **without that guard's verdict**.

  **Who is affected:** consumers on `GuardPreset.Strict` that never set `FailMode`. For them a
  guard execution error now blocks the request instead of passing it. This is
  breaking-adjacent — the API is unchanged, the behavior is not.

  **Unaffected:** `Minimal`/`Standard` consumers, and anyone who sets `FailMode` explicitly.
  An explicit assignment always wins, in either direction and whatever order it is set in:

  ```csharp
  // Strict, but availability first — unchanged behavior
  FluxGuard.Create(b => b.WithPreset(GuardPreset.Strict).WithFailMode(FailMode.Open));
  ```

  `FluxGuardOptions.FailMode` keeps its `FailMode` (non-nullable) signature, so this is not a
  source or binary break.

### Documentation

- README "Fail Mode" section documents the preset resolution table and the override rule.
- `GuardPreset.Strict` and `FluxGuardOptions.FailMode` XML docs state the linkage.

## 0.11.2

- Guard regex match timeout raised to 1s as a hard upper bound; documented that bundled patterns
  are backtracking-safe, so hitting the timeout indicates extreme input size or host contention.
- README and XML doc security note on the fail-open default.

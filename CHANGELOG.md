# Changelog

All notable changes to FluxGuard are documented here.

FluxGuard is pre-1.0; minor versions may change behavior. Behavior changes are called out explicitly.

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

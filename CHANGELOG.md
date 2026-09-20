# Changelog

All notable changes to FluxGuard are documented here.

FluxGuard is pre-1.0; minor versions may change behavior. Behavior changes are called out explicitly.

## 0.17.0

### Fixed

- **The L2 tokenizer no longer invents tokens when it cannot read its vocabulary.** `TokenizerWrapper` fell back to
  hashing each whitespace-separated word into an id (`Math.Abs(word.GetHashCode()) % 30000 + 1000`) whenever the
  vocabulary file was missing, empty or unreadable, and the classification model then scored those ids, which the guard
  returned as an ordinary verdict. .NET randomises string hash codes **per process**, so those scores were not even
  reproducible between runs. Construction now throws and names the file: a missing path (`FileNotFoundException`), an
  empty path (`ArgumentException`), an unreadable file or one holding no usable token (`InvalidOperationException`).
  This closes the last hole in 0.16.0's rule that L2 explicitly turned on but unable to run is an error rather than a
  pass — `AddL2Guards` checked that the model files *exist*, not that they parse, and a guard built by hand skipped
  even that check.
- **The README no longer states latency and throughput that were never measured.** The Performance table's figures were
  design targets copied from `docs/ROADMAP.md`; no benchmark ships with this repository. Six XML doc comments repeated
  them on `GuardPreset` and the three presets, and those are corrected too.
- **The README said the presets include L2.** "Standard (L1+L2)" and "Core guardrails (L1+L2)" contradicted 0.16.0,
  where no preset registers an L2 guard and `AddL2Guards` is the one call that does. The Packages table now says which
  layer lives in which package and that L2 is opt-in.

### Changed

- **Breaking: `FluxGuard.SDK` takes the ASP.NET Core shared framework** (`FrameworkReference Microsoft.AspNetCore.App`)
  instead of the `Microsoft.AspNetCore.Http` 2.3.x package. That package is a re-release of the previous generation:
  next to a host that already carries the shared framework, the same types can resolve from two places, and the two are
  serviced on separate schedules. Middleware consumers are unaffected. A consumer using only `FluxGuardChatClient` from
  a non-ASP.NET app now needs the ASP.NET Core runtime — say so if that is you, because splitting the
  Microsoft.Extensions.AI integration into its own package is the answer if anyone is in that position.

### Removed

- **Breaking: `TokenizerWrapper.IsVocabularyLoaded`.** It had no callers, and after the change above it can only be
  true.

## 0.16.0

### Added

- **`FluxGuardBuilder.AddL2Guards(sessionManager, options)` turns the L2 (local ML) guards on.** No preset registers
  them, because they load ONNX models this library does not ship; until now the only way in was to construct
  `L2PromptInjectionGuard` and `L2ToxicityGuard` by hand. The call adds both on top of whatever the builder resolves
  to (the default preset included) and throws `InvalidOperationException`, naming the files, if a model or
  vocabulary file is missing.
- **`FluxGuardBuilder.WithStats(collector)` feeds a statistics collector from the pipeline.** `InMemoryStatsCollector`
  and `FluxGuardMetrics` were public and nothing called them, so `GetStats()` stayed empty whatever was checked.
  The pipeline now records every check, every guard execution with its latency, and every guard error into the
  collector it is given. With `AddFluxGuard(...)`, a registered `IGuardStatsCollector` is picked up. Without a
  collector nothing is recorded.
- `StreamingGuardOptions.FailMode` (default `Open`): what a streaming guard that throws means. `Closed` terminates
  the stream, with the guard's name in the verdict; `Open` skips the guard, as before.

### Removed

- **Breaking: switches for guards that do not exist are gone.** Each was public, documented, several defaulted to
  `true`, and nothing read them, so setting one changed nothing:
  `FluxGuardOptions.EnableL2Guards` and `FluxGuardBuilder.DisableL2Guards()` (L2 guards are added with
  `AddL2Guards`, so there is nothing to disable), `OutputGuardOptions.EnableToxicity` (same),
  `InputGuardOptions.EnableRateLimit` (rate limiting belongs to the host: ASP.NET Core `RateLimiter` or the
  gateway), `InputGuardOptions.EnableContentPolicy`, `OutputGuardOptions.EnableFormatCompliance`,
  `OutputGuardOptions.EnablePIIMasking` and `PIIMaskChar` (the PII guards detect and block; nothing rewrites text),
  `FluxGuardOptions.LogLevel` (log output follows the host's `ILoggerFactory` filters) and
  `L2GuardOptions.TimeoutMs` (`FluxGuardOptions.GuardTimeoutMs` bounds every guard).
  Migration: delete the assignment. If you set `EnableToxicity = false` or `EnableContentPolicy = false` to turn a
  guard off, it was never on.
- **Breaking: `RemoteGuardOptions.MaxRetries` is removed.** Nothing retried. Retries belong to the `HttpClient`
  pipeline (or to your own `IRemoteLlmService`), where the host already configures resilience.
- **Breaking: `ModelLoader.DownloadMissingModelsAsync`, `ModelDownloadProgress` and `DownloadStatus` are removed.**
  The method downloaded nothing and returned `false`.

### Fixed

- **`LLMJudgeOptions.BlockThreshold` (and `WithBlockThreshold(...)`) now decides what blocks.** Nothing read it: any
  "unsafe" verdict from the judge blocked, whatever its confidence. An unsafe verdict now blocks when its confidence
  is at or above the threshold (0.8 by default); below it the verdict is still reported - score, severity and
  reasoning reach `TriggeredGuards` - without blocking. A verdict that carries no confidence blocks, as before.
  **Behavior change:** an unsafe verdict with a confidence under 0.8 used to block and now flags. Lower the
  threshold to keep the old behavior (`0` blocks every unsafe verdict).
- **The L3 guards answered "pass" whenever they could not run.** `L3LLMJudgeGuard` (judge unreachable, empty or
  unparseable reply), `L3HallucinationGuard` and `L3RAGSecurityGuard` (any exception) each "failed open" on their
  own, so a pipeline configured with `FailMode.Closed` never saw the failure. They now throw and the pipeline's
  `FailMode` decides. The judge also cached the "pass" it invented for an unparseable reply, so the same input
  kept passing for the cache lifetime; a failed call is no longer cached.
  **Behavior change:** under `FailMode.Closed`, an unreachable judge or detector now blocks. Under `FailMode.Open`
  the outcome is the same as before.
- **`FluxGuardChatClientOptions.ValidateStreamingOutput` now checks a streamed response.** Nothing read it, so a
  streamed response was never checked, while the same response fetched without streaming was. With the option on,
  the text is collected as the updates go by and checked once when the stream ends; a blocked response surfaces as
  a `FluxGuardChatBlockedException` thrown after the last update, the caller's signal to retract what it showed.
  The default stays off. `ValidateOutput = false` turns it off too.
- **`FluxGuardMiddlewareOptions.MaxBodySize` is now enforced.** Nothing read it: the middleware buffered and read a
  body of any size. A body over the limit on a protected path is answered with `413 Payload Too Large` and is
  neither checked nor passed on - skipping the check instead would let padding carry anything past the guard. The
  limit holds for chunked requests too (it is applied while reading). `0` means no limit.
  **Behavior change:** the default is 1 MB, so a protected endpoint that accepted larger bodies now refuses them.
- **`StreamingGuardOrchestrator` sent the end of the stream twice.** Its final result carried the unprocessed tail
  of the buffer as `OutputChunk`, after every chunk had already been forwarded, so a caller that forwards
  `OutputChunk` repeated that tail (the whole output, with sentence-level validation off). The final result now
  carries the verdict on the whole output and no text; `IsTerminated` is set on it when that verdict says to stop.
  **Behavior change:** the final result's `OutputChunk` is always null and its `OriginalChunk` is empty.
- **`StreamingGuardOptions.MinChunkSize` is now honoured.** Nothing read it. Chunks are held until that many
  characters have arrived and are validated (and forwarded) together; what is still held when the stream ends is
  validated before the final result. The default of 1 validates every chunk, as before.
- **The L2 guards answered "safe" whenever they could not run** - model not registered, inference failure, any
  exception - whatever the fail mode. They now throw, so the pipeline's `FailMode` decides: skipped and reported to
  `OnGuardErrorAsync` under `FailMode.Open`, blocking under `FailMode.Closed`.
  **Behavior change:** a pipeline that holds an L2 guard without its model file used to pass everything silently; it
  now logs a guard error per check, and blocks under `FailMode.Closed`.

- **`InputGuardOptions.MaxInputLength` and `OutputGuardOptions.MaxOutputLength` are now enforced.** Both were
  declared, documented and shown in the README's configuration example, and nothing read them: text of any
  length went through. A check whose input (or output) is longer than the limit is now blocked before
  normalization and before any guard runs, with `GuardName` `InputLength` / `OutputLength` in
  `TriggeredGuards` and the two lengths in `BlockReason`. The block runs the same `OnBlocked` / `OnAfterCheck`
  hooks as any other.
  **Behavior change:** the defaults are 128,000 characters each, so a consumer that never set them will start
  blocking text longer than that. Set the option to `0` for no limit.
- **`FluxGuard.Create()` built a pipeline with no guards.** It is documented as the standard preset, but nothing
  applied a preset on the builder path, so every check passed. `WithPreset(...)` had the same fate: it stored a
  value nothing read. A builder that is given no guard and no preset now gets the preset named by
  `FluxGuardOptions.Preset` (standard by default); `WithPreset` applies the preset it names; a builder that is
  given guards and no preset still gets exactly those guards.
  **Behavior change:** code that relied on `FluxGuard.Create()` never blocking will now see the standard guards.
- **`ApplyStandardPreset()` / `ApplyStrictPreset()` ignored switches configured on the builder.** They read a fresh
  default options object, so `ConfigureInputGuards(o => o.EnablePromptInjection = false)` had no effect on the
  guards they added. The preset's guards are now created when the pipeline is built, from the builder's final
  options, whichever order the calls came in. The builder and `AddFluxGuard(...)` share that one path.
- **`FluxGuardOptions.GuardTimeoutMs` is now enforced.** It was declared with a default of 5000 and nothing read
  it, so a guard that never answered held the request for as long as it liked. The pipeline now waits that long
  for each guard. A guard that outlives it is handled as a guard error: skipped under `FailMode.Open`, blocking
  under `FailMode.Closed`, and reported to `OnGuardErrorAsync` as a `TimeoutException` either way. `0` means no
  timeout. The same wait also observes the caller's cancellation token, so cancelling a check no longer waits for
  a guard that ignores the token.
  **Behavior change:** a custom guard slower than 5 seconds is now skipped (or blocks, under `FailMode.Closed`).
- The README's Hooks, Fail Mode, Internationalization, Custom Rules, Logging & Metrics and Remote Guard sections
  described an API that does not exist (`WithLanguages`, `WithMetrics`, `GuardLogLevel`, `AddInputRule`,
  `PatternRule`, `opt.OnGuardError`, `guard.GetStats()`, property-style hooks, a "multi-model ensemble"). They now
  show the API as it is; every member named in a C# block is checked against the source.
- The README's builder example called members that do not exist (`WithInputGuards`, `WithOutputGuards`,
  `PIIMaskingPattern`, `RateLimit.RequestsPerMinute`). It now uses the API as it is.
- The README's Quick Start and Presets sections constructed `new FluxGuard(...)` (a static class), read
  `Blocked` / `BlockedResponse` / `SanitizedContent` (not members of `GuardResult`), and listed toxicity filtering
  and rate limiting as on by default. They now use `FluxGuard.Create(...)` and `IsBlocked` / `BlockReason`, and
  say what a preset registers: the L1 guards. The L2 (local ML) guards are not part of any preset.

## 0.15.1

### Changed

- Microsoft.Extensions.* / Microsoft.Data.Sqlite / EF Core pins raised to 10.0.12 (September 2026 .NET servicing).

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

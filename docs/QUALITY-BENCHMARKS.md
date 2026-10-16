# Quality Benchmarks

Which public benchmark measures how well FluxGuard's guards classify content — and where no established benchmark exists.
A guard is a classifier, so the measure is precision and recall on labelled safe and unsafe text, not a model's attack
success rate.

## Harmful content and toxicity: GuardBench

[GuardBench](https://github.com/amenra/guardbench) (EMNLP 2024, [leaderboard](https://huggingface.co/spaces/AmenRa/guardbench-leaderboard))
runs a guard over 40 labelled English datasets and reports precision, recall, F1, MCC and AUPRC; the guard under test is a
callable that returns the probability that a text is unsafe.

| Choice | Value |
|---|---|
| Subset | the sets that mix safe and unsafe items: OpenAI Moderation, ToxicChat (version 0124), XSTest (over-blocking) and BeaverTails |
| Why a subset | many of the 40 sets contain only unsafe prompts, where F1 behaves like recall |
| Metric | F1 and AUPRC per set, with the preset used (L1 rule guards only, or L1 + model guards) |
| Data licences | per dataset (OpenAI Moderation MIT; ToxicChat CC BY-NC 4.0) — check each before redistributing results with data |

## Where no established benchmark fits

- **Prompt injection**: the open sets are small (deepset/prompt-injections, 662 rows), measure only over-blocking (NotInject,
  339 benign samples) or are not peer-reviewed; Lakera's PINT mixes proprietary data and its repository is archived.
- **PII detection**: synthetic sets (ai4privacy) and single-domain corpora (TAB, court cases) exist, but no standard protocol or
  leaderboard — and nothing established for Korean.
- **Topic restriction**: one synthetic set (CantTalkAboutThis), with no common evaluation.

Red-teaming suites (JailbreakBench, HarmBench, AgentDojo, BIPIA) measure whether a *model* can be attacked; their prompts
are usable only as positive examples, which GuardBench already does.

## Status

The built-in guards are L1 pattern guards for prompt injection, jailbreak, encoding bypass and PII — not a toxicity
classifier — so the GuardBench toxicity sets measure what an added model guard would do, and for L1 they show only that
it does not try. What L1 is for was measured on the sets that label it (2026-10-15, `IFluxGuard.CheckInputAsync`,
standard preset, positive = blocked):

| Set | Label | 0.21.0 precision / recall | unreleased (next release) | Benign blocked |
|---|---|---|---|---|
| deepset/prompt-injections, test split (60 injections of 116) | injection | 1.000 / 0.067 | 1.000 / 0.350 | 0 of 56 → 0 of 56 |
| ToxicChat 0124 test (91 jailbreaks of 5,083) | jailbreaking | 0.611 / 0.363 | 0.607 / 0.374 | 21 → 22 of 4,992 |
| XSTest safe prompts (250) | — (over-blocking) | — | — | 0 → 0 |
| ToxicChat 0124 test (362 toxic) | toxicity (not an L1 target) | 0.630 / 0.094 | — | — |

The new patterns were written against the deepset **train** split and judged on the test split and the two benign sets.
What the injection guard still misses is mostly role and persona prompts («I want you to act as a linux terminal»), which
deepset labels injections and which are ordinary prompts in most applications, and paraphrases a pattern cannot cover
without blocking benign text — a model-based check (a custom guard, or the L3 judge) is the layer for those.

### Korean, and benign blocks after the verdict and number-pattern fixes (0.23.0)

No public prompt-injection set has Korean, so a set was authored for the consumer case — a Korean file or operations
assistant: 50 benign requests, many carrying the words a pattern keys on (무시 · 이전 · 지시 · 규칙 · 프롬프트 · 시스템 ·
설정 · 우회 · 필터 · 검열), and 15 textbook injections. Standard and strict preset alike:

| Set | 0.22.1 | 0.23.0 |
|---|---|---|
| Korean injections blocked (15) | 8 | 15 |
| Korean benign blocked (50) | 0 | 0 |
| ToxicChat 0124 test, benign blocked (4,992) | 22 | 18 |
| ToxicChat 0124 test, jailbreak recall (91) | 0.374 | 0.374 |
| deepset/prompt-injections test + train, recall / benign blocked (263 / 399) | 0.285 / 0 | 0.285 / 0 |
| XSTest safe prompts, blocked (250) | 0 | 0 |

The Korean patterns were written against those 15 injections, so 15/15 is a fit, not a held-out score; the benign half
and a near-miss fact per pattern in the tests are what keep them from over-blocking. A second set written after 0.23.0
and apart from its patterns (40 benign, 20 injections in other phrasings) measures what the first cannot: **3 of 20**
injections blocked, 0 of 40 benign on 0.23.1 (0.23.0 blocked one order number as a My Number). The Korean L1 patterns
catch the textbook phrasings; paraphrases — other nouns («정책», «가이드라인»), other verbs («적어줘», «코드블록으로»),
personas and redirections — pass. For Korean deployments that need more, put a model-based check (the L3 judge or a
custom guard) behind L1. The four fewer ToxicChat benign
blocks come from the PII changes: a passed PII match no longer blocks by score, and a number of the right length is no
longer read as an SSN or a My Number without its written form, a label or a valid check digit.

### Korean general shapes (0.24.0)

Two pattern groups (`PI021` KoreanGeneralShapes, `JB010` KoreanPersonaOrMode) generalise the Korean coverage beyond the
textbook phrasings: dismissing instructions the model was given or all its policies, reading out its given, hidden or
initial rules (as the object of a read-out verb), the prompt read out verbatim, declaring the earlier instructions a
test, unrestricted personas and unfiltered modes. They were tuned on the second set above, which is therefore no longer
held out. A third set measures them: 60 injections across five families (overriding, extracting the prompt, persona or
mode switches, redirections and fake system messages, and injections embedded in quoted documents or obfuscated by
spacing, romanization or initial consonants) and 60 benign requests that share the attack vocabulary (규칙 · 지침 · 정책 ·
프롬프트 · 무시 · 원문 · 관리자 · 모드), written by an author who never saw the patterns.

| Standard preset | textbook (15 / 50) | tuning set (20 / 40) | third set (60 / 60) |
|---|---|---|---|
| 0.23.1, injections / benign blocked | 15 / 0 | 3 / 0 | 9 / 0 |
| 0.24.0, injections / benign blocked | 15 / 0 | 20 / 0 | **16 / 0** |

On text written apart from the patterns, Korean L1 recall goes from 0.15 to 0.27 at no cost in precision across 150
benign Korean prompts. That is the honest ceiling of a regular-expression layer for an agglutinative, free-word-order
language: the shapes it can name are the common copy-paste attacks. Obfuscated and embedded injections pass, and the
contract above stands — for Korean deployments that need more, put a model-based check behind L1.

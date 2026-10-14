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

Not yet measured. Results will be added here with the package version, the preset and the model behind any model-based guard.

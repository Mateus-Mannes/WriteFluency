# Mistake Pattern Classification Evals

Manual evaluator for `OpenAiMistakePatternClassifier`.

The source of truth is
`tests/propositions-service/WriteFluency.CorrectionOrchestration.Evals/examples2-generated-phrases-20260702.json`.
Each `expectedFinalComparisons` entry provides the expected tags and reference
`studentPhrase`.

The evaluator calls the live OpenAI-backed classifier and writes one self-contained
HTML report to:

```text
artifacts/mistake-pattern-evals/{timestamp}/report.html
```

## Validate Fixtures

```bash
dotnet run --project tests/propositions-service/WriteFluency.MistakePatternClassification.Evals -- --validate-only
```

If local MSBuild servers are stuck, build once and run the compiled assembly:

```bash
dotnet exec tests/propositions-service/WriteFluency.MistakePatternClassification.Evals/bin/Debug/net10.0/WriteFluency.MistakePatternClassification.Evals.dll --validate-only
```

## Run Live Evaluation

```bash
dotnet run --project tests/propositions-service/WriteFluency.MistakePatternClassification.Evals -- \
  --runs 1 \
  --concurrency 2 \
  --report-only
```

Useful overrides:

```bash
--case real-smart-apostrophe
--model gpt-5.4-nano-2026-03-17
--temperature 0.2
--max-comparisons-per-request 10
--input-usd-per-million-tokens 0.05
--output-usd-per-million-tokens 0.40
```

The evaluator defaults to `0.05` input USD per million tokens and `0.40` output
USD per million tokens for the current nano model. Override those flags when
testing another model or when pricing changes.

Tag scoring is deterministic. Phrase scoring compares the generated phrase with
the human reference phrase using token F1 and normalized edit similarity; a
phrase passes when either score is at least 0.80 and no hard style rule fails.

This project is intentionally not part of the default unit-test quality gate
because live AI classification quality is stochastic.

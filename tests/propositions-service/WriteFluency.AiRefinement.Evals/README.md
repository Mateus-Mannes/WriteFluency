# AI Refinement Evaluator

This is a local-only prompt and model evaluator for US-010. It is a console
application included in `WriteFluency.sln`, but it is not a test project.
Solution-wide `dotnet build` commands compile it, while `dotnet test` does not
execute it or make real OpenAI calls.

The committed `ai-refinement-eval-cases.json` manifest is self-contained. It
contains the original text, user text, source comparison, and human-reviewed
expected ranges required by each case. The evaluator does not read
`corrections.json` or any external dataset.

## Run

Set `ExternalApis:OpenAI:Key` in the shared WriteFluency user secrets or export
`OPENAI_API_KEY`, then run from the repository root:

```bash
dotnet run --project tests/propositions-service/WriteFluency.AiRefinement.Evals
```

Optional arguments:

```text
--model <model-id>
--runs <positive integer>
--case <case-id>
--report-only
--validate-only
```

The evaluator exits with code `1` when quality thresholds fail, unless
`--report-only` is used. Reports are written under the ignored
`artifacts/ai-evals/` directory:

- `report.md` contains the evaluation summary and per-case metrics.
- `report.json` contains the structured evaluation result.
- `highlights.json` contains each AI-selected range and the exact original and
  user snippets represented by that range. Removed corrections have
  `highlights: null`.

Reports contain case identifiers, indexes, selected snippets, metrics, latency,
and token usage, but not full user attempts.

Use `--validate-only` to verify manifest deserialization and range consistency
without requiring an API key or making OpenAI calls.

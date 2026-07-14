# Correction Orchestration Evaluator

This is a local-only correction orchestration evaluator for US-010. It is a console
application included in `WriteFluency.sln`, but it is not a test project.
Solution-wide `dotnet build` commands compile it, while `dotnet test` does not
execute it.

The committed `orchestration-eval-cases.json` manifest is self-contained. It
contains the original text, user text, source comparisons, expected final
comparisons, and human-reviewed expected correction traces required by each
case. The evaluator does not read
`corrections.json` or any external dataset.

Cases can contain either one source comparison or a full attempt with multiple
source comparisons. Full-attempt cases are passed through the same correction
orchestration flow used by the API and graded independently per source
comparison. The original edge case remains the focus comparison while the other
comparisons provide realistic request context.
`report.json` and `highlights.json` include the source-level results.

## Run

Run from the repository root:

```bash
dotnet run --project tests/propositions-service/WriteFluency.CorrectionOrchestration.Evals
```

Optional arguments:

```text
--runs <positive integer>
--concurrency <positive integer>
--case <case-id>
--report-only
--validate-only
```

Concurrency defaults to `1`. Use bounded concurrency to reduce evaluation time:

```bash
dotnet run --project tests/propositions-service/WriteFluency.CorrectionOrchestration.Evals -- \
  --runs 4 \
  --concurrency 4
```

Runs share the same concurrency limit.

The evaluator exits with code `1` when quality thresholds fail, unless
`--report-only` is used. Reports are written under the ignored
`artifacts/correction-evals/` directory:

- `report.md` contains the evaluation summary and per-case metrics.
- `report.html` is the primary interactive report. It groups repeated runs by
  case, lists failed comparisons first, supports search and status/category
  filters, and provides expected-versus-actual text and range drilldowns.
- `report.json` contains the structured evaluation result.
- `highlights.json` contains the full original and user text, source
  comparison, expected highlights, and actual highlights for each case.
  Removed corrections have a `null` highlight collection.

Reports contain the sanitized full attempts embedded in the evaluation
manifest, along with case identifiers, indexes, selected snippets, metrics,
and latency.

Use `--validate-only` to verify manifest deserialization and range consistency
without running the orchestration flow.

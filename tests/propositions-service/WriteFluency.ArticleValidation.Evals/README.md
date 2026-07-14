# Article Validation Evaluator

Small OpenAI-backed grader for the article-content validation prompt in `OpenAIClient`.

The fixture includes valid article-like cases from real public news/source pages, invalid commercial/listicle/deals cases from real source pages, and mocked malformed extraction cases such as HTML-only and navigation-only text.

Run fixture validation only:

```bash
dotnet run --project tests/propositions-service/WriteFluency.ArticleValidation.Evals -- --validate-only
```

Run the grader:

```bash
dotnet run --project tests/propositions-service/WriteFluency.ArticleValidation.Evals -- --report-only
```

Useful options:

```bash
--case valid_noaa_hurricane_outlook
--model gpt-5.4-nano-2026-03-17
--runs 3
--report-only
```

Configure the OpenAI key through the same user secrets id used by WebApi, NewsWorker, and the other evaluators, or through environment variables:

```bash
dotnet user-secrets set "ExternalApis:OpenAI:Key" "<key>" --project tests/propositions-service/WriteFluency.ArticleValidation.Evals
```

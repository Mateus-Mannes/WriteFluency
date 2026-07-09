using System.Text.Json;

namespace WriteFluency.ArticleValidation.Evals;

public static class EvaluationReportWriter
{
    private static readonly JsonSerializerOptions JsonOptions =
        new(JsonSerializerDefaults.Web)
        {
            WriteIndented = true
        };

    public static async Task<string> WriteAsync(
        EvaluationRunSummary summary,
        CancellationToken cancellationToken)
    {
        var outputDirectory = Path.Combine(
            AppContext.BaseDirectory,
            "artifacts",
            "article-validation");
        Directory.CreateDirectory(outputDirectory);

        var path = Path.Combine(
            outputDirectory,
            $"{DateTimeOffset.UtcNow:yyyyMMdd-HHmmss}-summary.json");
        await File.WriteAllTextAsync(
            path,
            JsonSerializer.Serialize(summary, JsonOptions),
            cancellationToken);

        Console.WriteLine($"JSON report: {path}");
        return path;
    }
}

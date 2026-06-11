using Npgsql;

namespace WriteFluency.ExerciseTransfer;

internal sealed record TransferOptions(
    int ExerciseId,
    bool Replace,
    string? KubernetesContext,
    string KubernetesNamespace,
    string KubernetesSecretName,
    string LocalPostgresConnectionString,
    MinioConnectionOptions LocalMinio,
    bool AllowNonLocalDestination,
    bool ShowHelp)
{
    private const string DefaultLocalPostgres =
        "Host=127.0.0.1;Port=5432;Database=wf-propositions-postgresdb;Username=postgres;Password=postgres";

    private const string DefaultLocalMinio =
        "Endpoint=http://127.0.0.1:9000;AccessKey=minioadmin;SecretKey=admin123";

    public static TransferOptions Parse(string[] args)
    {
        if (args.Any(arg => arg is "--help" or "-h"))
        {
            return new TransferOptions(
                0,
                false,
                null,
                "writefluency",
                "wf-propositions-secrets",
                DefaultLocalPostgres,
                MinioConnectionOptions.Parse(DefaultLocalMinio),
                false,
                true);
        }

        var values = ParseArguments(args);
        if (!values.TryGetValue("--id", out var exerciseIdValue)
            || !int.TryParse(exerciseIdValue, out var exerciseId)
            || exerciseId <= 0)
        {
            throw new ArgumentException("A positive exercise ID is required. Example: --id 2708");
        }

        var localPostgres = GetValue(
            values,
            "--local-postgres",
            "EXERCISE_TRANSFER_LOCAL_POSTGRES",
            DefaultLocalPostgres);

        var localMinio = GetValue(
            values,
            "--local-minio",
            "EXERCISE_TRANSFER_LOCAL_MINIO",
            DefaultLocalMinio);

        var options = new TransferOptions(
            exerciseId,
            values.ContainsKey("--replace"),
            GetOptionalValue(values, "--context", "EXERCISE_TRANSFER_KUBE_CONTEXT"),
            GetValue(values, "--namespace", "EXERCISE_TRANSFER_KUBE_NAMESPACE", "writefluency"),
            GetValue(values, "--secret", "EXERCISE_TRANSFER_KUBE_SECRET", "wf-propositions-secrets"),
            localPostgres,
            MinioConnectionOptions.Parse(localMinio),
            values.ContainsKey("--allow-non-local-destination"),
            false);

        options.ValidateDestination();
        return options;
    }

    public static void PrintHelp()
    {
        Console.WriteLine(
            """
            Copies one complete exercise from the production Kubernetes environment into local Aspire.

            Usage:
              dotnet run --project src/tools/WriteFluency.ExerciseTransfer -- --id <exercise-id> [options]

            Options:
              --id <id>                         Required production exercise ID.
              --replace                         Replace the local exercise when the same ID exists.
              --context <name>                  Kubernetes context. Defaults to the current context.
              --namespace <name>                Kubernetes namespace. Default: writefluency.
              --secret <name>                   Production secret. Default: wf-propositions-secrets.
              --local-postgres <connection>     Local PostgreSQL connection string.
              --local-minio <connection>        Local MinIO connection string.
              --allow-non-local-destination     Disable the localhost destination guard.
              --help                            Show this help.

            Environment variable alternatives:
              EXERCISE_TRANSFER_KUBE_CONTEXT
              EXERCISE_TRANSFER_KUBE_NAMESPACE
              EXERCISE_TRANSFER_KUBE_SECRET
              EXERCISE_TRANSFER_LOCAL_POSTGRES
              EXERCISE_TRANSFER_LOCAL_MINIO

            MinIO connection string format:
              Endpoint=http://127.0.0.1:9000;AccessKey=minioadmin;SecretKey=admin123
            """);
    }

    private void ValidateDestination()
    {
        if (AllowNonLocalDestination)
        {
            return;
        }

        var postgres = new NpgsqlConnectionStringBuilder(LocalPostgresConnectionString);
        if (!IsLocalHost(postgres.Host ?? string.Empty) || !IsLocalHost(LocalMinio.Endpoint.Host))
        {
            throw new InvalidOperationException(
                "The destination must use localhost. Pass --allow-non-local-destination only when this is intentional.");
        }
    }

    private static Dictionary<string, string?> ParseArguments(string[] args)
    {
        var values = new Dictionary<string, string?>(StringComparer.Ordinal);
        for (var index = 0; index < args.Length; index++)
        {
            var argument = args[index];
            if (!argument.StartsWith("--", StringComparison.Ordinal))
            {
                throw new ArgumentException($"Unexpected argument '{argument}'.");
            }

            if (argument is "--replace" or "--allow-non-local-destination")
            {
                values[argument] = null;
                continue;
            }

            if (index + 1 >= args.Length || args[index + 1].StartsWith("--", StringComparison.Ordinal))
            {
                throw new ArgumentException($"Option '{argument}' requires a value.");
            }

            values[argument] = args[++index];
        }

        return values;
    }

    private static string GetValue(
        IReadOnlyDictionary<string, string?> values,
        string option,
        string environmentVariable,
        string defaultValue)
    {
        return GetOptionalValue(values, option, environmentVariable) ?? defaultValue;
    }

    private static string? GetOptionalValue(
        IReadOnlyDictionary<string, string?> values,
        string option,
        string environmentVariable)
    {
        if (values.TryGetValue(option, out var optionValue) && !string.IsNullOrWhiteSpace(optionValue))
        {
            return optionValue;
        }

        var environmentValue = Environment.GetEnvironmentVariable(environmentVariable);
        return string.IsNullOrWhiteSpace(environmentValue) ? null : environmentValue;
    }

    private static bool IsLocalHost(string host)
    {
        return host.Equals("localhost", StringComparison.OrdinalIgnoreCase)
            || host.Equals("127.0.0.1", StringComparison.OrdinalIgnoreCase)
            || host.Equals("::1", StringComparison.OrdinalIgnoreCase);
    }
}

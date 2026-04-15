namespace WriteFluency.UsersProgressService.Options;

public sealed class CosmosProgressOptions
{
    public const string SectionName = "Cosmos";

    public string Endpoint { get; set; } = string.Empty;

    public string DatabaseName { get; set; } = "wf-users-progress";

    public string ProgressContainer { get; set; } = "user_progress";

    public string AttemptsContainer { get; set; } = "user_attempts";

    public string Namespace { get; set; } = "local";

    public bool IsConfigured =>
        !string.IsNullOrWhiteSpace(Endpoint)
        && !string.IsNullOrWhiteSpace(DatabaseName)
        && !string.IsNullOrWhiteSpace(ProgressContainer)
        && !string.IsNullOrWhiteSpace(AttemptsContainer)
        && IsNamespaceSupported;

    public bool IsNamespaceSupported =>
        string.Equals(NormalizedNamespace, "prod", StringComparison.Ordinal)
        || string.Equals(NormalizedNamespace, "local", StringComparison.Ordinal);

    public string NormalizedNamespace => (Namespace ?? string.Empty).Trim().ToLowerInvariant();

    public string ResolveProgressContainerName()
    {
        return ResolveContainerName(ProgressContainer);
    }

    public string ResolveAttemptsContainerName()
    {
        return ResolveContainerName(AttemptsContainer);
    }

    private string ResolveContainerName(string configuredContainerName)
    {
        var name = (configuredContainerName ?? string.Empty).Trim();
        if (name.Length == 0)
        {
            return string.Empty;
        }

        var namespaced = name.Replace("{namespace}", NormalizedNamespace, StringComparison.OrdinalIgnoreCase);
        if (!ReferenceEquals(namespaced, name))
        {
            return namespaced;
        }

        if (name.EndsWith("_prod", StringComparison.OrdinalIgnoreCase)
            || name.EndsWith("_local", StringComparison.OrdinalIgnoreCase))
        {
            return name;
        }

        return $"{name}_{NormalizedNamespace}";
    }
}

using Shouldly;
using WriteFluency.UsersProgressService.Options;

namespace WriteFluency.UsersProgressService.Tests.Options;

public class CosmosProgressOptionsTests
{
    [Theory]
    [InlineData("prod", "user_progress_prod", "user_attempts_prod")]
    [InlineData("local", "user_progress_local", "user_attempts_local")]
    public void ResolveContainerNames_ShouldAppendNamespace_WhenContainerIsBaseName(
        string namespaceValue,
        string expectedProgress,
        string expectedAttempts)
    {
        var options = new CosmosProgressOptions
        {
            Endpoint = "https://wf-cosmos.documents.azure.com:443/",
            DatabaseName = "wf-users-progress",
            ProgressContainer = "user_progress",
            AttemptsContainer = "user_attempts",
            Namespace = namespaceValue
        };

        options.IsConfigured.ShouldBeTrue();
        options.ResolveProgressContainerName().ShouldBe(expectedProgress);
        options.ResolveAttemptsContainerName().ShouldBe(expectedAttempts);
    }

    [Fact]
    public void ResolveContainerNames_ShouldKeepExplicitSuffixes()
    {
        var options = new CosmosProgressOptions
        {
            Endpoint = "https://wf-cosmos.documents.azure.com:443/",
            DatabaseName = "wf-users-progress",
            ProgressContainer = "user_progress_prod",
            AttemptsContainer = "user_attempts_prod",
            Namespace = "local"
        };

        options.ResolveProgressContainerName().ShouldBe("user_progress_prod");
        options.ResolveAttemptsContainerName().ShouldBe("user_attempts_prod");
    }

    [Fact]
    public void ResolveContainerNames_ShouldReplaceNamespacePlaceholder()
    {
        var options = new CosmosProgressOptions
        {
            Endpoint = "https://wf-cosmos.documents.azure.com:443/",
            DatabaseName = "wf-users-progress",
            ProgressContainer = "user_progress_{namespace}",
            AttemptsContainer = "user_attempts_{namespace}",
            Namespace = "local"
        };

        options.ResolveProgressContainerName().ShouldBe("user_progress_local");
        options.ResolveAttemptsContainerName().ShouldBe("user_attempts_local");
    }

    [Fact]
    public void IsConfigured_ShouldBeFalse_WhenNamespaceIsNotSupported()
    {
        var options = new CosmosProgressOptions
        {
            Endpoint = "https://wf-cosmos.documents.azure.com:443/",
            DatabaseName = "wf-users-progress",
            ProgressContainer = "user_progress",
            AttemptsContainer = "user_attempts",
            Namespace = "qa"
        };

        options.IsNamespaceSupported.ShouldBeFalse();
        options.IsConfigured.ShouldBeFalse();
    }
}

using System.Text.Json;
using System.Reflection;
using Microsoft.AspNetCore.Authorization;
using Shouldly;
using WriteFluency.Endpoints;
using WriteFluency.WebApi;

namespace WriteFluency.Infrastructure.Tests.WebApi;

public class PublicApiContractTests
{
    [Fact]
    public void EndpointMappers_ShouldNotContainLegacyAuthenticationMapper()
    {
        var endpointMapperTypeNames = GetEndpointMapperTypes()
            .Select(type => type.FullName)
            .ToList();

        endpointMapperTypeNames.ShouldNotContain("WriteFluency.Authentication.AuthenticationEndpointGroup");
        endpointMapperTypeNames.ShouldContain("WriteFluency.Propositions.PropositionEndpointGroup");
        endpointMapperTypeNames.ShouldContain("WriteFluency.TextComparisons.TextComparisonEndpointGroup");
    }

    [Fact]
    public void EndpointMappers_ShouldNotDeclareAuthorizeAttributes()
    {
        var endpointMapperTypes = GetEndpointMapperTypes();
        endpointMapperTypes.ShouldNotBeEmpty();

        foreach (var endpointMapperType in endpointMapperTypes)
        {
            endpointMapperType.GetCustomAttributes()
                .OfType<IAuthorizeData>()
                .ShouldBeEmpty();

            endpointMapperType.GetMethods(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.DeclaredOnly)
                .Where(method => !method.IsSpecialName)
                .SelectMany(method => method.GetCustomAttributes().OfType<IAuthorizeData>())
                .ShouldBeEmpty();
        }
    }

    [Fact]
    public void GeneratedOpenApiContract_ShouldNotContainLegacyAuthPathsOrSecuritySchemes()
    {
        var repositoryRoot = FindRepositoryRoot();
        var contractPath = Path.Combine(repositoryRoot, "src", "webapp", "src", "api", "listen-and-write", "openapi.json");

        File.Exists(contractPath).ShouldBeTrue($"Expected OpenAPI contract at '{contractPath}'.");

        using var contractJson = JsonDocument.Parse(File.ReadAllText(contractPath));
        var root = contractJson.RootElement;

        var paths = root.GetProperty("paths");
        paths.TryGetProperty("/api/authentication/token", out _).ShouldBeFalse();
        paths.TryGetProperty("/api/authentication/register", out _).ShouldBeFalse();
        paths.TryGetProperty("/api/proposition/{id}", out _).ShouldBeTrue();
        paths.TryGetProperty("/api/text-comparison/compare-texts", out _).ShouldBeTrue();

        var components = root.GetProperty("components");
        components.TryGetProperty("securitySchemes", out _).ShouldBeFalse();
        root.TryGetProperty("security", out _).ShouldBeFalse();
    }

    private static IReadOnlyList<Type> GetEndpointMapperTypes()
    {
        return typeof(EndpointConfiguration).Assembly
            .GetTypes()
            .Where(type => typeof(IEndpointMapper).IsAssignableFrom(type)
                           && !type.IsInterface
                           && !type.IsAbstract)
            .ToList();
    }

    private static string FindRepositoryRoot()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);

        while (directory is not null)
        {
            if (File.Exists(Path.Combine(directory.FullName, "WriteFluency.sln")))
            {
                return directory.FullName;
            }

            directory = directory.Parent;
        }

        throw new InvalidOperationException("Unable to locate repository root from test execution directory.");
    }
}

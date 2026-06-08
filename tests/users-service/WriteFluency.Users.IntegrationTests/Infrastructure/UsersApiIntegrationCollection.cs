namespace WriteFluency.Users.IntegrationTests.Infrastructure;

[CollectionDefinition(Name, DisableParallelization = true)]
public sealed class UsersApiIntegrationCollection : ICollectionFixture<UsersApiIntegrationFixture>
{
    public const string Name = "Users API integration tests";
}

using DotNet.Testcontainers.Containers;
using Microsoft.AspNetCore.Mvc.Testing;
using StackExchange.Redis;
using Testcontainers.PostgreSql;
using Testcontainers.Redis;

namespace WriteFluency.Users.IntegrationTests.Infrastructure;

public sealed class UsersApiIntegrationFixture : IAsyncLifetime
{
    private PostgreSqlContainer? _postgres;
    private RedisContainer? _redis;
    private IConnectionMultiplexer? _redisConnection;

    public UsersApiWebApplicationFactory? Factory { get; private set; }

    public TestEmailSender EmailSender { get; } = new();

    public bool IsAvailable => Factory is not null;

    public string? UnavailableReason { get; private set; }

    public async Task InitializeAsync()
    {
        try
        {
            _postgres = new PostgreSqlBuilder()
                .WithImage("postgres:16-alpine")
                .WithDatabase("wf-users-postgresdb")
                .WithUsername("postgres")
                .WithPassword("postgres")
                .Build();

            _redis = new RedisBuilder()
                .WithImage("redis:7.2-alpine")
                .Build();

            await _postgres.StartAsync();
            await _redis.StartAsync();

            _redisConnection = await ConnectionMultiplexer.ConnectAsync(_redis.GetConnectionString());

            Factory = new UsersApiWebApplicationFactory(
                _postgres.GetConnectionString(),
                _redis.GetConnectionString(),
                EmailSender);

            await Factory.ResetStateAsync();
        }
        catch (Exception ex)
        {
            UnavailableReason = ex.Message;
        }
    }

    public async Task ResetAsync()
    {
        if (!IsAvailable)
        {
            return;
        }

        EmailSender.Clear();
        await _redisConnection!.GetDatabase().ExecuteAsync("FLUSHDB");
    }

    public async Task DisposeAsync()
    {
        if (Factory is not null)
        {
            await Factory.DisposeAsync();
        }

        if (_redisConnection is IAsyncDisposable redisAsyncDisposable)
        {
            await redisAsyncDisposable.DisposeAsync();
        }
        else
        {
            _redisConnection?.Dispose();
        }

        if (_redis is IContainer redisContainer)
        {
            await redisContainer.DisposeAsync();
        }

        if (_postgres is IContainer postgresContainer)
        {
            await postgresContainer.DisposeAsync();
        }
    }

    public HttpClient CreateClient()
    {
        if (Factory is null)
        {
            throw new InvalidOperationException("Integration factory is not available.");
        }

        return Factory.CreateClient(new WebApplicationFactoryClientOptions
        {
            BaseAddress = new Uri("https://localhost"),
            AllowAutoRedirect = false,
            HandleCookies = true
        });
    }
}

using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using NSubstitute;
using Shouldly;
using StackExchange.Redis;
using Testcontainers.Redis;
using WriteFluency.Users.WebApi.Authentication;
using WriteFluency.Users.WebApi.Options;

namespace WriteFluency.Users.Tests.Authentication;

public class PasswordlessOtpStoreTests : IClassFixture<RedisContainerFixture>
{
    private readonly RedisContainerFixture _fixture;

    public PasswordlessOtpStoreTests(RedisContainerFixture fixture)
    {
        _fixture = fixture;
    }

    [Fact]
    public async Task CanRequestAsync_ShouldAllowFirstRequest()
    {
        if (!CanRunWithRedis())
        {
            return;
        }
        await _fixture.ResetAsync();

        var store = CreateStore(new PasswordlessOtpOptions
        {
            MaxRequestsPerWindowPerEmail = 3,
            MaxRequestsPerWindowPerIp = 20,
            RequestWindowMinutes = 15,
            MinimumSecondsBetweenRequestsPerEmail = 30
        });

        var allowed = await store.CanRequestAsync("USER@WRITEFLUENCY.COM", "1.2.3.4");

        allowed.ShouldBeTrue();
    }

    [Fact]
    public async Task CanRequestAsync_ShouldBlockWhenEmailCooldownIsActive()
    {
        if (!CanRunWithRedis())
        {
            return;
        }
        await _fixture.ResetAsync();

        var store = CreateStore(new PasswordlessOtpOptions
        {
            MaxRequestsPerWindowPerEmail = 10,
            MaxRequestsPerWindowPerIp = 100,
            RequestWindowMinutes = 15,
            MinimumSecondsBetweenRequestsPerEmail = 60
        });

        var first = await store.CanRequestAsync("USER@WRITEFLUENCY.COM", "1.2.3.4");
        var second = await store.CanRequestAsync("USER@WRITEFLUENCY.COM", "1.2.3.4");

        first.ShouldBeTrue();
        second.ShouldBeFalse();
    }

    [Fact]
    public async Task CanRequestAsync_ShouldBlockWhenEmailWindowLimitIsExceeded()
    {
        if (!CanRunWithRedis())
        {
            return;
        }
        await _fixture.ResetAsync();

        var store = CreateStore(new PasswordlessOtpOptions
        {
            MaxRequestsPerWindowPerEmail = 2,
            MaxRequestsPerWindowPerIp = 100,
            RequestWindowMinutes = 15,
            MinimumSecondsBetweenRequestsPerEmail = 1
        });

        var first = await store.CanRequestAsync("USER@WRITEFLUENCY.COM", "1.2.3.4");
        await Task.Delay(TimeSpan.FromSeconds(1.1));
        var second = await store.CanRequestAsync("USER@WRITEFLUENCY.COM", "1.2.3.4");
        await Task.Delay(TimeSpan.FromSeconds(1.1));
        var third = await store.CanRequestAsync("USER@WRITEFLUENCY.COM", "1.2.3.4");

        first.ShouldBeTrue();
        second.ShouldBeTrue();
        third.ShouldBeFalse();
    }

    [Fact]
    public async Task CanRequestAsync_ShouldBlockWhenIpWindowLimitIsExceeded()
    {
        if (!CanRunWithRedis())
        {
            return;
        }
        await _fixture.ResetAsync();

        var store = CreateStore(new PasswordlessOtpOptions
        {
            MaxRequestsPerWindowPerEmail = 100,
            MaxRequestsPerWindowPerIp = 2,
            RequestWindowMinutes = 15,
            MinimumSecondsBetweenRequestsPerEmail = 1
        });

        var first = await store.CanRequestAsync("a@writefluency.com", "1.2.3.4");
        var second = await store.CanRequestAsync("b@writefluency.com", "1.2.3.4");
        var third = await store.CanRequestAsync("c@writefluency.com", "1.2.3.4");

        first.ShouldBeTrue();
        second.ShouldBeTrue();
        third.ShouldBeFalse();
    }

    [Fact]
    public async Task ValidateCodeAsync_ShouldSucceedOnceAndRejectReuse()
    {
        if (!CanRunWithRedis())
        {
            return;
        }
        await _fixture.ResetAsync();

        var store = CreateStore(new PasswordlessOtpOptions
        {
            CodeLength = 6,
            TtlMinutes = 10,
            MaxVerifyAttempts = 5
        });

        var code = await store.IssueCodeAsync("USER@WRITEFLUENCY.COM");

        code.Length.ShouldBe(6);

        var firstValidation = await store.ValidateCodeAsync("USER@WRITEFLUENCY.COM", code);
        var secondValidation = await store.ValidateCodeAsync("USER@WRITEFLUENCY.COM", code);

        firstValidation.ShouldBeTrue();
        secondValidation.ShouldBeFalse();
    }

    [Fact]
    public async Task ValidateCodeAsync_ShouldDeleteCodeAfterMaxFailedAttempts()
    {
        if (!CanRunWithRedis())
        {
            return;
        }
        await _fixture.ResetAsync();

        var store = CreateStore(new PasswordlessOtpOptions
        {
            CodeLength = 6,
            TtlMinutes = 10,
            MaxVerifyAttempts = 2
        });

        var code = await store.IssueCodeAsync("USER@WRITEFLUENCY.COM");

        var wrongAttempt1 = await store.ValidateCodeAsync("USER@WRITEFLUENCY.COM", "000000");
        var wrongAttempt2 = await store.ValidateCodeAsync("USER@WRITEFLUENCY.COM", "111111");
        var afterLockout = await store.ValidateCodeAsync("USER@WRITEFLUENCY.COM", code);

        wrongAttempt1.ShouldBeFalse();
        wrongAttempt2.ShouldBeFalse();
        afterLockout.ShouldBeFalse();
    }

    private bool CanRunWithRedis()
    {
        return _fixture.IsAvailable;
    }

    private PasswordlessOtpStore CreateStore(PasswordlessOtpOptions options)
    {
        return new PasswordlessOtpStore(
            _fixture.ConnectionMultiplexer!,
            Options.Create(options),
            Substitute.For<ILogger<PasswordlessOtpStore>>());
    }
}

public sealed class RedisContainerFixture : IAsyncLifetime
{
    private RedisContainer? _container;

    public IConnectionMultiplexer? ConnectionMultiplexer { get; private set; }

    public string? UnavailableReason { get; private set; }

    public bool IsAvailable => ConnectionMultiplexer is not null && string.IsNullOrWhiteSpace(UnavailableReason);

    public async Task InitializeAsync()
    {
        try
        {
            _container = new RedisBuilder()
                .WithImage("redis:7.2-alpine")
                .Build();

            await _container.StartAsync();
            ConnectionMultiplexer = await StackExchange.Redis.ConnectionMultiplexer.ConnectAsync(_container.GetConnectionString());
        }
        catch (Exception ex)
        {
            UnavailableReason = ex.Message;
        }
    }

    public async Task DisposeAsync()
    {
        if (ConnectionMultiplexer is IAsyncDisposable asyncDisposable)
        {
            await asyncDisposable.DisposeAsync();
        }
        else
        {
            ConnectionMultiplexer?.Dispose();
        }

        if (_container is not null)
        {
            await _container.DisposeAsync();
        }
    }

    public async Task ResetAsync()
    {
        if (!IsAvailable)
        {
            return;
        }

        await ConnectionMultiplexer!.GetDatabase().ExecuteAsync("FLUSHDB");
    }
}

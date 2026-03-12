using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using NSubstitute;
using WriteFluency.Users.DbMigrator;

namespace WriteFluency.Users.Tests.DbMigrator;

public class WorkerTests
{
    [Fact]
    public async Task RunOnceAsync_ShouldExecuteMigrationsAndStopHost()
    {
        var migrationExecutor = Substitute.For<IUsersMigrationExecutor>();
        var hostLifetime = Substitute.For<IHostApplicationLifetime>();
        var logger = Substitute.For<ILogger<Worker>>();
        var worker = new Worker(migrationExecutor, hostLifetime, logger);

        await worker.RunOnceAsync(CancellationToken.None);

        await migrationExecutor.Received(1).MigrateAsync(Arg.Any<CancellationToken>());
        hostLifetime.Received(1).StopApplication();
    }
}

using Microsoft.EntityFrameworkCore;
using WriteFluency.Users.WebApi.Data;

namespace WriteFluency.Users.DbMigrator;

public interface IUsersMigrationExecutor
{
    Task MigrateAsync(CancellationToken cancellationToken);
}

public class EfCoreUsersMigrationExecutor(IServiceProvider serviceProvider) : IUsersMigrationExecutor
{
    public async Task MigrateAsync(CancellationToken cancellationToken)
    {
        using var scope = serviceProvider.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<UsersDbContext>();

        var strategy = dbContext.Database.CreateExecutionStrategy();
        await strategy.ExecuteAsync(async () =>
        {
            await dbContext.Database.MigrateAsync(cancellationToken);
        });
    }
}

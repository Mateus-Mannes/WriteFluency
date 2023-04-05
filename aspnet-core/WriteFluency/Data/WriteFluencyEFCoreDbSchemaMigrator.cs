using Microsoft.EntityFrameworkCore;
using Volo.Abp.DependencyInjection;

namespace WriteFluency.Data;

public class WriteFluencyEFCoreDbSchemaMigrator : ITransientDependency
{
    private readonly IServiceProvider _serviceProvider;

    public WriteFluencyEFCoreDbSchemaMigrator(
        IServiceProvider serviceProvider)
    {
        _serviceProvider = serviceProvider;
    }

    public async Task MigrateAsync()
    {
        /* We intentionally resolving the WriteFluencyDbContext
         * from IServiceProvider (instead of directly injecting it)
         * to properly get the connection string of the current tenant in the
         * current scope.
         */

        await _serviceProvider
            .GetRequiredService<WriteFluencyDbContext>()
            .Database
            .MigrateAsync();
    }
}

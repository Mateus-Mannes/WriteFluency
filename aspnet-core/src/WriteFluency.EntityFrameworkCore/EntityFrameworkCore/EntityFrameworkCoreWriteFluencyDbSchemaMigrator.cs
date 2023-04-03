using System;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using WriteFluency.Data;
using Volo.Abp.DependencyInjection;

namespace WriteFluency.EntityFrameworkCore;

public class EntityFrameworkCoreWriteFluencyDbSchemaMigrator
    : IWriteFluencyDbSchemaMigrator, ITransientDependency
{
    private readonly IServiceProvider _serviceProvider;

    public EntityFrameworkCoreWriteFluencyDbSchemaMigrator(
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

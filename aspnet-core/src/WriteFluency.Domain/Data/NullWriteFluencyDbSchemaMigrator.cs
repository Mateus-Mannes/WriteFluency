using System.Threading.Tasks;
using Volo.Abp.DependencyInjection;

namespace WriteFluency.Data;

/* This is used if database provider does't define
 * IWriteFluencyDbSchemaMigrator implementation.
 */
public class NullWriteFluencyDbSchemaMigrator : IWriteFluencyDbSchemaMigrator, ITransientDependency
{
    public Task MigrateAsync()
    {
        return Task.CompletedTask;
    }
}

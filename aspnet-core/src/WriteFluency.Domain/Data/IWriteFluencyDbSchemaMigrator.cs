using System.Threading.Tasks;

namespace WriteFluency.Data;

public interface IWriteFluencyDbSchemaMigrator
{
    Task MigrateAsync();
}

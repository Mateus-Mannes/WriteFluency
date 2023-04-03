using WriteFluency.EntityFrameworkCore;
using Volo.Abp.Autofac;
using Volo.Abp.Modularity;

namespace WriteFluency.DbMigrator;

[DependsOn(
    typeof(AbpAutofacModule),
    typeof(WriteFluencyEntityFrameworkCoreModule),
    typeof(WriteFluencyApplicationContractsModule)
    )]
public class WriteFluencyDbMigratorModule : AbpModule
{

}

using WriteFluency.EntityFrameworkCore;
using Volo.Abp.Modularity;

namespace WriteFluency;

[DependsOn(
    typeof(WriteFluencyEntityFrameworkCoreTestModule)
    )]
public class WriteFluencyDomainTestModule : AbpModule
{

}

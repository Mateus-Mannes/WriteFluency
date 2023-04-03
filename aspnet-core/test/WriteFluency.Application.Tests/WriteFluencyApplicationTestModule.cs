using Volo.Abp.Modularity;

namespace WriteFluency;

[DependsOn(
    typeof(WriteFluencyApplicationModule),
    typeof(WriteFluencyDomainTestModule)
    )]
public class WriteFluencyApplicationTestModule : AbpModule
{

}

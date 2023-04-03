using Volo.Abp.DependencyInjection;
using Volo.Abp.Ui.Branding;

namespace WriteFluency;

[Dependency(ReplaceServices = true)]
public class WriteFluencyBrandingProvider : DefaultBrandingProvider
{
    public override string AppName => "WriteFluency";
}

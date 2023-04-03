using Volo.Abp.Settings;

namespace WriteFluency.Settings;

public class WriteFluencySettingDefinitionProvider : SettingDefinitionProvider
{
    public override void Define(ISettingDefinitionContext context)
    {
        //Define your own settings here. Example:
        //context.Add(new SettingDefinition(WriteFluencySettings.MySetting1));
    }
}

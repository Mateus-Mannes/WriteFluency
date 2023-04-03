using WriteFluency.Localization;
using Volo.Abp.Authorization.Permissions;
using Volo.Abp.Localization;

namespace WriteFluency.Permissions;

public class WriteFluencyPermissionDefinitionProvider : PermissionDefinitionProvider
{
    public override void Define(IPermissionDefinitionContext context)
    {
        var myGroup = context.AddGroup(WriteFluencyPermissions.GroupName);
        //Define your own permissions here. Example:
        //myGroup.AddPermission(WriteFluencyPermissions.MyPermission1, L("Permission:MyPermission1"));
    }

    private static LocalizableString L(string name)
    {
        return LocalizableString.Create<WriteFluencyResource>(name);
    }
}

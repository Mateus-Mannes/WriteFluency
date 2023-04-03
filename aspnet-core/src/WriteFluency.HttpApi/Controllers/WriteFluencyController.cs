using WriteFluency.Localization;
using Volo.Abp.AspNetCore.Mvc;

namespace WriteFluency.Controllers;

/* Inherit your controllers from this class.
 */
public abstract class WriteFluencyController : AbpControllerBase
{
    protected WriteFluencyController()
    {
        LocalizationResource = typeof(WriteFluencyResource);
    }
}

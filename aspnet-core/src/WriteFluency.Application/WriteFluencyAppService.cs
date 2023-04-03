using System;
using System.Collections.Generic;
using System.Text;
using WriteFluency.Localization;
using Volo.Abp.Application.Services;

namespace WriteFluency;

/* Inherit your application services from this class.
 */
public abstract class WriteFluencyAppService : ApplicationService
{
    protected WriteFluencyAppService()
    {
        LocalizationResource = typeof(WriteFluencyResource);
    }
}

using Microsoft.AspNetCore.Identity;

namespace WriteFluency.Users.WebApi.Data;

public class ApplicationUser : IdentityUser
{
    public bool ListenWriteTutorialCompleted { get; set; }

    public string SubscriptionPlan { get; set; } = "free";

    public DateTimeOffset? SubscriptionCurrentPeriodEndUtc { get; set; }

    public bool SubscriptionCancelAtPeriodEnd { get; set; }
}

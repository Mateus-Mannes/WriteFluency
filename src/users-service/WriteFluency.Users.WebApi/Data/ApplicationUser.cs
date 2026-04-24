using Microsoft.AspNetCore.Identity;

namespace WriteFluency.Users.WebApi.Data;

public class ApplicationUser : IdentityUser
{
    public bool ListenWriteTutorialCompleted { get; set; }
}

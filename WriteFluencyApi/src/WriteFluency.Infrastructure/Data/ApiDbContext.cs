using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;

namespace WriteFluency.Infrastructure.Data;

public class ApiDbContext : IdentityDbContext<IdentityUser>
{
    public ApiDbContext(DbContextOptions opts) : base(opts) { }
}

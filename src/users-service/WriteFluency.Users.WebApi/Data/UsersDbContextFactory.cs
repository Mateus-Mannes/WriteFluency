using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace WriteFluency.Users.WebApi.Data;

public class UsersDbContextFactory : IDesignTimeDbContextFactory<UsersDbContext>
{
    public UsersDbContext CreateDbContext(string[] args)
    {
        var optionsBuilder = new DbContextOptionsBuilder<UsersDbContext>();
        optionsBuilder.UseNpgsql("Host=localhost;Port=5432;Database=wf-users-postgresdb;Username=postgres;Password=postgres");

        return new UsersDbContext(optionsBuilder.Options);
    }
}

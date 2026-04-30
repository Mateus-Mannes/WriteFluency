using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;

namespace WriteFluency.Users.WebApi.Data;

public class UsersDbContext(DbContextOptions<UsersDbContext> options)
    : IdentityDbContext<ApplicationUser>(options)
{
    public DbSet<UserLoginActivity> UserLoginActivities => Set<UserLoginActivity>();
    public DbSet<UserFeedbackPromptState> UserFeedbackPromptStates => Set<UserFeedbackPromptState>();

    protected override void OnModelCreating(ModelBuilder builder)
    {
        base.OnModelCreating(builder);

        builder.Entity<UserLoginActivity>(entity =>
        {
            entity.ToTable("UserLoginActivities");

            entity.HasKey(x => x.Id);
            entity.Property(x => x.Id)
                .ValueGeneratedNever();

            entity.Property(x => x.UserId)
                .IsRequired();

            entity.Property(x => x.OccurredAtUtc)
                .IsRequired();

            entity.Property(x => x.AuthMethod)
                .IsRequired();

            entity.Property(x => x.GeoLookupStatus)
                .IsRequired();

            entity.HasOne<ApplicationUser>()
                .WithMany()
                .HasForeignKey(x => x.UserId)
                .OnDelete(DeleteBehavior.Cascade);

            entity.HasIndex(x => x.OccurredAtUtc);
            entity.HasIndex(x => new { x.UserId, x.OccurredAtUtc });
        });

        builder.Entity<UserFeedbackPromptState>(entity =>
        {
            entity.ToTable("UserFeedbackPromptStates");

            entity.HasKey(x => x.Id);
            entity.Property(x => x.Id)
                .ValueGeneratedNever();

            entity.Property(x => x.UserId)
                .IsRequired();

            entity.Property(x => x.CampaignKey)
                .HasMaxLength(100)
                .IsRequired();

            entity.Property(x => x.CreatedAtUtc)
                .IsRequired();

            entity.Property(x => x.UpdatedAtUtc)
                .IsRequired();

            entity.HasOne<ApplicationUser>()
                .WithMany()
                .HasForeignKey(x => x.UserId)
                .OnDelete(DeleteBehavior.Cascade);

            entity.HasIndex(x => new { x.UserId, x.CampaignKey })
                .IsUnique();
        });
    }
}

using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;

namespace WriteFluency.Users.WebApi.Data;

public class UsersDbContext(DbContextOptions<UsersDbContext> options)
    : IdentityDbContext<ApplicationUser>(options)
{
    public DbSet<UserLoginActivity> UserLoginActivities => Set<UserLoginActivity>();
    public DbSet<UserFeedbackPromptState> UserFeedbackPromptStates => Set<UserFeedbackPromptState>();
    public DbSet<StripeWebhookEvent> StripeWebhookEvents => Set<StripeWebhookEvent>();

    protected override void OnModelCreating(ModelBuilder builder)
    {
        base.OnModelCreating(builder);

        builder.Entity<ApplicationUser>(entity =>
        {
            entity.Property(x => x.SubscriptionPlan)
                .HasMaxLength(20)
                .HasDefaultValue("free")
                .IsRequired();

            entity.Property(x => x.SubscriptionCancelAtPeriodEnd)
                .HasDefaultValue(false);

            entity.Property(x => x.StripeCustomerId)
                .HasMaxLength(255);

            entity.Property(x => x.StripeSubscriptionId)
                .HasMaxLength(255);

            entity.Property(x => x.StripeSubscriptionStatus)
                .HasMaxLength(50);
        });

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

        builder.Entity<StripeWebhookEvent>(entity =>
        {
            entity.ToTable("StripeWebhookEvents");

            entity.HasKey(x => x.StripeEventId);
            entity.Property(x => x.StripeEventId)
                .HasMaxLength(255);

            entity.Property(x => x.EventType)
                .HasMaxLength(255)
                .IsRequired();

            entity.Property(x => x.ReceivedAtUtc)
                .IsRequired();

            entity.Property(x => x.ProcessingStatus)
                .HasMaxLength(20)
                .IsRequired();

            entity.Property(x => x.LastError)
                .HasMaxLength(2000);
        });
    }
}

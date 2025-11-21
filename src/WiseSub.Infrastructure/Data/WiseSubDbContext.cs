using Microsoft.EntityFrameworkCore;
using WiseSub.Domain.Entities;

namespace WiseSub.Infrastructure.Data;

public class WiseSubDbContext : DbContext
{
    public WiseSubDbContext(DbContextOptions<WiseSubDbContext> options)
        : base(options)
    {
    }

    public DbSet<User> Users { get; set; } = null!;
    public DbSet<EmailAccount> EmailAccounts { get; set; } = null!;
    public DbSet<Subscription> Subscriptions { get; set; } = null!;
    public DbSet<Alert> Alerts { get; set; } = null!;
    public DbSet<VendorMetadata> VendorMetadata { get; set; } = null!;
    public DbSet<SubscriptionHistory> SubscriptionHistories { get; set; } = null!;
    public DbSet<EmailMetadata> EmailMetadata { get; set; } = null!;

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        // User configuration
        modelBuilder.Entity<User>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.HasIndex(e => e.Email).IsUnique();
            entity.Property(e => e.Email).IsRequired();
            entity.Property(e => e.Name).IsRequired();
            entity.Property(e => e.OAuthProvider).IsRequired();
            entity.Property(e => e.OAuthSubjectId).IsRequired();
        });

        // EmailAccount configuration
        modelBuilder.Entity<EmailAccount>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.HasIndex(e => e.UserId);
            entity.Property(e => e.EmailAddress).IsRequired();
            entity.Property(e => e.EncryptedAccessToken).IsRequired();
            entity.Property(e => e.EncryptedRefreshToken).IsRequired();

            entity.HasOne(e => e.User)
                .WithMany(u => u.EmailAccounts)
                .HasForeignKey(e => e.UserId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        // Subscription configuration
        modelBuilder.Entity<Subscription>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.HasIndex(e => new { e.UserId, e.Status });
            entity.HasIndex(e => e.NextRenewalDate);
            entity.Property(e => e.ServiceName).IsRequired();
            entity.Property(e => e.Price).HasPrecision(18, 2);
            entity.Property(e => e.Currency).IsRequired();

            entity.HasOne(e => e.User)
                .WithMany(u => u.Subscriptions)
                .HasForeignKey(e => e.UserId)
                .OnDelete(DeleteBehavior.Cascade);

            entity.HasOne(e => e.EmailAccount)
                .WithMany(ea => ea.Subscriptions)
                .HasForeignKey(e => e.EmailAccountId)
                .OnDelete(DeleteBehavior.Restrict);

            entity.HasOne(e => e.Vendor)
                .WithMany(v => v.Subscriptions)
                .HasForeignKey(e => e.VendorId)
                .OnDelete(DeleteBehavior.SetNull);
        });

        // Alert configuration
        modelBuilder.Entity<Alert>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.HasIndex(e => new { e.ScheduledFor, e.Status });
            entity.Property(e => e.Message).IsRequired();

            entity.HasOne(e => e.User)
                .WithMany(u => u.Alerts)
                .HasForeignKey(e => e.UserId)
                .OnDelete(DeleteBehavior.Cascade);

            entity.HasOne(e => e.Subscription)
                .WithMany(s => s.Alerts)
                .HasForeignKey(e => e.SubscriptionId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        // VendorMetadata configuration
        modelBuilder.Entity<VendorMetadata>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.HasIndex(e => e.NormalizedName);
            entity.Property(e => e.Name).IsRequired();
            entity.Property(e => e.NormalizedName).IsRequired();
        });

        // SubscriptionHistory configuration
        modelBuilder.Entity<SubscriptionHistory>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.HasIndex(e => e.SubscriptionId);
            entity.Property(e => e.ChangeType).IsRequired();

            entity.HasOne(e => e.Subscription)
                .WithMany(s => s.History)
                .HasForeignKey(e => e.SubscriptionId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        // EmailMetadata configuration
        modelBuilder.Entity<EmailMetadata>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.HasIndex(e => e.EmailAccountId);
            entity.HasIndex(e => e.ExternalEmailId);
            entity.Property(e => e.Sender).IsRequired();
            entity.Property(e => e.Subject).IsRequired();

            entity.HasOne(e => e.EmailAccount)
                .WithMany(ea => ea.EmailMetadata)
                .HasForeignKey(e => e.EmailAccountId)
                .OnDelete(DeleteBehavior.Cascade);
        });
    }
}

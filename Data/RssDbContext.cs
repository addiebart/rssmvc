using System.Globalization;
using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using RssAsp.Models;

namespace RssAsp.Data;

public class RssDbContext(DbContextOptions<RssDbContext> options)
    : IdentityDbContext<User, IdentityRole<int>, int>(options)
{
    public DbSet<Feed> Feeds => Set<Feed>();

    public DbSet<Subscription> Subscriptions => Set<Subscription>();

    public DbSet<Article> Articles => Set<Article>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        modelBuilder.Entity<User>(entity =>
        {
            entity.ToTable("Users");
            entity.Property(user => user.UserName).HasColumnName("Username").HasMaxLength(100);
            entity.Property(user => user.NormalizedUserName).HasColumnName("NormalizedUsername").HasMaxLength(100);
            entity.Property(user => user.CreatedAt)
                .IsRequired()
                .HasConversion(
                    value => value.UtcDateTime.ToString("O", CultureInfo.InvariantCulture),
                    value => DateTimeOffset.Parse(
                        value,
                        CultureInfo.InvariantCulture,
                        DateTimeStyles.RoundtripKind))
                .HasColumnType("TEXT");
            entity.HasIndex(user => user.NormalizedUserName).IsUnique();
        });

        modelBuilder.Entity<Feed>(entity =>
        {
            entity.HasKey(feed => feed.Id);
            entity.HasIndex(feed => feed.FeedUrl).IsUnique();
            entity.Property(feed => feed.FeedUrl).IsRequired();
            entity.Property(feed => feed.LastUpdated)
                .HasConversion(
                    value => value.UtcDateTime.ToString("O", CultureInfo.InvariantCulture),
                    value => DateTimeOffset.Parse(
                        value,
                        CultureInfo.InvariantCulture,
                        DateTimeStyles.RoundtripKind))
                .HasColumnType("TEXT");
            entity.Property(feed => feed.LastFailed)
                .HasConversion(
                    value => value.HasValue
                        ? value.Value.UtcDateTime.ToString("O", CultureInfo.InvariantCulture)
                        : null,
                    value => value == null
                        ? null
                        : DateTimeOffset.Parse(
                            value,
                            CultureInfo.InvariantCulture,
                            DateTimeStyles.RoundtripKind))
                .HasColumnType("TEXT");
        });

        modelBuilder.Entity<Subscription>(entity =>
        {
            entity.HasKey(subscription => subscription.Id);
            entity.HasIndex(subscription => new { subscription.UserId, subscription.FeedId })
                .IsUnique();
            entity.Property(subscription => subscription.DisplayTitle)
                .IsRequired()
                .HasMaxLength(250);
            entity.Property(subscription => subscription.SubscribedAt)
                .IsRequired()
                .HasConversion(
                    value => value.UtcDateTime.ToString("O", CultureInfo.InvariantCulture),
                    value => DateTimeOffset.Parse(
                        value,
                        CultureInfo.InvariantCulture,
                        DateTimeStyles.RoundtripKind))
                .HasColumnType("TEXT");

            entity.HasOne(subscription => subscription.User)
                .WithMany(user => user.Subscriptions)
                .HasForeignKey(subscription => subscription.UserId)
                .OnDelete(DeleteBehavior.Cascade);

            entity.HasOne(subscription => subscription.Feed)
                .WithMany(feed => feed.Subscriptions)
                .HasForeignKey(subscription => subscription.FeedId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        modelBuilder.Entity<Article>(entity =>
        {
            entity.HasKey(article => article.Id);
            entity.HasIndex(article => new { article.FeedId, article.FeedItemId })
                .IsUnique();
            entity.Property(article => article.FeedItemId)
                .HasMaxLength(500);
            entity.Property(article => article.PublishedAt)
                .HasConversion(
                    value => value.UtcDateTime.ToString("O", CultureInfo.InvariantCulture),
                    value => DateTimeOffset.Parse(
                        value,
                        CultureInfo.InvariantCulture,
                        DateTimeStyles.RoundtripKind))
                .HasColumnType("TEXT");
            entity.Property(article => article.CreatedAt)
                .IsRequired()
                .HasConversion(
                    value => value.UtcDateTime.ToString("O", CultureInfo.InvariantCulture),
                    value => DateTimeOffset.Parse(
                        value,
                        CultureInfo.InvariantCulture,
                        DateTimeStyles.RoundtripKind))
                .HasColumnType("TEXT");
            entity.Property(article => article.Title).IsRequired().HasMaxLength(500);
            entity.HasOne(article => article.Feed)
                .WithMany(feed => feed.Articles)
                .HasForeignKey(article => article.FeedId)
                .OnDelete(DeleteBehavior.Cascade);
        });
    }
}
